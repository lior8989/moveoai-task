using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Application;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring;

namespace WeatherRisk.Core.Agent.Tools;

/// <summary>
/// Compact result shapes returned to the model. Kept small on purpose (tokens = latency and cost):
/// rankings carry no evidence; the per-hub tool carries the full breakdown.
/// </summary>
internal static class ToolResults
{
    /// <summary>Hub list entry.</summary>
    public sealed record HubItem(string Id, string Name, string City, string State, Region Region);

    /// <summary>Ranking entry.</summary>
    public sealed record RankItem(
        int Rank, string HubId, string HubName, Region Region, double Score, RiskRating Rating,
        double CompositeScore, IReadOnlyList<HazardType> TopDrivers, ScoreConfidence Confidence);

    /// <summary>Per-hazard breakdown with evidence.</summary>
    public sealed record HazardItem(
        HazardType Hazard, double Score, RiskRating Rating, double? ObservedScore, double? BaselineScore,
        double Contribution, bool IsWorstHazard, IReadOnlyList<ScoreFactor> Evidence);

    /// <summary>Full hub assessment.</summary>
    public sealed record HubRiskItem(
        string HubId, string HubName, Region Region, double CompositeScore, RiskRating Rating, ScoreConfidence Confidence,
        IReadOnlyList<HazardType> TopDrivers, IReadOnlyList<HazardItem> Hazards, IReadOnlyList<string> Notes,
        DataWindow DataWindow, DataFreshness WeatherFreshness, DataFreshness BaselineFreshness);

    /// <summary>Maps an assessment.</summary>
    public static HubRiskItem ToItem(HubAssessment a) => new(
        a.Score.Hub.Id, a.Score.Hub.Name, a.Score.Hub.Region, a.Score.CompositeScore, a.Score.Rating, a.Score.Confidence,
        a.Score.TopDrivers,
        a.Score.Hazards.Select(h => new HazardItem(
            h.Detail.Hazard, h.Detail.Score, h.Detail.Rating, h.Detail.ObservedScore, h.Detail.BaselineScore,
            h.Contribution, h.IsWorst, h.Detail.Factors)).ToList(),
        a.Score.Notes, a.Provenance.Window, a.Provenance.WeatherFreshness, a.Provenance.BaselineFreshness);
}
