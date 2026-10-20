using WeatherRisk.Api.Contracts.Hubs;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring;

namespace WeatherRisk.Api.Contracts.Risk;

/// <summary>Explainable weather-disruption risk assessment of one hub.</summary>
/// <param name="Hub">The hub.</param>
/// <param name="CompositeScore">Overall risk 0–100 (higher = more exposed).</param>
/// <param name="Rating">Band of the composite: Low &lt; 25 ≤ Moderate &lt; 50 ≤ High &lt; 75 ≤ VeryHigh.</param>
/// <param name="Confidence">Data-quality confidence: High (both signals complete), Medium (one missing/partial), Low (neither).</param>
/// <param name="TopDrivers">Hazards contributing most to the composite, largest first.</param>
/// <param name="Hazards">Per-hazard breakdown; contributions sum to the composite.</param>
/// <param name="Notes">Caveats about the data behind this score.</param>
/// <param name="Data">Where the data came from and how fresh it is.</param>
public sealed record HubRiskResponse(
    HubResponse Hub,
    double CompositeScore,
    RiskRating Rating,
    ScoreConfidence Confidence,
    IReadOnlyList<HazardType> TopDrivers,
    IReadOnlyList<HazardBreakdownResponse> Hazards,
    IReadOnlyList<string> Notes,
    DataProvenanceResponse Data);

/// <summary>One hazard's contribution to a hub's risk.</summary>
/// <param name="Hazard">Hazard.</param>
/// <param name="Score">Hazard score 0–100.</param>
/// <param name="Rating">Band of the hazard score.</param>
/// <param name="ObservedScore">Part from observed weather 0–100; null when unavailable.</param>
/// <param name="BaselineScore">Part from FEMA NRI 0–100; null when unavailable.</param>
/// <param name="Weight">Normalized weight in the weighted-average part of the composite.</param>
/// <param name="Contribution">Points this hazard adds to the composite.</param>
/// <param name="IsWorstHazard">True for the hub's highest hazard (it also drives the worst-hazard half of the composite).</param>
/// <param name="Evidence">Raw inputs behind the score.</param>
public sealed record HazardBreakdownResponse(
    HazardType Hazard,
    double Score,
    RiskRating Rating,
    double? ObservedScore,
    double? BaselineScore,
    double Weight,
    double Contribution,
    bool IsWorstHazard,
    IReadOnlyList<EvidenceResponse> Evidence);

/// <summary>One piece of evidence.</summary>
/// <param name="Name">Machine-friendly name, e.g. <c>snowDaysPerYear</c>.</param>
/// <param name="Value">Value.</param>
/// <param name="Unit">Unit, e.g. <c>days/yr</c> or <c>percentile</c>.</param>
/// <param name="Description">What the value means, including thresholds.</param>
public sealed record EvidenceResponse(string Name, double Value, string Unit, string Description);

/// <summary>Provenance of the data behind a score.</summary>
/// <param name="WindowFrom">First day of the observed-weather window.</param>
/// <param name="WindowTo">Last day of the observed-weather window.</param>
/// <param name="WeatherFreshness">Fresh / Cached / Stale / Unavailable.</param>
/// <param name="WeatherRetrievedAt">When the weather was fetched from Open-Meteo.</param>
/// <param name="BaselineFreshness">Fresh / Cached / Stale / Unavailable.</param>
/// <param name="BaselineRetrievedAt">When the baseline was fetched from FEMA.</param>
public sealed record DataProvenanceResponse(
    DateOnly WindowFrom,
    DateOnly WindowTo,
    DataFreshness WeatherFreshness,
    DateTimeOffset? WeatherRetrievedAt,
    DataFreshness BaselineFreshness,
    DateTimeOffset? BaselineRetrievedAt);
