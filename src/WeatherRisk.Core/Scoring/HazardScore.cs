using WeatherRisk.Core.Domain;

namespace WeatherRisk.Core.Scoring;

/// <summary>Score for one hazard at one hub, with the evidence behind it.</summary>
/// <param name="Hazard">Hazard scored.</param>
/// <param name="Score">Blended hazard score, 0–100 (1 decimal).</param>
/// <param name="Rating">Band of <paramref name="Score"/>.</param>
/// <param name="ObservedScore">Score from observed weather, 0–100; null when weather data was unavailable.</param>
/// <param name="BaselineScore">Score from the FEMA NRI baseline, 0–100; null when NRI data was unavailable.</param>
/// <param name="Factors">Raw inputs that produced the score, for explanations (e.g. snow days per year).</param>
public sealed record HazardScore(
    HazardType Hazard,
    double Score,
    RiskRating Rating,
    double? ObservedScore,
    double? BaselineScore,
    IReadOnlyList<ScoreFactor> Factors);

/// <summary>One piece of evidence behind a hazard score.</summary>
/// <param name="Name">Machine-friendly name, e.g. <c>snowDaysPerYear</c>.</param>
/// <param name="Value">Value (1–2 decimals).</param>
/// <param name="Unit">Unit, e.g. <c>days/yr</c>, <c>NRI score</c>.</param>
/// <param name="Description">Human-readable description including the threshold used.</param>
public sealed record ScoreFactor(string Name, double Value, string Unit, string Description);
