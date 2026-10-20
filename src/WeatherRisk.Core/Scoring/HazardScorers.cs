using WeatherRisk.Core.Scoring.Hazards;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Scoring;

/// <summary>Creates the standard set of hazard scorers — one per <see cref="Domain.HazardType"/>.</summary>
/// <remarks>Used by tests and non-DI hosts (e.g. the evaluation runner); the API registers the same classes via DI.</remarks>
public static class HazardScorers
{
    /// <summary>Builds all five scorers with the given settings.</summary>
    /// <param name="settings">Model settings.</param>
    /// <param name="thresholds">Weather thresholds (used in evidence descriptions).</param>
    /// <returns>Winter, hurricane, flood, severe-storm and heat scorers.</returns>
    public static IReadOnlyList<IHazardScorer> CreateDefault(ScoringSettings settings, WeatherThresholds thresholds) =>
    [
        new WinterScorer(settings, thresholds),
        new HurricaneScorer(settings, thresholds),
        new FloodScorer(settings, thresholds),
        new SevereStormScorer(settings, thresholds),
        new HeatScorer(settings, thresholds),
    ];
}
