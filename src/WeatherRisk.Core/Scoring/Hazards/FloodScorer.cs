using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Scoring.Hazards;

/// <summary>Scores flood exposure: observed = heavy-rain days; baseline = mean of NRI Riverine and Coastal Flooding.</summary>
public sealed class FloodScorer : IHazardScorer
{
    private readonly SingleMetricScoringSettings _settings;
    private readonly string _metricDescription;

    /// <summary>Creates the scorer.</summary>
    /// <param name="settings">Scoring settings (flood section is used).</param>
    /// <param name="thresholds">Thresholds — used to describe evidence.</param>
    public FloodScorer(ScoringSettings settings, WeatherThresholds thresholds)
    {
        _settings = settings.Flood;
        _metricDescription = $"Days with precipitation ≥ {thresholds.HeavyRainMm} mm";
    }

    /// <inheritdoc />
    public HazardType Hazard => HazardType.Flood;

    /// <inheritdoc />
    /// <remarks>
    /// Coastal flooding is null for inland counties, so inland hubs are scored on riverine flooding alone
    /// rather than being diluted by a "zero coastal" value.
    /// </remarks>
    public HazardScore Score(WeatherSummary? weather, HazardBaseline? baseline) =>
        HazardMath.ScoreSingleMetric(
            Hazard, _settings, weather, baseline is not null, static w => w.HeavyRainDays,
            "heavyRainDaysPerYear", _metricDescription,
            ("Riverine Flooding", baseline?.RiverineFlooding),
            ("Coastal Flooding", baseline?.CoastalFlooding));
}
