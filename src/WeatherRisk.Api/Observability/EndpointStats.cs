using WeatherRisk.Api.Contracts.System;

namespace WeatherRisk.Api.Observability;

/// <summary>
/// Thread-safe running statistics for one endpoint (or for all traffic): outcome counters,
/// recent-traffic window, and latency window.
/// </summary>
/// <remarks>
/// A single short lock guards all fields: one uncontended lock per request is cheaper and simpler than
/// keeping several lock-free structures consistent with each other.
/// </remarks>
internal sealed class EndpointStats
{
    private readonly Lock _gate = new();
    private readonly SlidingWindowCounter _recent;
    private readonly LatencyWindow _latency;
    private long _total;
    private long _succeeded;
    private long _clientErrors;
    private long _serverErrors;
    private long _rateLimited;

    /// <summary>Creates empty statistics.</summary>
    /// <param name="settings">Window sizes for recent traffic and latency samples.</param>
    public EndpointStats(MonitoringSettings settings)
    {
        _recent = new SlidingWindowCounter(settings.WindowSeconds);
        _latency = new LatencyWindow(settings.LatencySampleSize);
    }

    /// <summary>Records one completed request.</summary>
    /// <param name="statusCode">HTTP status code returned.</param>
    /// <param name="elapsedMs">Request duration in milliseconds.</param>
    /// <param name="unixSecond">Completion time as Unix seconds.</param>
    public void Record(int statusCode, double elapsedMs, long unixSecond)
    {
        lock (_gate)
        {
            _total++;
            switch (statusCode)
            {
                case StatusCodes.Status429TooManyRequests:
                    // Counted separately from other 4xx: it signals load, not a client bug.
                    _rateLimited++;
                    break;
                case < 400:
                    _succeeded++;
                    break;
                case < 500:
                    _clientErrors++;
                    break;
                default:
                    _serverErrors++;
                    break;
            }

            _recent.Increment(unixSecond);
            _latency.Add(elapsedMs);
        }
    }

    /// <summary>Takes a consistent snapshot of the statistics.</summary>
    /// <param name="name">Endpoint label to put in the snapshot.</param>
    /// <param name="unixSecond">"Now" as Unix seconds, used for the recent-traffic window.</param>
    /// <returns>Immutable metrics for the API response.</returns>
    public EndpointMetrics ToMetrics(string name, long unixSecond)
    {
        lock (_gate)
        {
            LatencySummary latency = _latency.Summarize();
            return new EndpointMetrics(
                name,
                _total,
                _succeeded,
                _clientErrors,
                _serverErrors,
                _rateLimited,
                _recent.Sum(unixSecond),
                _recent.WindowSeconds,
                Math.Round(latency.AverageMs, 2),
                Math.Round(latency.P95Ms, 2),
                Math.Round(latency.MaxMs, 2),
                latency.Samples);
        }
    }
}
