using WeatherRisk.Core.Abstractions;

namespace WeatherRisk.Api.Contracts.Hubs;

/// <summary>Observed weather statistics for one hub over a period.</summary>
/// <param name="HubId">Hub id.</param>
/// <param name="HubName">Hub display name.</param>
/// <param name="From">First day of the period.</param>
/// <param name="To">Last day of the period.</param>
/// <param name="DaysInPeriod">Calendar days in the period.</param>
/// <param name="DaysObserved">Days with data.</param>
/// <param name="Coverage">Share of the period with data, 0–1.</param>
/// <param name="DisruptionDays">Counts of days exceeding each disruption threshold.</param>
/// <param name="Totals">Totals and extremes over the period.</param>
/// <param name="Freshness">Provenance of the daily data.</param>
/// <param name="RetrievedAt">When the data was fetched from Open-Meteo.</param>
public sealed record WeatherStatisticsResponse(
    string HubId,
    string HubName,
    DateOnly From,
    DateOnly To,
    int DaysInPeriod,
    int DaysObserved,
    double Coverage,
    IReadOnlyList<DisruptionDaysResponse> DisruptionDays,
    WeatherTotalsResponse Totals,
    DataFreshness Freshness,
    DateTimeOffset? RetrievedAt);

/// <summary>Days in the period meeting one disruption threshold.</summary>
/// <param name="Metric">Metric name, e.g. <c>snowDays</c>.</param>
/// <param name="Definition">Threshold definition, e.g. <c>snowfall ≥ 1 cm</c>.</param>
/// <param name="Days">Number of days.</param>
/// <param name="PercentOfDays">Percentage of observed days (2 decimals).</param>
/// <param name="PerYear">Average days per year over the period (2 decimals).</param>
public sealed record DisruptionDaysResponse(string Metric, string Definition, int Days, double PercentOfDays, double PerYear);

/// <summary>Totals and extremes.</summary>
/// <param name="TotalSnowfallCm">Total snowfall, cm.</param>
/// <param name="TotalPrecipitationMm">Total precipitation, mm.</param>
/// <param name="MaxWindGustKmh">Highest daily max gust, km/h.</param>
/// <param name="MaxTemperatureC">Highest daily max temperature, °C.</param>
/// <param name="MinTemperatureC">Lowest daily min temperature, °C.</param>
public sealed record WeatherTotalsResponse(
    double TotalSnowfallCm, double TotalPrecipitationMm, double? MaxWindGustKmh, double? MaxTemperatureC, double? MinTemperatureC);
