using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring;

namespace WeatherRisk.Api.Contracts.Risk;

/// <summary>Hubs ranked by exposure.</summary>
/// <param name="RankedBy"><c>Composite</c> or the hazard name the ranking used.</param>
/// <param name="Region">Region filter applied; null = all regions.</param>
/// <param name="Entries">Ranked hubs, rank 1 = most exposed.</param>
public sealed record RankingResponse(string RankedBy, Region? Region, IReadOnlyList<RankingEntryResponse> Entries);

/// <summary>A hub's position in a ranking.</summary>
/// <param name="Rank">1-based rank.</param>
/// <param name="HubId">Hub id.</param>
/// <param name="HubName">Hub name.</param>
/// <param name="Region">Hub region.</param>
/// <param name="Score">Score used for ranking (composite or selected hazard), 0–100.</param>
/// <param name="Rating">Band of <paramref name="Score"/>.</param>
/// <param name="CompositeScore">Overall composite score, 0–100.</param>
/// <param name="TopDrivers">Hazards driving the composite.</param>
/// <param name="Confidence">Data-quality confidence.</param>
/// <param name="WeatherFreshness">Provenance of weather data.</param>
/// <param name="BaselineFreshness">Provenance of FEMA data.</param>
public sealed record RankingEntryResponse(
    int Rank,
    string HubId,
    string HubName,
    Region Region,
    double Score,
    RiskRating Rating,
    double CompositeScore,
    IReadOnlyList<HazardType> TopDrivers,
    ScoreConfidence Confidence,
    DataFreshness WeatherFreshness,
    DataFreshness BaselineFreshness);

/// <summary>Side-by-side comparison of hubs.</summary>
/// <param name="HubIds">Compared hubs, in request order.</param>
/// <param name="Rows">Composite row first, then one row per hazard.</param>
/// <param name="Hubs">Full assessments of the compared hubs (for explanations).</param>
public sealed record ComparisonResponse(
    IReadOnlyList<string> HubIds, IReadOnlyList<ComparisonRowResponse> Rows, IReadOnlyList<HubRiskResponse> Hubs);

/// <summary>One metric compared across hubs.</summary>
/// <param name="Metric"><c>Composite</c> or the hazard name.</param>
/// <param name="Hazard">Hazard; null for the composite row.</param>
/// <param name="MostExposedHubId">Hub with the highest score.</param>
/// <param name="Spread">Highest minus lowest score.</param>
/// <param name="Values">Each hub's score, in request order.</param>
public sealed record ComparisonRowResponse(
    string Metric, HazardType? Hazard, string MostExposedHubId, double Spread, IReadOnlyList<HubScoreResponse> Values);

/// <summary>A hub's score on one metric.</summary>
/// <param name="HubId">Hub id.</param>
/// <param name="Score">Score 0–100.</param>
/// <param name="Rating">Band of the score.</param>
public sealed record HubScoreResponse(string HubId, double Score, RiskRating Rating);
