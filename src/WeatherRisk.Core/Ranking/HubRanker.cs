using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring;

namespace WeatherRisk.Core.Ranking;

/// <summary>Ranks and compares already-scored hubs. Pure functions over <see cref="HubRiskScore"/>s.</summary>
public static class HubRanker
{
    /// <summary>Label of the composite row in comparisons.</summary>
    public const string CompositeMetric = "Composite";

    /// <summary>
    /// Ranks hubs by composite score or by one hazard, optionally filtered by region and truncated to the top N.
    /// Ties break by composite score, then hub name — so rankings are fully deterministic.
    /// </summary>
    /// <param name="scores">Scored hubs.</param>
    /// <param name="criteria">Filter, sort key and limit.</param>
    /// <returns>Ranked hubs, rank 1 = most exposed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">When <see cref="RankingCriteria.Top"/> is not positive.</exception>
    public static IReadOnlyList<RankedHub> Rank(IEnumerable<HubRiskScore> scores, RankingCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(scores);
        ArgumentNullException.ThrowIfNull(criteria);
        if (criteria.Top is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(criteria), criteria.Top, "Top must be positive.");
        }

        IEnumerable<HubRiskScore> filtered = criteria.Region is { } region
            ? scores.Where(s => s.Hub.Region == region)
            : scores;

        IEnumerable<(HubRiskScore Score, double Key)> ordered = filtered
            .Select(s => (Score: s, Key: SortKey(s, criteria.Hazard)))
            .OrderByDescending(x => x.Key)
            .ThenByDescending(x => x.Score.CompositeScore)
            .ThenBy(x => x.Score.Hub.Name, StringComparer.Ordinal);

        if (criteria.Top is { } top)
        {
            ordered = ordered.Take(top);
        }

        return ordered
            .Select((x, index) => new RankedHub(index + 1, x.Key, RiskRatings.FromScore(x.Key), x.Score))
            .ToList();
    }

    /// <summary>Compares hubs side by side: a composite row plus one row per requested hazard.</summary>
    /// <param name="scores">Two or more scored hubs (distinct).</param>
    /// <param name="hazards">Hazards to include; null or empty = all hazards.</param>
    /// <returns>Comparison rows with each hub's score, the most exposed hub, and the spread.</returns>
    /// <exception cref="ArgumentException">When fewer than two hubs or duplicate hubs are given.</exception>
    public static HubComparison Compare(IReadOnlyList<HubRiskScore> scores, IReadOnlyCollection<HazardType>? hazards = null)
    {
        ArgumentNullException.ThrowIfNull(scores);
        if (scores.Count < 2)
        {
            throw new ArgumentException("A comparison needs at least two hubs.", nameof(scores));
        }

        if (scores.Select(s => s.Hub.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != scores.Count)
        {
            throw new ArgumentException("Each hub may appear only once in a comparison.", nameof(scores));
        }

        IEnumerable<HazardType> selected = hazards is { Count: > 0 }
            ? Enum.GetValues<HazardType>().Where(hazards.Contains) // Keep canonical order regardless of input order.
            : Enum.GetValues<HazardType>();

        var rows = new List<ComparisonRow> { BuildRow(CompositeMetric, null, scores, s => s.CompositeScore) };
        rows.AddRange(selected.Select(h => BuildRow(h.ToString(), h, scores, s => s.For(h).Detail.Score)));

        return new HubComparison(scores.Select(s => s.Hub.Id).ToList(), rows, scores);
    }

    private static double SortKey(HubRiskScore score, HazardType? hazard) =>
        hazard is { } h ? score.For(h).Detail.Score : score.CompositeScore;

    private static ComparisonRow BuildRow(
        string metric, HazardType? hazard, IReadOnlyList<HubRiskScore> scores, Func<HubRiskScore, double> value)
    {
        var values = scores
            .Select(s => new HubScoreValue(s.Hub.Id, value(s), RiskRatings.FromScore(value(s))))
            .ToList();

        HubScoreValue highest = values[0];
        double lowest = values[0].Score;
        foreach (HubScoreValue v in values)
        {
            if (v.Score > highest.Score) highest = v;
            lowest = Math.Min(lowest, v.Score);
        }

        return new ComparisonRow(metric, hazard, values, highest.HubId, Math.Round(highest.Score - lowest, 1));
    }
}

/// <summary>What to rank by and which hubs to include.</summary>
/// <param name="Region">Only hubs in this region; null = all regions.</param>
/// <param name="Hazard">Rank by this hazard's score; null = by composite score.</param>
/// <param name="Top">Return only the first N hubs; null = all.</param>
public sealed record RankingCriteria(Region? Region = null, HazardType? Hazard = null, int? Top = null);

/// <summary>A hub's position in a ranking.</summary>
/// <param name="Rank">1-based position (1 = most exposed).</param>
/// <param name="Score">Score the ranking used (composite or the selected hazard), 0–100.</param>
/// <param name="Rating">Band of <paramref name="Score"/>.</param>
/// <param name="Details">Full assessment of the hub, for explanations.</param>
public sealed record RankedHub(int Rank, double Score, RiskRating Rating, HubRiskScore Details);

/// <summary>Side-by-side comparison of hubs.</summary>
/// <param name="HubIds">Compared hubs, in input order.</param>
/// <param name="Rows">Composite row first, then one row per hazard.</param>
/// <param name="Details">Full assessments of the compared hubs, for explanations.</param>
public sealed record HubComparison(IReadOnlyList<string> HubIds, IReadOnlyList<ComparisonRow> Rows, IReadOnlyList<HubRiskScore> Details);

/// <summary>One metric compared across hubs.</summary>
/// <param name="Metric"><c>Composite</c> or the hazard name.</param>
/// <param name="Hazard">Hazard compared; null for the composite row.</param>
/// <param name="Values">Each hub's score for this metric, in input order.</param>
/// <param name="MostExposedHubId">Hub with the highest score (first one on ties).</param>
/// <param name="Spread">Highest minus lowest score — how different the hubs are on this metric.</param>
public sealed record ComparisonRow(
    string Metric, HazardType? Hazard, IReadOnlyList<HubScoreValue> Values, string MostExposedHubId, double Spread);

/// <summary>A hub's score on one compared metric.</summary>
/// <param name="HubId">Hub identifier.</param>
/// <param name="Score">Score 0–100.</param>
/// <param name="Rating">Band of <paramref name="Score"/>.</param>
public sealed record HubScoreValue(string HubId, double Score, RiskRating Rating);
