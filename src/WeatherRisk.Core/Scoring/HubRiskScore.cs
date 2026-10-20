using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Scoring;

/// <summary>Inputs needed to score one hub. Either signal may be missing; confidence reflects that.</summary>
/// <param name="Hub">Hub to score.</param>
/// <param name="Weather">Observed-weather summary for the look-back window; null when unavailable.</param>
/// <param name="Baseline">FEMA NRI baseline for the hub's county; null when unavailable.</param>
public sealed record HubHazardInputs(Hub Hub, WeatherSummary? Weather, HazardBaseline? Baseline);

/// <summary>How much the score can be trusted given the data behind it.</summary>
public enum ScoreConfidence
{
    /// <summary>Neither signal is complete — treat the score as indicative only.</summary>
    Low,

    /// <summary>One signal is missing or incomplete.</summary>
    Medium,

    /// <summary>Both observed weather (sufficient coverage) and the FEMA baseline are present.</summary>
    High,
}

/// <summary>Complete, explainable risk assessment of one hub.</summary>
/// <param name="Hub">Hub assessed.</param>
/// <param name="CompositeScore">Overall weather-disruption risk 0–100 (1 decimal).</param>
/// <param name="Rating">Band of <paramref name="CompositeScore"/>.</param>
/// <param name="Confidence">Data-quality confidence.</param>
/// <param name="Hazards">Per-hazard assessments in <see cref="HazardType"/> order; contributions sum to the composite.</param>
/// <param name="TopDrivers">Hazards contributing most to the composite (up to two), largest first.</param>
/// <param name="Notes">Caveats about the data, e.g. missing baseline or partial weather coverage.</param>
/// <param name="WeatherPeriodFrom">First day of the observed-weather window, if weather was available.</param>
/// <param name="WeatherPeriodTo">Last day of the observed-weather window, if weather was available.</param>
public sealed record HubRiskScore(
    Hub Hub,
    double CompositeScore,
    RiskRating Rating,
    ScoreConfidence Confidence,
    IReadOnlyList<HazardAssessment> Hazards,
    IReadOnlyList<HazardType> TopDrivers,
    IReadOnlyList<string> Notes,
    DateOnly? WeatherPeriodFrom,
    DateOnly? WeatherPeriodTo)
{
    /// <summary>Returns the assessment for one hazard.</summary>
    /// <param name="hazard">Hazard to look up.</param>
    /// <returns>That hazard's assessment.</returns>
    /// <remarks>Linear scan over five items — cheaper than building a dictionary per score.</remarks>
    public HazardAssessment For(HazardType hazard)
    {
        foreach (HazardAssessment assessment in Hazards)
        {
            if (assessment.Detail.Hazard == hazard)
            {
                return assessment;
            }
        }

        throw new KeyNotFoundException($"No assessment for hazard {hazard}.");
    }
}

/// <summary>A hazard's score and how much it contributed to the hub's composite.</summary>
/// <param name="Detail">Hazard score with evidence.</param>
/// <param name="Weight">Normalized weight of the hazard in the weighted-average part (weights sum to 1).</param>
/// <param name="Contribution">Points this hazard adds to the composite (all contributions sum to the composite).</param>
/// <param name="IsWorst">True for the hub's highest-scoring hazard (it also feeds the worst-hazard part).</param>
public sealed record HazardAssessment(HazardScore Detail, double Weight, double Contribution, bool IsWorst);
