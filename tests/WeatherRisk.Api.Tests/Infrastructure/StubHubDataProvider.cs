using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Api.Tests.Infrastructure;

/// <summary>
/// Deterministic stand-in for the real data provider in endpoint tests: the further north a hub, the more
/// snow; hurricanes apply only in the South. Lets tests assert real orderings without any network.
/// </summary>
public sealed class StubHubDataProvider : IHubDataProvider
{
    public static readonly DataWindow FixedWindow = new(new DateOnly(2021, 1, 1), new DateOnly(2025, 12, 31));
    public static readonly DateTimeOffset RetrievedAt = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    public DataWindow Window => FixedWindow;

    public Task<IReadOnlyDictionary<string, HubWeatherData>> GetWeatherAsync(
        IReadOnlyCollection<Hub> hubs, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, HubWeatherData> result = hubs.ToDictionary(h => h.Id, h =>
        {
            // Snow every N-th day of the year: Minneapolis (44.98°) every 4th, Chicago/Detroit every 8th, …
            int snowEvery = h.Latitude switch { > 44 => 4, > 41 => 8, > 38 => 15, _ => 0 };
            double maxTemp = h.Latitude < 34 ? 39 : 25; // Southern hubs get extreme-heat days.
            int days = FixedWindow.To.DayNumber - FixedWindow.From.DayNumber + 1;
            var rows = Enumerable.Range(0, days)
                .Select(i => FixedWindow.From.AddDays(i))
                .Select(d => new DailyWeather(
                    d,
                    snowEvery > 0 && d.DayOfYear % snowEvery == 0 ? 4 : 0,
                    d.DayOfYear % 90 == 0 ? 60 : 2,
                    d.DayOfYear % 60 == 0 ? 80 : 30,
                    d.DayOfYear % 10 == 0 ? maxTemp : 20,
                    snowEvery is > 0 and <= 4 && d.DayOfYear % 20 == 0 ? -20 : 0))
                .ToList();
            return new HubWeatherData(h.Id, FixedWindow, rows, DataFreshness.Cached, RetrievedAt);
        });
        return Task.FromResult(result);
    }

    public Task<IReadOnlyDictionary<string, HubBaselineData>> GetBaselinesAsync(
        IReadOnlyCollection<Hub> hubs, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, HubBaselineData> result = hubs
            .DistinctBy(h => h.CountyFips)
            .ToDictionary(h => h.CountyFips, h =>
            {
                bool south = h.Region == Region.South;
                var baseline = new HazardBaseline(
                    WinterWeather: south ? 10 : 70, IceStorm: 40, ColdWave: south ? 10 : 60,
                    Hurricane: south ? 90 : null, RiverineFlooding: 50, CoastalFlooding: south ? 70 : null,
                    Tornado: 60, Hail: 60, StrongWind: 50, HeatWave: south ? 80 : 30);
                return new HubBaselineData(h.CountyFips, baseline, DataFreshness.Cached, RetrievedAt);
            });
        return Task.FromResult(result);
    }
}
