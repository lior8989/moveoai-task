using Microsoft.Extensions.Options;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Domain;
using WeatherRisk.Data.External;

namespace WeatherRisk.Api.Background;

/// <summary>
/// Fills the data cache for every hub once at startup (in the background — startup isn't delayed), so the
/// first analyst question is served from SQLite. Failures are logged; the provider's fallbacks already apply.
/// </summary>
public sealed partial class DataWarmupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly HubDataSettings _settings;
    private readonly ILogger<DataWarmupService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="scopes">Creates the DI scope the scoped provider/repositories need.</param>
    /// <param name="settings">Hub data settings (warm-up switch).</param>
    /// <param name="logger">Logger.</param>
    public DataWarmupService(IServiceScopeFactory scopes, IOptions<HubDataSettings> settings, ILogger<DataWarmupService> logger)
    {
        _scopes = scopes;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.WarmUpOnStartup)
        {
            return;
        }

        try
        {
            await using AsyncServiceScope scope = _scopes.CreateAsyncScope();
            IReadOnlyList<Hub> hubs = await scope.ServiceProvider.GetRequiredService<IHubRepository>().GetAllAsync(stoppingToken);
            var provider = scope.ServiceProvider.GetRequiredService<IHubDataProvider>();

            IReadOnlyDictionary<string, HubBaselineData> baselines = await provider.GetBaselinesAsync(hubs, stoppingToken);
            IReadOnlyDictionary<string, HubWeatherData> weather = await provider.GetWeatherAsync(hubs, stoppingToken);

            LogCompleted(hubs.Count, Describe(weather.Values.Select(w => w.Freshness)), Describe(baselines.Values.Select(b => b.Freshness)));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down — nothing to do.
        }
        catch (Exception ex)
        {
            LogFailed(ex);
        }
    }

    /// <summary>Summarizes freshness counts, e.g. <c>Fresh=12, Cached=3</c>.</summary>
    private static string Describe(IEnumerable<DataFreshness> freshness) =>
        string.Join(", ", freshness.GroupBy(f => f).OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Count()}"));

    [LoggerMessage(Level = LogLevel.Information, Message = "Data warm-up done for {Hubs} hubs. Weather: {Weather}. Baselines: {Baselines}.")]
    private partial void LogCompleted(int hubs, string weather, string baselines);

    [LoggerMessage(Level = LogLevel.Error, Message = "Data warm-up failed; data will be fetched on demand.")]
    private partial void LogFailed(Exception exception);
}
