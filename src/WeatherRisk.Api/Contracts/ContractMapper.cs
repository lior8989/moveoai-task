using WeatherRisk.Api.Contracts.Hubs;
using WeatherRisk.Api.Contracts.Risk;
using WeatherRisk.Core.Application;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Ranking;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Api.Contracts;

/// <summary>Maps Core results to API contracts. Pure, allocation-proportional to the output; no business logic.</summary>
public static class ContractMapper
{
    /// <summary>Maps a hub.</summary>
    /// <param name="hub">Domain hub.</param>
    /// <returns>Contract.</returns>
    public static HubResponse ToResponse(this Hub hub) =>
        new(hub.Id, hub.Name, hub.City, hub.State, hub.Region, hub.Latitude, hub.Longitude, hub.CountyFips);

    /// <summary>Maps an assessment with its full breakdown.</summary>
    /// <param name="assessment">Assessment.</param>
    /// <returns>Contract.</returns>
    public static HubRiskResponse ToResponse(this HubAssessment assessment) =>
        ToResponse(assessment.Score, assessment.Provenance);

    /// <summary>Maps a ranking.</summary>
    /// <param name="result">Ranking result.</param>
    /// <returns>Contract.</returns>
    public static RankingResponse ToResponse(this RankingResult result) => new(
        result.Criteria.Hazard?.ToString() ?? HubRanker.CompositeMetric,
        result.Criteria.Region,
        result.Entries.Select(e =>
        {
            HubDataProvenance provenance = result.Provenance[e.Details.Hub.Id];
            return new RankingEntryResponse(
                e.Rank, e.Details.Hub.Id, e.Details.Hub.Name, e.Details.Hub.Region, e.Score, e.Rating,
                e.Details.CompositeScore, e.Details.TopDrivers, e.Details.Confidence,
                provenance.WeatherFreshness, provenance.BaselineFreshness);
        }).ToList());

    /// <summary>Maps a comparison.</summary>
    /// <param name="result">Comparison result.</param>
    /// <returns>Contract.</returns>
    public static ComparisonResponse ToResponse(this ComparisonResult result) => new(
        result.Comparison.HubIds,
        result.Comparison.Rows.Select(r => new ComparisonRowResponse(
            r.Metric, r.Hazard, r.MostExposedHubId, r.Spread,
            r.Values.Select(v => new HubScoreResponse(v.HubId, v.Score, v.Rating)).ToList())).ToList(),
        result.Comparison.Details.Select(d => ToResponse(d, result.Provenance[d.Hub.Id])).ToList());

    /// <summary>Maps weather statistics, one entry per disruption threshold.</summary>
    /// <param name="result">Statistics result.</param>
    /// <returns>Contract.</returns>
    public static WeatherStatisticsResponse ToResponse(this WeatherStatisticsResult result)
    {
        WeatherSummary s = result.Summary;
        WeatherThresholds t = result.Thresholds;
        DisruptionDaysResponse Metric(string name, string definition, int days) =>
            new(name, definition, days, Math.Round(s.PercentOfDays(days), 2), Math.Round(s.PerYear(days), 2));

        return new WeatherStatisticsResponse(
            result.Hub.Id,
            result.Hub.Name,
            s.From,
            s.To,
            s.DaysInPeriod,
            s.DaysObserved,
            Math.Round(s.Coverage, 4),
            [
                Metric("snowDays", $"snowfall ≥ {t.SnowDayCm} cm", s.SnowDays),
                Metric("hardFreezeDays", $"minimum temperature ≤ {t.HardFreezeC} °C", s.HardFreezeDays),
                Metric("heavyRainDays", $"precipitation ≥ {t.HeavyRainMm} mm", s.HeavyRainDays),
                Metric("damagingGustDays", $"wind gust ≥ {t.DamagingGustKmh} km/h", s.DamagingGustDays),
                Metric("tropicalStormGustDays", $"wind gust ≥ {t.TropicalStormGustKmh} km/h", s.TropicalStormGustDays),
                Metric("extremeHeatDays", $"maximum temperature ≥ {t.ExtremeHeatC} °C", s.ExtremeHeatDays),
            ],
            new WeatherTotalsResponse(
                Math.Round(s.TotalSnowfallCm, 1), Math.Round(s.TotalPrecipitationMm, 1),
                s.MaxWindGustKmh, s.MaxTemperatureC, s.MinTemperatureC),
            result.Freshness,
            result.RetrievedAt);
    }

    private static HubRiskResponse ToResponse(HubRiskScore score, HubDataProvenance provenance) => new(
        score.Hub.ToResponse(),
        score.CompositeScore,
        score.Rating,
        score.Confidence,
        score.TopDrivers,
        score.Hazards.Select(h => new HazardBreakdownResponse(
            h.Detail.Hazard, h.Detail.Score, h.Detail.Rating, h.Detail.ObservedScore, h.Detail.BaselineScore,
            h.Weight, h.Contribution, h.IsWorst,
            h.Detail.Factors.Select(f => new EvidenceResponse(f.Name, f.Value, f.Unit, f.Description)).ToList())).ToList(),
        score.Notes,
        new DataProvenanceResponse(
            provenance.Window.From, provenance.Window.To,
            provenance.WeatherFreshness, provenance.WeatherRetrievedAt,
            provenance.BaselineFreshness, provenance.BaselineRetrievedAt));
}
