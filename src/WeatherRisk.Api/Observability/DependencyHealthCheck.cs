using Microsoft.Extensions.Diagnostics.HealthChecks;
using WeatherRisk.Api.Contracts.System;

namespace WeatherRisk.Api.Observability;

/// <summary>
/// Passive health check for an external dependency, based on the outcome of its most recent real call.
/// It never calls the dependency itself — health requests stay fast and don't consume the free APIs' quotas.
/// </summary>
/// <remarks>
/// A failing dependency reports <see cref="HealthStatus.Degraded"/>, not Unhealthy: the API keeps serving
/// (cached data, flagged as stale), which is exactly what "degraded" means.
/// </remarks>
public sealed class DependencyHealthCheck : IHealthCheck
{
    private readonly DependencyMetricsCollector _collector;
    private readonly string _dependency;

    /// <summary>Creates the check (registered via <c>AddTypeActivatedCheck</c> with the dependency name).</summary>
    /// <param name="collector">Dependency metrics.</param>
    /// <param name="dependency">Dependency name.</param>
    public DependencyHealthCheck(DependencyMetricsCollector collector, string dependency)
    {
        _collector = collector;
        _dependency = dependency;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        DependencyMetrics? metrics = _collector.Find(_dependency);
        HealthCheckResult result = metrics?.LastCallSucceeded switch
        {
            null => HealthCheckResult.Healthy("Not called yet."),
            true => HealthCheckResult.Healthy($"Last call succeeded at {metrics.LastSuccessAt:u}."),
            false => HealthCheckResult.Degraded(
                $"Last call failed at {metrics.LastFailureAt:u} ({metrics.LastError}); cached data is served where available."),
        };
        return Task.FromResult(result);
    }
}
