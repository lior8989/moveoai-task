using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WeatherRisk.Api.Contracts.System;
using WeatherRisk.Api.Observability;
using WeatherRisk.Api.Persistence;
using WeatherRisk.Data.Persistence;

namespace WeatherRisk.Api.Endpoints;

/// <summary>Operational endpoints under <c>/api/system</c>: health, request metrics, database schema. Exempt from rate limiting.</summary>
public static class SystemEndpoints
{
    /// <summary>Maps the <c>/api/system</c> endpoint group.</summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/system").WithTags("System");

        group.MapGet("/health", GetHealthAsync)
            .WithName("GetHealth")
            .WithSummary("Health of the API and its dependencies")
            .WithDescription(
                "Runs all registered health checks. Returns 200 when Healthy or Degraded, 503 when Unhealthy. " +
                "The body lists every check with its status and duration.")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/metrics", GetMetrics)
            .WithName("GetMetrics")
            .WithSummary("API request metrics")
            .WithDescription(
                "Per-endpoint and overall request counts (succeeded, client errors, server errors, rate-limited), " +
                "requests in the recent window (load), and latency avg/p95/max (bottlenecks). In-memory; resets on restart.");

        group.MapGet("/schema", GetSchema)
            .WithName("GetDatabaseSchema")
            .WithSummary("Database schema")
            .WithDescription(
                "Tables, columns (type, nullability, primary key, meaning/unit), indexes and foreign keys — " +
                "generated at runtime from the EF Core model, so it always matches the deployed code.");

        return app;
    }

    /// <summary>Describes the database schema from the EF Core model.</summary>
    /// <param name="db">Database context.</param>
    /// <returns>200 with the schema description.</returns>
    internal static Ok<DatabaseSchemaResponse> GetSchema(WeatherRiskDbContext db) =>
        TypedResults.Ok(DatabaseSchemaDescriber.Describe(db));

    /// <summary>Runs all health checks and reports the worst status.</summary>
    /// <param name="healthChecks">ASP.NET Core health-check service.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>200 with the report when Healthy/Degraded; 503 with the report when Unhealthy.</returns>
    internal static async Task<IResult> GetHealthAsync(HealthCheckService healthChecks, CancellationToken cancellationToken)
    {
        HealthReport report = await healthChecks.CheckHealthAsync(cancellationToken);

        var response = new HealthResponse(
            report.Status.ToString(),
            Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            report.Entries
                .Select(entry => new HealthCheckEntry(
                    entry.Key,
                    entry.Value.Status.ToString(),
                    entry.Value.Description,
                    Math.Round(entry.Value.Duration.TotalMilliseconds, 2)))
                .ToList());

        // Degraded still serves traffic (e.g. using cached data), so only Unhealthy maps to 503.
        int statusCode = report.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;
        return TypedResults.Json(response, statusCode: statusCode);
    }

    /// <summary>Returns a snapshot of request metrics.</summary>
    /// <param name="collector">Metrics store.</param>
    /// <returns>200 with the current metrics snapshot.</returns>
    internal static Ok<MetricsSnapshot> GetMetrics(RequestMetricsCollector collector) =>
        TypedResults.Ok(collector.GetSnapshot());
}
