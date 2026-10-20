using Microsoft.AspNetCore.Http.HttpResults;
using WeatherRisk.Api.Contracts;
using WeatherRisk.Api.Contracts.Hubs;
using WeatherRisk.Api.Contracts.Risk;
using WeatherRisk.Core.Application;
using WeatherRisk.Core.Domain;

namespace WeatherRisk.Api.Endpoints;

/// <summary>Hub catalog, per-hub risk assessment and weather statistics under <c>/api/hubs</c>.</summary>
public static class HubEndpoints
{
    /// <summary>Maps the <c>/api/hubs</c> endpoint group.</summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapHubEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/hubs").WithTags("Hubs");

        group.MapGet("/", GetHubsAsync)
            .WithName("ListHubs")
            .WithSummary("List hubs")
            .WithDescription("All distribution hubs, optionally filtered by US Census region (Midwest, South, West, Northeast).")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id}", GetHubAsync)
            .WithName("GetHub")
            .WithSummary("Get one hub")
            .WithDescription("Hub details by id (case-insensitive), e.g. `dallas`, `new-york`.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/risk", GetRiskAsync)
            .WithName("GetHubRisk")
            .WithSummary("Risk assessment of one hub")
            .WithDescription(
                "Composite weather-disruption risk (0–100), rating, confidence, top drivers and a per-hazard breakdown " +
                "with observed/baseline parts, weights, contributions (summing to the composite) and raw evidence, " +
                "plus data provenance. Answers questions like \"why is the Dallas hub's risk high?\".")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/weather", GetWeatherAsync)
            .WithName("GetHubWeatherStatistics")
            .WithSummary("Weather statistics for a period")
            .WithDescription(
                "Disruption-day counts (snow, hard freeze, heavy rain, damaging gusts, tropical-storm gusts, extreme heat) " +
                "with percentage of days and per-year rate, plus totals and extremes. Defaults to the last full calendar " +
                "year; any period inside the data window is allowed. Answers \"what percentage of days in Denver last " +
                "year had snowfall?\".")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>Lists hubs.</summary>
    /// <param name="service">Application service.</param>
    /// <param name="region">Optional region filter.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>200 with hubs ordered by name.</returns>
    internal static async Task<Ok<List<HubResponse>>> GetHubsAsync(
        HubRiskService service, Region? region, CancellationToken cancellationToken) =>
        TypedResults.Ok((await service.GetHubsAsync(region, cancellationToken)).Select(h => h.ToResponse()).ToList());

    /// <summary>Returns one hub.</summary>
    /// <param name="service">Application service.</param>
    /// <param name="id">Hub id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>200 with the hub (404 via <see cref="HubNotFoundException"/>).</returns>
    internal static async Task<Ok<HubResponse>> GetHubAsync(
        HubRiskService service, string id, CancellationToken cancellationToken) =>
        TypedResults.Ok((await service.GetHubAsync(id, cancellationToken)).ToResponse());

    /// <summary>Scores one hub.</summary>
    /// <param name="service">Application service.</param>
    /// <param name="id">Hub id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>200 with the assessment.</returns>
    internal static async Task<Ok<HubRiskResponse>> GetRiskAsync(
        HubRiskService service, string id, CancellationToken cancellationToken) =>
        TypedResults.Ok((await service.AssessAsync(id, cancellationToken)).ToResponse());

    /// <summary>Weather statistics for a hub and period.</summary>
    /// <param name="service">Application service.</param>
    /// <param name="id">Hub id.</param>
    /// <param name="from">First day (yyyy-MM-dd); default 1 Jan of the last full year.</param>
    /// <param name="to">Last day (yyyy-MM-dd); default 31 Dec of the last full year.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>200 with statistics.</returns>
    internal static async Task<Ok<WeatherStatisticsResponse>> GetWeatherAsync(
        HubRiskService service, string id, DateOnly? from, DateOnly? to, CancellationToken cancellationToken) =>
        TypedResults.Ok((await service.GetWeatherStatisticsAsync(id, from, to, cancellationToken)).ToResponse());
}
