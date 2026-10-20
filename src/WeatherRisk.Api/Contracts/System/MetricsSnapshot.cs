namespace WeatherRisk.Api.Contracts.System;

/// <summary>Point-in-time view of API traffic, returned by <c>GET /api/system/metrics</c>.</summary>
/// <param name="GeneratedAt">When the snapshot was taken (UTC).</param>
/// <param name="UptimeSeconds">Seconds since the API process started.</param>
/// <param name="Overall">Aggregate metrics across all API endpoints.</param>
/// <param name="Endpoints">Per-endpoint metrics, busiest first.</param>
public sealed record MetricsSnapshot(
    DateTimeOffset GeneratedAt,
    double UptimeSeconds,
    EndpointMetrics Overall,
    IReadOnlyList<EndpointMetrics> Endpoints);

/// <summary>Traffic, outcome and latency metrics for one endpoint (or all traffic).</summary>
/// <param name="Endpoint">HTTP method + route template, e.g. <c>GET /api/system/health</c>; <c>ALL</c> for the aggregate; <c>(unmatched)</c> for unknown routes.</param>
/// <param name="Total">Requests since startup.</param>
/// <param name="Succeeded">Requests answered with 1xx–3xx.</param>
/// <param name="ClientErrors">Requests answered with 4xx, excluding 429.</param>
/// <param name="ServerErrors">Requests answered with 5xx (including unhandled exceptions).</param>
/// <param name="RateLimited">Requests rejected with 429 by the rate limiter.</param>
/// <param name="RequestsInWindow">Requests within the last <paramref name="WindowSeconds"/> seconds — a load indicator.</param>
/// <param name="WindowSeconds">Length of the recent-traffic window in seconds.</param>
/// <param name="AvgLatencyMs">Mean latency over the most recent <paramref name="LatencySamples"/> requests, in ms.</param>
/// <param name="P95LatencyMs">95th-percentile latency over the same samples, in ms — the bottleneck indicator.</param>
/// <param name="MaxLatencyMs">Maximum latency over the same samples, in ms.</param>
/// <param name="LatencySamples">Number of recent requests the latency figures are based on.</param>
public sealed record EndpointMetrics(
    string Endpoint,
    long Total,
    long Succeeded,
    long ClientErrors,
    long ServerErrors,
    long RateLimited,
    long RequestsInWindow,
    int WindowSeconds,
    double AvgLatencyMs,
    double P95LatencyMs,
    double MaxLatencyMs,
    int LatencySamples);
