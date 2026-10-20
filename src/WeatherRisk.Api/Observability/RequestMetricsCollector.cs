using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using WeatherRisk.Api.Contracts.System;

namespace WeatherRisk.Api.Observability;

/// <summary>
/// In-process store of API request metrics: per-endpoint and overall counts, recent traffic and latency.
/// Registered as a singleton; fed by <see cref="RequestMetricsMiddleware"/>, read by <c>GET /api/system/metrics</c>.
/// </summary>
/// <remarks>
/// Memory is bounded: endpoint keys are route templates (a finite set), and every window has a fixed size.
/// ASP.NET Core's built-in <c>http.server.*</c> meters run alongside, so <c>dotnet-counters</c> works too.
/// </remarks>
public sealed class RequestMetricsCollector
{
    /// <summary>Label used for requests to <c>/api</c> paths that matched no endpoint (prevents unbounded keys).</summary>
    public const string UnmatchedEndpoint = "(unmatched)";

    /// <summary>Label of the aggregate entry covering all API traffic.</summary>
    public const string OverallEndpoint = "ALL";

    private readonly ConcurrentDictionary<string, EndpointStats> _endpoints = new(StringComparer.Ordinal);
    private readonly EndpointStats _overall;
    private readonly MonitoringSettings _settings;
    private readonly TimeProvider _time;
    private readonly DateTimeOffset _startedAt;

    /// <summary>Creates an empty collector.</summary>
    /// <param name="settings">Window sizes.</param>
    /// <param name="time">Clock abstraction (faked in tests).</param>
    public RequestMetricsCollector(IOptions<MonitoringSettings> settings, TimeProvider time)
    {
        _settings = settings.Value;
        _time = time;
        _startedAt = time.GetUtcNow();
        _overall = new EndpointStats(_settings);
    }

    /// <summary>Records one completed API request.</summary>
    /// <param name="endpoint">Endpoint label, e.g. <c>GET /api/hubs/{id}</c>.</param>
    /// <param name="statusCode">HTTP status code returned.</param>
    /// <param name="elapsed">Request duration.</param>
    public void Record(string endpoint, int statusCode, TimeSpan elapsed)
    {
        long now = _time.GetUtcNow().ToUnixTimeSeconds();
        double ms = elapsed.TotalMilliseconds;

        // Static lambda + state argument: no closure allocation per request.
        _endpoints.GetOrAdd(endpoint, static (_, s) => new EndpointStats(s), _settings).Record(statusCode, ms, now);
        _overall.Record(statusCode, ms, now);
    }

    /// <summary>Builds a point-in-time snapshot of all metrics.</summary>
    /// <returns>Overall totals plus per-endpoint metrics, busiest endpoint first.</returns>
    public MetricsSnapshot GetSnapshot()
    {
        DateTimeOffset now = _time.GetUtcNow();
        long nowSeconds = now.ToUnixTimeSeconds();

        var endpoints = _endpoints
            .Select(pair => pair.Value.ToMetrics(pair.Key, nowSeconds))
            .OrderByDescending(m => m.Total)
            .ThenBy(m => m.Endpoint, StringComparer.Ordinal)
            .ToList();

        return new MetricsSnapshot(
            now,
            Math.Round((now - _startedAt).TotalSeconds, 1),
            _overall.ToMetrics(OverallEndpoint, nowSeconds),
            endpoints);
    }
}
