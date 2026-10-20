using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring;

namespace WeatherRisk.Api.Contracts.Risk;

/// <summary>The deterministic risk model as currently configured — for transparency and explanations.</summary>
/// <param name="Summary">One-paragraph description of how scores are computed.</param>
/// <param name="WindowFrom">First day of the observed-weather window.</param>
/// <param name="WindowTo">Last day of the observed-weather window.</param>
/// <param name="WorstHazardBlend">Share of the composite taken from the worst hazard (rest = weighted average).</param>
/// <param name="MinimumCoverage">Weather coverage required for full confidence (0–1).</param>
/// <param name="Hazards">Per-hazard signals and weights.</param>
/// <param name="RatingBands">Score bands.</param>
/// <param name="DataSources">External data sources.</param>
/// <param name="Assumptions">Key assumptions and limitations.</param>
public sealed record RiskModelResponse(
    string Summary,
    DateOnly WindowFrom,
    DateOnly WindowTo,
    double WorstHazardBlend,
    double MinimumCoverage,
    IReadOnlyList<HazardModelResponse> Hazards,
    IReadOnlyList<RatingBandResponse> RatingBands,
    IReadOnlyList<string> DataSources,
    IReadOnlyList<string> Assumptions);

/// <summary>How one hazard is scored.</summary>
/// <param name="Hazard">Hazard.</param>
/// <param name="CompositeWeight">Normalized weight in the weighted average (weights sum to 1).</param>
/// <param name="ObservedWeight">Share of the hazard score from observed weather (rest from FEMA NRI).</param>
/// <param name="ObservedSignal">Observed disruption-day definition and the rate that maps to 100.</param>
/// <param name="BaselineSignal">FEMA NRI components averaged into the baseline.</param>
public sealed record HazardModelResponse(
    HazardType Hazard, double CompositeWeight, double ObservedWeight, string ObservedSignal, string BaselineSignal);

/// <summary>A rating band.</summary>
/// <param name="Rating">Rating.</param>
/// <param name="FromInclusive">Lower bound (inclusive).</param>
/// <param name="ToExclusive">Upper bound (exclusive); null for the top band.</param>
public sealed record RatingBandResponse(RiskRating Rating, double FromInclusive, double? ToExclusive);
