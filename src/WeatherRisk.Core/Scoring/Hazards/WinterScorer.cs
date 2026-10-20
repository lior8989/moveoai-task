using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Scoring.Hazards;

/// <summary>
/// Scores winter exposure: observed = weighted snow-day and hard-freeze-day rates;
/// baseline = mean of NRI Winter Weather, Ice Storm and Cold Wave.
/// </summary>
public sealed class WinterScorer : IHazardScorer
{
    private readonly WinterScoringSettings _settings;
    private readonly WeatherThresholds _thresholds;

    /// <summary>Creates the scorer.</summary>
    /// <param name="settings">Scoring settings (winter section is used).</param>
    /// <param name="thresholds">Thresholds — used only to describe evidence accurately.</param>
    public WinterScorer(ScoringSettings settings, WeatherThresholds thresholds)
    {
        _settings = settings.Winter;
        _thresholds = thresholds;
    }

    /// <inheritdoc />
    public HazardType Hazard => HazardType.Winter;

    /// <inheritdoc />
    public HazardScore Score(WeatherSummary? weather, HazardBaseline? baseline)
    {
        var factors = new List<ScoreFactor>(5);
        double? observed = null;
        if (weather is { DaysObserved: > 0 })
        {
            double snowPerYear = weather.PerYear(weather.SnowDays);
            double freezePerYear = weather.PerYear(weather.HardFreezeDays);
            observed = (_settings.SnowShare * HazardMath.Normalize(snowPerYear, _settings.SnowDaysPerYearCap)) +
                       ((1 - _settings.SnowShare) * HazardMath.Normalize(freezePerYear, _settings.HardFreezeDaysPerYearCap));

            factors.Add(new ScoreFactor("snowDaysPerYear", HazardMath.Round(snowPerYear, 2), "days/yr",
                $"Days with snowfall ≥ {_thresholds.SnowDayCm} cm"));
            factors.Add(new ScoreFactor("hardFreezeDaysPerYear", HazardMath.Round(freezePerYear, 2), "days/yr",
                $"Days with minimum temperature ≤ {_thresholds.HardFreezeC} °C"));
        }

        double? nri = baseline is null
            ? null
            : HazardMath.AverageOfAvailable(baseline.WinterWeather, baseline.IceStorm, baseline.ColdWave);
        if (baseline is not null)
        {
            AddIfPresent(factors, "Winter Weather", baseline.WinterWeather);
            AddIfPresent(factors, "Ice Storm", baseline.IceStorm);
            AddIfPresent(factors, "Cold Wave", baseline.ColdWave);
        }

        return HazardMath.Build(Hazard, observed, nri, _settings.ObservedWeight, factors);
    }

    private static void AddIfPresent(List<ScoreFactor> factors, string name, double? value)
    {
        if (value is { } v)
        {
            factors.Add(HazardMath.NriFactor(name, v));
        }
    }
}
