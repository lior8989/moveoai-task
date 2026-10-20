using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Scoring.Hazards;

/// <summary>Scores extreme-heat exposure: observed = extreme-heat days; baseline = NRI Heat Wave.</summary>
public sealed class HeatScorer : IHazardScorer
{
    private readonly SingleMetricScoringSettings _settings;
    private readonly string _metricDescription;

    /// <summary>Creates the scorer.</summary>
    /// <param name="settings">Scoring settings (heat section is used).</param>
    /// <param name="thresholds">Thresholds — used to describe evidence.</param>
    public HeatScorer(ScoringSettings settings, WeatherThresholds thresholds)
    {
        _settings = settings.Heat;
        _metricDescription = $"Days with maximum temperature ≥ {thresholds.ExtremeHeatC} °C";
    }

    /// <inheritdoc />
    public HazardType Hazard => HazardType.Heat;

    /// <inheritdoc />
    public HazardScore Score(WeatherSummary? weather, HazardBaseline? baseline) =>
        HazardMath.ScoreSingleMetric(
            Hazard, _settings, weather, static w => w.ExtremeHeatDays,
            "extremeHeatDaysPerYear", _metricDescription,
            ("Heat Wave", baseline?.HeatWave));
}
