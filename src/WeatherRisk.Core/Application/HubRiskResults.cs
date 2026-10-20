using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Ranking;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Application;

/// <summary>A hub's risk score together with where its data came from.</summary>
/// <param name="Score">Explainable risk score.</param>
/// <param name="Provenance">Data window, freshness and retrieval times behind the score.</param>
public sealed record HubAssessment(HubRiskScore Score, HubDataProvenance Provenance);

/// <summary>Provenance of the data used for one hub — part of communicating uncertainty.</summary>
/// <param name="Window">Observed-weather window used.</param>
/// <param name="WeatherFreshness">Provenance of the weather data.</param>
/// <param name="WeatherRetrievedAt">When the weather data was fetched from the source.</param>
/// <param name="BaselineFreshness">Provenance of the FEMA baseline.</param>
/// <param name="BaselineRetrievedAt">When the baseline was fetched from the source.</param>
public sealed record HubDataProvenance(
    DataWindow Window,
    DataFreshness WeatherFreshness,
    DateTimeOffset? WeatherRetrievedAt,
    DataFreshness BaselineFreshness,
    DateTimeOffset? BaselineRetrievedAt);

/// <summary>Ranking plus the assessments it was built from.</summary>
/// <param name="Criteria">Criteria applied.</param>
/// <param name="Entries">Ranked hubs, most exposed first.</param>
/// <param name="Provenance">Provenance per hub id.</param>
public sealed record RankingResult(
    RankingCriteria Criteria, IReadOnlyList<RankedHub> Entries, IReadOnlyDictionary<string, HubDataProvenance> Provenance);

/// <summary>Side-by-side comparison plus provenance.</summary>
/// <param name="Comparison">Comparison rows and full scores.</param>
/// <param name="Provenance">Provenance per hub id.</param>
public sealed record ComparisonResult(HubComparison Comparison, IReadOnlyDictionary<string, HubDataProvenance> Provenance);

/// <summary>Weather statistics for a hub over a period.</summary>
/// <param name="Hub">Hub.</param>
/// <param name="Summary">Counts, percentages and extremes for the period.</param>
/// <param name="Thresholds">Thresholds that define each disruption day.</param>
/// <param name="Freshness">Provenance of the underlying daily data.</param>
/// <param name="RetrievedAt">When the data was fetched from the source.</param>
public sealed record WeatherStatisticsResult(
    Hub Hub, WeatherSummary Summary, WeatherThresholds Thresholds, DataFreshness Freshness, DateTimeOffset? RetrievedAt);
