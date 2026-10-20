using WeatherRisk.Core.Domain;

namespace WeatherRisk.Core.Scoring;

/// <summary>A compact, storable record of a hub's score at a point in time (no evidence/factors).</summary>
/// <param name="HubId">Hub id.</param>
/// <param name="ComputedAt">When the score was computed.</param>
/// <param name="CompositeScore">Composite score 0–100.</param>
/// <param name="Rating">Composite rating.</param>
/// <param name="Confidence">Data-quality confidence at that time.</param>
/// <param name="WinterScore">Winter hazard score 0–100.</param>
/// <param name="HurricaneScore">Hurricane hazard score 0–100.</param>
/// <param name="FloodScore">Flood hazard score 0–100.</param>
/// <param name="SevereStormScore">Severe-storm hazard score 0–100.</param>
/// <param name="HeatScore">Heat hazard score 0–100.</param>
public sealed record RiskScoreSnapshot(
    string HubId,
    DateTimeOffset ComputedAt,
    double CompositeScore,
    RiskRating Rating,
    ScoreConfidence Confidence,
    double WinterScore,
    double HurricaneScore,
    double FloodScore,
    double SevereStormScore,
    double HeatScore)
{
    /// <summary>Creates a snapshot from a full assessment.</summary>
    /// <param name="score">Full hub assessment.</param>
    /// <param name="computedAt">Timestamp to record.</param>
    /// <returns>The snapshot.</returns>
    public static RiskScoreSnapshot From(HubRiskScore score, DateTimeOffset computedAt)
    {
        ArgumentNullException.ThrowIfNull(score);
        return new RiskScoreSnapshot(
            score.Hub.Id,
            computedAt,
            score.CompositeScore,
            score.Rating,
            score.Confidence,
            score.For(HazardType.Winter).Detail.Score,
            score.For(HazardType.Hurricane).Detail.Score,
            score.For(HazardType.Flood).Detail.Score,
            score.For(HazardType.SevereStorm).Detail.Score,
            score.For(HazardType.Heat).Detail.Score);
    }
}
