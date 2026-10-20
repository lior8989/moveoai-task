using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Scoring.Hazards;

/// <summary>Scores hurricane exposure: observed = tropical-storm-force gust days; baseline = NRI Hurricane.</summary>
public sealed class HurricaneScorer : IHazardScorer
{
    private readonly SingleMetricScoringSettings _settings;
    private readonly string _metricDescription;

    /// <summary>Creates the scorer.</summary>
    /// <param name="settings">Scoring settings (hurricane section is used).</param>
    /// <param name="thresholds">Thresholds — used to describe evidence.</param>
    public HurricaneScorer(ScoringSettings settings, WeatherThresholds thresholds)
    {
        _settings = settings.Hurricane;
        _metricDescription = $"Days with wind gusts ≥ {thresholds.TropicalStormGustKmh} km/h (tropical-storm force)";
    }

    /// <inheritdoc />
    public HazardType Hazard => HazardType.Hurricane;

    /// <inheritdoc />
    public HazardScore Score(WeatherSummary? weather, HazardBaseline? baseline) =>
        HazardMath.ScoreSingleMetric(
            Hazard, _settings, weather, baseline is not null, static w => w.TropicalStormGustDays,
            "tropicalStormGustDaysPerYear", _metricDescription,
            ("Hurricane", baseline?.Hurricane));
}
