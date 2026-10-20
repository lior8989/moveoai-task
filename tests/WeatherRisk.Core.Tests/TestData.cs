using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Tests;

/// <summary>Builders for test inputs with easy-to-verify numbers.</summary>
internal static class TestData
{
    // 2021-01-01..2024-12-31 = 1461 days = exactly 4.0 years of 365.25 days → per-year rates are count / 4.
    public static readonly DateOnly From = new(2021, 1, 1);
    public static readonly DateOnly To = new(2024, 12, 31);
    public const int FourYearsOfDays = 1461;

    public static Hub Hub(string id = "test", Region region = Region.South, string? name = null) =>
        new(id, name ?? $"{id} Hub", id, "TX", region, 32.8, -96.8, "48113");

    public static WeatherSummary Weather(
        int snow = 0, int freeze = 0, int heavyRain = 0, int damagingGust = 0, int tropicalGust = 0, int heat = 0,
        int daysObserved = FourYearsOfDays) =>
        new(From, To, daysObserved, snow, freeze, heavyRain, damagingGust, tropicalGust, heat, 0, 0, null, null, null);

    public static HazardBaseline Baseline(
        double? winterWeather = null, double? iceStorm = null, double? coldWave = null, double? hurricane = null,
        double? riverine = null, double? coastal = null, double? tornado = null, double? hail = null,
        double? strongWind = null, double? heatWave = null) =>
        new(winterWeather, iceStorm, coldWave, hurricane, riverine, coastal, tornado, hail, strongWind, heatWave);

    /// <summary>A hand-built score for ranking tests (hazards not given default to 0).</summary>
    public static HubRiskScore Score(
        string id, double composite, Region region = Region.South,
        double winter = 0, double hurricane = 0, double flood = 0, double severeStorm = 0, double heat = 0)
    {
        HazardAssessment A(HazardType h, double s) =>
            new(new HazardScore(h, s, RiskRatings.FromScore(s), s, s, []), 0.2, 0, false);

        return new HubRiskScore(
            Hub(id, region),
            composite,
            RiskRatings.FromScore(composite),
            ScoreConfidence.High,
            [A(HazardType.Winter, winter), A(HazardType.Hurricane, hurricane), A(HazardType.Flood, flood),
             A(HazardType.SevereStorm, severeStorm), A(HazardType.Heat, heat)],
            [],
            [],
            From,
            To);
    }
}

/// <summary>Scorer returning a fixed score — isolates engine math from scorer math.</summary>
internal sealed class FixedScorer(HazardType hazard, double score) : IHazardScorer
{
    public HazardType Hazard => hazard;

    public HazardScore Score(WeatherSummary? weather, HazardBaseline? baseline) =>
        new(hazard, score, RiskRatings.FromScore(score), score, score, []);
}
