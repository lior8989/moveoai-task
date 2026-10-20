using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Scoring;

/// <summary>
/// Strategy that scores one hazard from observed weather and the FEMA NRI baseline.
/// Add a hazard by adding an implementation and registering it — the engine needs no changes (Open/Closed).
/// </summary>
public interface IHazardScorer
{
    /// <summary>Hazard this scorer handles. Each hazard must have exactly one scorer.</summary>
    HazardType Hazard { get; }

    /// <summary>Scores the hazard. Must be pure and thread-safe.</summary>
    /// <param name="weather">Weather summary for the look-back window; null when unavailable.</param>
    /// <param name="baseline">FEMA NRI baseline for the hub's county; null when unavailable.</param>
    /// <returns>The hazard score with its evidence.</returns>
    HazardScore Score(WeatherSummary? weather, HazardBaseline? baseline);
}
