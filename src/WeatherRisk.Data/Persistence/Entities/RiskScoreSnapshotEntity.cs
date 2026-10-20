using WeatherRisk.Core.Scoring;

namespace WeatherRisk.Data.Persistence.Entities;

/// <summary>Row of the <c>RiskScoreSnapshots</c> table: one historical score of one hub.</summary>
public sealed class RiskScoreSnapshotEntity
{
    /// <summary>Auto-increment primary key; also defines insertion (= chronological) order.</summary>
    public long Id { get; set; }

    /// <summary>Hub id (FK to <c>Hubs</c>).</summary>
    public required string HubId { get; set; }

    /// <summary>When the score was computed.</summary>
    public DateTimeOffset ComputedAt { get; set; }

    /// <summary>Composite score 0–100.</summary>
    public double CompositeScore { get; set; }

    /// <summary>Composite rating (stored as text).</summary>
    public RiskRating Rating { get; set; }

    /// <summary>Confidence (stored as text).</summary>
    public ScoreConfidence Confidence { get; set; }

    /// <summary>Winter score 0–100.</summary>
    public double WinterScore { get; set; }

    /// <summary>Hurricane score 0–100.</summary>
    public double HurricaneScore { get; set; }

    /// <summary>Flood score 0–100.</summary>
    public double FloodScore { get; set; }

    /// <summary>Severe-storm score 0–100.</summary>
    public double SevereStormScore { get; set; }

    /// <summary>Heat score 0–100.</summary>
    public double HeatScore { get; set; }

    /// <summary>Maps to the domain model.</summary>
    /// <returns>The domain snapshot.</returns>
    public RiskScoreSnapshot ToDomain() => new(
        HubId, ComputedAt, CompositeScore, Rating, Confidence,
        WinterScore, HurricaneScore, FloodScore, SevereStormScore, HeatScore);

    /// <summary>Maps from the domain model.</summary>
    /// <param name="snapshot">Domain snapshot.</param>
    /// <returns>The entity (Id assigned by the database).</returns>
    public static RiskScoreSnapshotEntity FromDomain(RiskScoreSnapshot snapshot) => new()
    {
        HubId = snapshot.HubId,
        ComputedAt = snapshot.ComputedAt,
        CompositeScore = snapshot.CompositeScore,
        Rating = snapshot.Rating,
        Confidence = snapshot.Confidence,
        WinterScore = snapshot.WinterScore,
        HurricaneScore = snapshot.HurricaneScore,
        FloodScore = snapshot.FloodScore,
        SevereStormScore = snapshot.SevereStormScore,
        HeatScore = snapshot.HeatScore,
    };
}
