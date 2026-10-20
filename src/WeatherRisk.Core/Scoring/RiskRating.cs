namespace WeatherRisk.Core.Scoring;

/// <summary>Qualitative band for a 0–100 risk score.</summary>
public enum RiskRating
{
    /// <summary>Score below 25.</summary>
    Low,

    /// <summary>Score 25 to below 50.</summary>
    Moderate,

    /// <summary>Score 50 to below 75.</summary>
    High,

    /// <summary>Score 75 and above.</summary>
    VeryHigh,
}

/// <summary>Maps scores to <see cref="RiskRating"/> bands (equal quartiles of the 0–100 scale).</summary>
public static class RiskRatings
{
    /// <summary>Lower bound (inclusive) of <see cref="RiskRating.Moderate"/>.</summary>
    public const double ModerateFrom = 25;

    /// <summary>Lower bound (inclusive) of <see cref="RiskRating.High"/>.</summary>
    public const double HighFrom = 50;

    /// <summary>Lower bound (inclusive) of <see cref="RiskRating.VeryHigh"/>.</summary>
    public const double VeryHighFrom = 75;

    /// <summary>Returns the band for a score.</summary>
    /// <param name="score">Score on the 0–100 scale.</param>
    /// <returns>The matching rating.</returns>
    public static RiskRating FromScore(double score) => score switch
    {
        >= VeryHighFrom => RiskRating.VeryHigh,
        >= HighFrom => RiskRating.High,
        >= ModerateFrom => RiskRating.Moderate,
        _ => RiskRating.Low,
    };
}
