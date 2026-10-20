using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Weather;
using WeatherRisk.Data.External;
using WeatherRisk.Data.Persistence;
using WeatherRisk.Data.Persistence.Repositories;
using WeatherRisk.Data.Persistence.Seed;

namespace WeatherRisk.Data.Tests.External;

/// <summary>Cache-first behaviour against a real SQLite cache, with fake sources and a fake clock.</summary>
public sealed class HubDataProviderTests : IAsyncLifetime
{
    private static readonly Hub Denver = HubCatalog.All.Single(h => h.Id == "denver");
    private static readonly Hub Dallas = HubCatalog.All.Single(h => h.Id == "dallas");
    private static readonly DataWindow ExpectedWindow = new(new DateOnly(2021, 1, 1), new DateOnly(2025, 12, 31));

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeWeatherSource _weather = new();
    private readonly FakeBaselineSource _baselines = new();
    private readonly FakeMetrics _metrics = new();
    private SqliteTestDatabase _database = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _database = await SqliteTestDatabase.CreateAsync();

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    /// <summary>A provider on a fresh DbContext — like a new request scope.</summary>
    private (HubDataProvider Provider, WeatherRiskDbContext Db) NewProvider(HubDataSettings? settings = null)
    {
        WeatherRiskDbContext db = _database.CreateContext();
        var provider = new HubDataProvider(
            new WeatherRepository(db), new HazardBaselineRepository(db), _weather, _baselines, _metrics,
            Options.Create(settings ?? new HubDataSettings()), _time, NullLogger<HubDataProvider>.Instance);
        return (provider, db);
    }

    private async Task<HubWeatherData> GetWeatherAsync(Hub hub, HubDataSettings? settings = null)
    {
        (HubDataProvider provider, WeatherRiskDbContext db) = NewProvider(settings);
        await using (db)
        {
            return (await provider.GetWeatherAsync([hub], Ct))[hub.Id];
        }
    }

    private async Task<IReadOnlyDictionary<string, HubBaselineData>> GetBaselinesAsync(params Hub[] hubs)
    {
        (HubDataProvider provider, WeatherRiskDbContext db) = NewProvider();
        await using (db)
        {
            return await provider.GetBaselinesAsync(hubs, Ct);
        }
    }

    // ---------------- Weather ----------------

    [Fact]
    public void Window_IsLastFiveFullCalendarYears()
    {
        Assert.Equal(ExpectedWindow, NewProvider().Provider.Window);
    }

    [Fact]
    public async Task GetWeather_EmptyCache_FetchesStoresAndReturnsFresh_ThenServesFromCache()
    {
        HubWeatherData first = await GetWeatherAsync(Denver);
        HubWeatherData second = await GetWeatherAsync(Denver);

        Assert.Equal(DataFreshness.Fresh, first.Freshness);
        Assert.Equal(1826, first.Days.Count); // 2021–2025 incl. leap year 2024
        Assert.Equal(DataFreshness.Cached, second.Freshness);
        Assert.Equal(first.Days, second.Days);
        Assert.Equal(1, _weather.Calls);
    }

    [Fact]
    public async Task GetWeather_CacheOlderThanTtl_Refetches()
    {
        await GetWeatherAsync(Denver);
        _time.Advance(TimeSpan.FromDays(31));

        HubWeatherData result = await GetWeatherAsync(Denver);

        Assert.Equal(DataFreshness.Fresh, result.Freshness);
        Assert.Equal(2, _weather.Calls);
    }

    [Fact]
    public async Task GetWeather_NewCalendarYear_WindowMovesAndRefetches()
    {
        await GetWeatherAsync(Denver);
        _time.SetUtcNow(new DateTimeOffset(2027, 1, 2, 0, 0, 0, TimeSpan.Zero)); // Within TTL, but window is now 2022–2026.

        HubWeatherData result = await GetWeatherAsync(Denver);

        Assert.Equal(DataFreshness.Fresh, result.Freshness);
        Assert.Equal(new DateOnly(2022, 1, 1), result.Window.From);
    }

    [Fact]
    public async Task GetWeather_SourceFailsWithExpiredCache_ServesStaleAndRecordsFallback()
    {
        await GetWeatherAsync(Denver);
        DateTimeOffset firstSync = _time.GetUtcNow();
        _time.Advance(TimeSpan.FromDays(31));
        _weather.FailWith = new HttpRequestException("down");

        HubWeatherData result = await GetWeatherAsync(Denver);

        Assert.Equal(DataFreshness.Stale, result.Freshness);
        Assert.Equal(1826, result.Days.Count);
        Assert.Equal(firstSync, result.RetrievedAt);
        Assert.Equal([FakeWeatherSource.SourceName], _metrics.Fallbacks);
    }

    [Fact]
    public async Task GetWeather_SourceFailsWithoutCache_ReturnsUnavailable()
    {
        _weather.FailWith = new TimeoutException();

        HubWeatherData result = await GetWeatherAsync(Denver);

        Assert.Equal(DataFreshness.Unavailable, result.Freshness);
        Assert.Empty(result.Days);
        Assert.Null(result.RetrievedAt);
        Assert.Single(_metrics.Fallbacks);
    }

    [Fact]
    public async Task GetWeather_RowsOutsideWindow_AreExcluded()
    {
        _weather.ExtraDayOutsideWindow = true;

        HubWeatherData result = await GetWeatherAsync(Denver);

        Assert.All(result.Days, d => Assert.InRange(d.Date, ExpectedWindow.From, ExpectedWindow.To));
        Assert.Equal(1826, result.Days.Count);
    }

    [Fact]
    public async Task GetWeather_ManyHubs_FetchesConcurrentlyWithinLimit()
    {
        _weather.Delay = TimeSpan.FromMilliseconds(50);
        (HubDataProvider provider, WeatherRiskDbContext db) = NewProvider(new HubDataSettings { MaxConcurrentFetches = 3 });
        await using (db)
        {
            IReadOnlyDictionary<string, HubWeatherData> result = await provider.GetWeatherAsync(HubCatalog.All, Ct);

            Assert.Equal(15, result.Count);
            Assert.All(result.Values, r => Assert.Equal(DataFreshness.Fresh, r.Freshness));
        }

        Assert.InRange(_weather.MaxConcurrency, 2, 3);
    }

    [Fact]
    public async Task GetWeather_CallerCancels_PropagatesInsteadOfFallingBack()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        (HubDataProvider provider, WeatherRiskDbContext db) = NewProvider();
        await using (db)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetWeatherAsync([Denver], cts.Token));
        }

        Assert.Empty(_metrics.Fallbacks);
    }

    // ---------------- Baselines ----------------

    [Fact]
    public async Task GetBaselines_EmptyCache_OneSourceCallForAllCounties_ThenCached()
    {
        IReadOnlyDictionary<string, HubBaselineData> first = await GetBaselinesAsync(Denver, Dallas);
        IReadOnlyDictionary<string, HubBaselineData> second = await GetBaselinesAsync(Denver, Dallas);

        Assert.Equal(1, _baselines.Calls);
        Assert.Equal(["08031", "48113"], _baselines.LastRequested.Order());
        Assert.All(first.Values, b => Assert.Equal(DataFreshness.Fresh, b.Freshness));
        Assert.All(second.Values, b => Assert.Equal(DataFreshness.Cached, b.Freshness));
        Assert.Equal(first["48113"].Baseline, second["48113"].Baseline);
    }

    [Fact]
    public async Task GetBaselines_SourceFails_StaleWhereCachedElseUnavailable()
    {
        await GetBaselinesAsync(Denver);
        _time.Advance(TimeSpan.FromDays(31));
        _baselines.FailWith = new HttpRequestException("down");

        IReadOnlyDictionary<string, HubBaselineData> result = await GetBaselinesAsync(Denver, Dallas);

        Assert.Equal(DataFreshness.Stale, result["08031"].Freshness);
        Assert.NotNull(result["08031"].Baseline);
        Assert.Equal(DataFreshness.Unavailable, result["48113"].Freshness);
        Assert.Null(result["48113"].Baseline);
        Assert.Equal([FakeBaselineSource.SourceName], _metrics.Fallbacks);
    }

    [Fact]
    public async Task GetBaselines_SourceOmitsCounty_ThatCountyIsUnavailable()
    {
        _baselines.Omit = "48113";

        IReadOnlyDictionary<string, HubBaselineData> result = await GetBaselinesAsync(Denver, Dallas);

        Assert.Equal(DataFreshness.Fresh, result["08031"].Freshness);
        Assert.Equal(DataFreshness.Unavailable, result["48113"].Freshness);
    }

    // ---------------- Fakes ----------------

    private sealed class FakeWeatherSource : IWeatherDataSource
    {
        public const string SourceName = "fake-weather";
        private int _current;
        private int _calls;
        private int _max;

        public string Name => SourceName;
        public Exception? FailWith { get; set; }
        public TimeSpan Delay { get; set; }
        public bool ExtraDayOutsideWindow { get; set; }
        public int Calls => _calls;
        public int MaxConcurrency => _max;

        public async Task<IReadOnlyList<DailyWeather>> FetchDailyAsync(
            Hub hub, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _calls);
            int now = Interlocked.Increment(ref _current);
            InterlockedMax(ref _max, now);
            try
            {
                if (Delay > TimeSpan.Zero)
                {
                    await Task.Delay(Delay, cancellationToken);
                }

                if (FailWith is not null)
                {
                    throw FailWith;
                }

                int count = to.DayNumber - from.DayNumber + 1;
                var days = Enumerable.Range(0, count).Select(i => new DailyWeather(from.AddDays(i), i % 7, 1, 20, 15, 2)).ToList();
                if (ExtraDayOutsideWindow)
                {
                    days.Add(new DailyWeather(to.AddDays(1), 99, 99, 99, 99, 99));
                }

                return days;
            }
            finally
            {
                Interlocked.Decrement(ref _current);
            }
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int snapshot;
            while (value > (snapshot = Volatile.Read(ref target)) &&
                   Interlocked.CompareExchange(ref target, value, snapshot) != snapshot)
            {
            }
        }
    }

    private sealed class FakeBaselineSource : IHazardBaselineSource
    {
        public const string SourceName = "fake-baseline";

        public string Name => SourceName;
        public Exception? FailWith { get; set; }
        public string? Omit { get; set; }
        public int Calls { get; private set; }
        public IReadOnlyCollection<string> LastRequested { get; private set; } = [];

        public Task<IReadOnlyDictionary<string, HazardBaseline>> FetchAsync(
            IReadOnlyCollection<string> countyFips, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequested = countyFips;
            if (FailWith is not null)
            {
                throw FailWith;
            }

            IReadOnlyDictionary<string, HazardBaseline> result = countyFips
                .Where(f => f != Omit)
                .ToDictionary(f => f, f => new HazardBaseline(50, 40, 30, null, 60, null, 70, 80, 90, 20));
            return Task.FromResult(result);
        }
    }

    private sealed class FakeMetrics : IDependencyMetrics
    {
        public List<string> Fallbacks { get; } = [];

        public void RecordFallback(string dependency) => Fallbacks.Add(dependency);
    }
}
