using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Scoring.Hazards;

/// <summary>Scores severe-storm exposure: observed = damaging-gust days; baseline = mean of NRI Tornado, Hail and Strong Wind.</summary>
public sealed class SevereStormScorer : IHazardScorer
{
    private readonly SingleMetricScoringSettings _settings;
    private readonly string _metricDescription;

    /// <summary>Creates the scorer.</summary>
    /// <param name="settings">Scoring settings (severe-storm section is used).</param>
    /// <param name="thresholds">Thresholds — used to describe evidence.</param>
    public SevereStormScorer(ScoringSettings settings, WeatherThresholds thresholds)
    {
        _settings = settings.SevereStorm;
        _metricDescription = $"Days with wind gusts ≥ {thresholds.DamagingGustKmh} km/h";
    }

    /// <inheritdoc />
    public HazardType Hazard => HazardType.SevereStorm;

    /// <inheritdoc />
    public HazardScore Score(WeatherSummary? weather, HazardBaseline? baseline) =>
        HazardMath.ScoreSingleMetric(
            Hazard, _settings, weather, baseline is not null, static w => w.DamagingGustDays,
            "damagingGustDaysPerYear", _metricDescription,
            ("Tornado", baseline?.Tornado),
            ("Hail", baseline?.Hail),
            ("Strong Wind", baseline?.StrongWind));
}
