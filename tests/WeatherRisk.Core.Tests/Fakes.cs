using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Application;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Tests;

/// <summary>In-memory hub catalog.</summary>
internal sealed class FakeHubs(params Hub[] hubs) : IHubRepository
{
    public Task<IReadOnlyList<Hub>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Hub>>(hubs.OrderBy(h => h.Name).ToList());

    public Task<Hub?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(hubs.FirstOrDefault(h => h.Id == Hub.NormalizeId(id)));
}

/// <summary>Deterministic data provider: snow every N-th day of the year per hub; flat FEMA baseline.</summary>
internal sealed class FakeHubDataProvider : IHubDataProvider
{
    public static readonly DataWindow FixedWindow = new(new DateOnly(2021, 1, 1), new DateOnly(2025, 12, 31));
    public static readonly DateTimeOffset Retrieved = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    public Dictionary<string, int> SnowEvery { get; } = [];
    public DataFreshness WeatherFreshness { get; set; } = DataFreshness.Fresh;
    public DataFreshness BaselineFreshness { get; set; } = DataFreshness.Cached;
    public IReadOnlyList<string> LastWeatherRequest { get; private set; } = [];

    public DataWindow Window => FixedWindow;

    public Task<IReadOnlyDictionary<string, HubWeatherData>> GetWeatherAsync(
        IReadOnlyCollection<Hub> hubs, CancellationToken cancellationToken = default)
    {
        LastWeatherRequest = hubs.Select(h => h.Id).ToList();
        IReadOnlyDictionary<string, HubWeatherData> result = hubs.ToDictionary(h => h.Id, h =>
        {
            if (WeatherFreshness == DataFreshness.Unavailable)
            {
                return new HubWeatherData(h.Id, FixedWindow, [], DataFreshness.Unavailable, null);
            }

            int every = SnowEvery.GetValueOrDefault(h.Id);
            int days = FixedWindow.To.DayNumber - FixedWindow.From.DayNumber + 1;
            var rows = Enumerable.Range(0, days)
                .Select(i => FixedWindow.From.AddDays(i))
                .Select(d => new DailyWeather(d, every > 0 && d.DayOfYear % every == 0 ? 3 : 0, 1, 20, 15, 2))
                .ToList();
            return new HubWeatherData(h.Id, FixedWindow, rows, WeatherFreshness, Retrieved);
        });
        return Task.FromResult(result);
    }

    public Task<IReadOnlyDictionary<string, HubBaselineData>> GetBaselinesAsync(
        IReadOnlyCollection<Hub> hubs, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, HubBaselineData> result = hubs
            .Select(h => h.CountyFips)
            .Distinct()
            .ToDictionary(f => f, f => new HubBaselineData(
                f, new HazardBaseline(50, 50, 50, 50, 50, null, 50, 50, 50, 50), BaselineFreshness, Retrieved));
        return Task.FromResult(result);
    }
}

/// <summary>Builds a real <see cref="HubRiskService"/> over the fakes.</summary>
internal static class ServiceFactory
{
    public static readonly Hub Minneapolis = TestData.Hub("minneapolis", Region.Midwest);
    public static readonly Hub Chicago = TestData.Hub("chicago", Region.Midwest);
    public static readonly Hub Miami = TestData.Hub("miami", Region.South);

    public static HubRiskService Create(FakeHubDataProvider provider)
    {
        var settings = new ScoringSettings();
        var thresholds = new WeatherThresholds();
        return new HubRiskService(
            new FakeHubs(Minneapolis, Chicago, Miami),
            provider,
            new WeatherStatisticsCalculator(thresholds),
            new RiskScoringEngine(HazardScorers.CreateDefault(settings, thresholds), settings),
            thresholds);
    }

    public static FakeHubDataProvider DefaultProvider()
    {
        var provider = new FakeHubDataProvider();
        provider.SnowEvery["minneapolis"] = 5;
        provider.SnowEvery["chicago"] = 10;
        return provider;
    }
}
