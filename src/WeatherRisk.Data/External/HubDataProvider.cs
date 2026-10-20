using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Data.External;

/// <summary>
/// Cache-first implementation of <see cref="IHubDataProvider"/> over SQLite repositories and external sources.
/// <list type="bullet">
/// <item>Valid cache → served as <see cref="DataFreshness.Cached"/>, no network call.</item>
/// <item>Missing/expired cache → fetched live (bounded parallelism), stored, served as <see cref="DataFreshness.Fresh"/>.</item>
/// <item>Fetch failed → older cache served as <see cref="DataFreshness.Stale"/>, else <see cref="DataFreshness.Unavailable"/>;
/// the fallback is logged and counted.</item>
/// </list>
/// </summary>
/// <remarks>
/// Scoped (it uses the scoped DbContext). HTTP calls run in parallel, but every database access stays
/// sequential on the calling flow because a DbContext is not thread-safe.
/// </remarks>
internal sealed partial class HubDataProvider : IHubDataProvider
{
    private readonly IWeatherRepository _weatherRepository;
    private readonly IHazardBaselineRepository _baselineRepository;
    private readonly IWeatherDataSource _weatherSource;
    private readonly IHazardBaselineSource _baselineSource;
    private readonly IDependencyMetrics _metrics;
    private readonly HubDataSettings _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<HubDataProvider> _logger;

    /// <summary>Creates the provider.</summary>
    /// <param name="weatherRepository">Weather cache.</param>
    /// <param name="baselineRepository">Baseline cache.</param>
    /// <param name="weatherSource">Live weather source.</param>
    /// <param name="baselineSource">Live baseline source.</param>
    /// <param name="metrics">Receives fallback events.</param>
    /// <param name="settings">Cache/fetch settings.</param>
    /// <param name="time">Clock (faked in tests).</param>
    /// <param name="logger">Logger.</param>
    public HubDataProvider(
        IWeatherRepository weatherRepository,
        IHazardBaselineRepository baselineRepository,
        IWeatherDataSource weatherSource,
        IHazardBaselineSource baselineSource,
        IDependencyMetrics metrics,
        IOptions<HubDataSettings> settings,
        TimeProvider time,
        ILogger<HubDataProvider> logger)
    {
        _weatherRepository = weatherRepository;
        _baselineRepository = baselineRepository;
        _weatherSource = weatherSource;
        _baselineSource = baselineSource;
        _metrics = metrics;
        _settings = settings.Value;
        _time = time;
        _logger = logger;
    }

    /// <inheritdoc />
    public DataWindow Window
    {
        get
        {
            // Full calendar years only: the current year is partial and would bias per-year rates.
            int lastFullYear = _time.GetUtcNow().Year - 1;
            return new DataWindow(new DateOnly(lastFullYear - _settings.LookbackYears + 1, 1, 1), new DateOnly(lastFullYear, 12, 31));
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, HubWeatherData>> GetWeatherAsync(
        IReadOnlyCollection<Hub> hubs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hubs);
        DataWindow window = Window;
        DateTimeOffset now = _time.GetUtcNow();
        var results = new Dictionary<string, HubWeatherData>(hubs.Count, StringComparer.Ordinal);

        // 1. Decide per hub: serve from cache, or refresh (sequential DB reads).
        var refresh = new List<(Hub Hub, WeatherSyncState? Previous)>();
        foreach (Hub hub in hubs.DistinctBy(h => h.Id))
        {
            WeatherSyncState? state = await _weatherRepository.GetSyncStateAsync(hub.Id, cancellationToken);
            if (IsWeatherCacheValid(state, window, now))
            {
                IReadOnlyList<DailyWeather> cached = await _weatherRepository.GetDailyAsync(hub.Id, window.From, window.To, cancellationToken);
                results[hub.Id] = new HubWeatherData(hub.Id, window, cached, DataFreshness.Cached, state!.SyncedAt);
            }
            else
            {
                refresh.Add((hub, state));
            }
        }

        if (refresh.Count == 0)
        {
            return results;
        }

        // 2. Fetch stale hubs concurrently (network only), bounded to respect the free API.
        using var gate = new SemaphoreSlim(_settings.MaxConcurrentFetches);
        var outcomes = await Task.WhenAll(refresh.Select(async item =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                IReadOnlyList<DailyWeather> days = await _weatherSource.FetchDailyAsync(item.Hub, window.From, window.To, cancellationToken);
                return (item.Hub, item.Previous, Days: days, Error: (Exception?)null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // Includes timeouts and open circuits from the resilience pipeline; caller cancellation propagates.
                return (item.Hub, item.Previous, Days: (IReadOnlyList<DailyWeather>?)null, Error: ex);
            }
            finally
            {
                gate.Release();
            }
        }));

        // 3. Store successes and resolve fallbacks (sequential DB writes).
        foreach ((Hub hub, WeatherSyncState? previous, IReadOnlyList<DailyWeather>? days, Exception? error) in outcomes)
        {
            if (days is not null)
            {
                await _weatherRepository.ReplaceDailyAsync(hub.Id, window.From, window.To, days.ToList(), now, cancellationToken);
                List<DailyWeather> inWindow = days.Where(d => d.Date >= window.From && d.Date <= window.To).OrderBy(d => d.Date).ToList();
                results[hub.Id] = new HubWeatherData(hub.Id, window, inWindow, DataFreshness.Fresh, now);
                continue;
            }

            _metrics.RecordFallback(_weatherSource.Name);
            LogFallback(_weatherSource.Name, hub.Id, previous is null ? "none" : "stale cache", error!);

            results[hub.Id] = previous is null
                ? new HubWeatherData(hub.Id, window, [], DataFreshness.Unavailable, null)
                : new HubWeatherData(
                    hub.Id, window,
                    await _weatherRepository.GetDailyAsync(hub.Id, window.From, window.To, cancellationToken),
                    DataFreshness.Stale, previous.SyncedAt);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, HubBaselineData>> GetBaselinesAsync(
        IReadOnlyCollection<Hub> hubs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hubs);
        DateTimeOffset now = _time.GetUtcNow();
        TimeSpan ttl = TimeSpan.FromDays(_settings.BaselineCacheDays);
        var results = new Dictionary<string, HubBaselineData>(StringComparer.Ordinal);
        var stale = new Dictionary<string, StoredBaseline?>(StringComparer.Ordinal);

        foreach (string fips in hubs.Select(h => h.CountyFips).Distinct(StringComparer.Ordinal))
        {
            StoredBaseline? stored = await _baselineRepository.GetAsync(fips, cancellationToken);
            if (stored is not null && now - stored.FetchedAt < ttl)
            {
                results[fips] = new HubBaselineData(fips, stored.Baseline, DataFreshness.Cached, stored.FetchedAt);
            }
            else
            {
                stale[fips] = stored;
            }
        }

        if (stale.Count == 0)
        {
            return results;
        }

        // One source call covers every county that needs refreshing (FEMA returns all counties anyway).
        IReadOnlyDictionary<string, HazardBaseline>? fetched = null;
        try
        {
            fetched = await _baselineSource.FetchAsync(stale.Keys.ToList(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _metrics.RecordFallback(_baselineSource.Name);
            LogFallback(_baselineSource.Name, string.Join(',', stale.Keys), "stale cache where available", ex);
        }

        foreach ((string fips, StoredBaseline? previous) in stale)
        {
            if (fetched is not null && fetched.TryGetValue(fips, out HazardBaseline? baseline))
            {
                await _baselineRepository.UpsertAsync(fips, baseline, now, cancellationToken);
                results[fips] = new HubBaselineData(fips, baseline, DataFreshness.Fresh, now);
            }
            else
            {
                // Source failed, or succeeded without this county: fall back per county.
                results[fips] = previous is null
                    ? new HubBaselineData(fips, null, DataFreshness.Unavailable, null)
                    : new HubBaselineData(fips, previous.Baseline, DataFreshness.Stale, previous.FetchedAt);
            }
        }

        return results;
    }

    /// <summary>A cache entry is valid when it covers the whole window and is younger than the TTL.</summary>
    private bool IsWeatherCacheValid(WeatherSyncState? state, DataWindow window, DateTimeOffset now) =>
        state is not null &&
        state.CoveredFrom <= window.From &&
        state.CoveredTo >= window.To &&
        now - state.SyncedAt < TimeSpan.FromDays(_settings.WeatherCacheDays);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Dependency {Dependency} failed for {Subject}; fallback: {Fallback}")]
    private partial void LogFallback(string dependency, string subject, string fallback, Exception exception);
}
