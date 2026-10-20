namespace WeatherRisk.Api.Contracts.System;

/// <summary>Health of the API and its dependencies, returned by <c>GET /api/system/health</c>.</summary>
/// <param name="Status">Overall status: <c>Healthy</c>, <c>Degraded</c> or <c>Unhealthy</c> (worst of all checks).</param>
/// <param name="TotalDurationMs">Time taken to run all checks, in ms.</param>
/// <param name="Checks">Individual check results (dependency checks are added as data sources are integrated).</param>
public sealed record HealthResponse(string Status, double TotalDurationMs, IReadOnlyList<HealthCheckEntry> Checks);

/// <summary>Result of a single health check.</summary>
/// <param name="Name">Check name, e.g. <c>open-meteo</c>.</param>
/// <param name="Status"><c>Healthy</c>, <c>Degraded</c> or <c>Unhealthy</c>.</param>
/// <param name="Description">Optional human-readable detail.</param>
/// <param name="DurationMs">Time taken by this check, in ms.</param>
public sealed record HealthCheckEntry(string Name, string Status, string? Description, double DurationMs);
