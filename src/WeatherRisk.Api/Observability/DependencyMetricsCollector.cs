using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using WeatherRisk.Api.Contracts.System;
using WeatherRisk.Core.Abstractions;

namespace WeatherRisk.Api.Observability;

/// <summary>
/// In-process metrics per external dependency: logical calls, failures, fallbacks served, latency and the
/// outcome of the most recent call. Fed by <see cref="DependencyMetricsHandler"/> (calls) and by the data
/// layer through <see cref="IDependencyMetrics"/> (fallbacks). Singleton.
/// </summary>
public sealed class DependencyMetricsCollector : IDependencyMetrics
{
    private readonly ConcurrentDictionary<string, DependencyStats> _dependencies = new(StringComparer.Ordinal);
    private readonly MonitoringSettings _settings;
    private readonly TimeProvider _time;

    /// <summary>Creates an empty collector.</summary>
    /// <param name="settings">Latency sample size.</param>
    /// <param name="time">Clock (faked in tests).</param>
    public DependencyMetricsCollector(IOptions<MonitoringSettings> settings, TimeProvider time)
    {
        _settings = settings.Value;
        _time = time;
    }

    /// <summary>Records the outcome of one logical call (after retries).</summary>
    /// <param name="dependency">Dependency name.</param>
    /// <param name="succeeded">Whether a successful response was received.</param>
    /// <param name="elapsed">Total duration including retries.</param>
    /// <param name="error">Short failure description (status code or exception type); null on success.</param>
    public void RecordCall(string dependency, bool succeeded, TimeSpan elapsed, string? error = null) =>
        Get(dependency).RecordCall(succeeded, elapsed.TotalMilliseconds, _time.GetUtcNow(), error);

    /// <inheritdoc />
    public void RecordFallback(string dependency) => Get(dependency).RecordFallback();

    /// <summary>Returns the latest call outcome for a dependency, or null when it has not been called yet.</summary>
    /// <param name="dependency">Dependency name.</param>
    /// <returns>Snapshot or null.</returns>
    public DependencyMetrics? Find(string dependency) =>
        _dependencies.TryGetValue(dependency, out DependencyStats? stats) ? stats.ToMetrics(dependency) : null;

    /// <summary>Snapshot of all dependencies, alphabetically.</summary>
    /// <returns>Per-dependency metrics.</returns>
    public IReadOnlyList<DependencyMetrics> GetSnapshot() => _dependencies
        .Select(pair => pair.Value.ToMetrics(pair.Key))
        .OrderBy(m => m.Dependency, StringComparer.Ordinal)
        .ToList();

    private DependencyStats Get(string dependency) =>
        _dependencies.GetOrAdd(dependency, static (_, s) => new DependencyStats(s.LatencySampleSize), _settings);

    /// <summary>Thread-safe counters for one dependency (single short lock, like <see cref="EndpointStats"/>).</summary>
    private sealed class DependencyStats(int latencySamples)
    {
        private readonly Lock _gate = new();
        private readonly LatencyWindow _latency = new(latencySamples);
        private long _calls;
        private long _succeeded;
        private long _failed;
        private long _fallbacks;
        private bool? _lastCallSucceeded;
        private DateTimeOffset? _lastSuccessAt;
        private DateTimeOffset? _lastFailureAt;
        private string? _lastError;

        public void RecordCall(bool succeeded, double elapsedMs, DateTimeOffset now, string? error)
        {
            lock (_gate)
            {
                _calls++;
                _latency.Add(elapsedMs);
                _lastCallSucceeded = succeeded;
                if (succeeded)
                {
                    _succeeded++;
                    _lastSuccessAt = now;
                }
                else
                {
                    _failed++;
                    _lastFailureAt = now;
                    _lastError = error;
                }
            }
        }

        public void RecordFallback()
        {
            lock (_gate)
            {
                _fallbacks++;
            }
        }

        public DependencyMetrics ToMetrics(string name)
        {
            lock (_gate)
            {
                LatencySummary latency = _latency.Summarize();
                return new DependencyMetrics(
                    name, _calls, _succeeded, _failed, _fallbacks,
                    Math.Round(latency.AverageMs, 2), Math.Round(latency.P95Ms, 2),
                    _lastCallSucceeded, _lastSuccessAt, _lastFailureAt, _lastError);
            }
        }
    }
}
