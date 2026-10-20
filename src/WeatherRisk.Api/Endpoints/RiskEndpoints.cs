using Microsoft.AspNetCore.Http.HttpResults;
using WeatherRisk.Api.Contracts;
using WeatherRisk.Api.Contracts.Risk;
using WeatherRisk.Api.Risk;
using WeatherRisk.Core.Application;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Ranking;

namespace WeatherRisk.Api.Endpoints;

/// <summary>Cross-hub analysis under <c>/api/risk</c>: rankings, comparisons and the model description.</summary>
public static class RiskEndpoints
{
    /// <summary>Maps the <c>/api/risk</c> endpoint group.</summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapRiskEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/risk").WithTags("Risk");

        group.MapGet("/rankings", RankAsync)
            .WithName("RankHubs")
            .WithSummary("Rank hubs by exposure")
            .WithDescription(
                "Ranks hubs by composite score, or by one hazard's score, optionally within a region and limited to the " +
                "top N. Ties break by composite score, then name. Answers \"which hubs in the Midwest are most exposed " +
                "to winter disruption?\" (`region=Midwest&hazard=Winter`).")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/compare", CompareAsync)
            .WithName("CompareHubs")
            .WithSummary("Compare hubs side by side")
            .WithDescription(
                "Composite row plus one row per hazard with each hub's score, the most exposed hub and the spread, " +
                "plus each hub's full breakdown. `hubs` accepts repeated values or a comma-separated list. Answers " +
                "\"compare Miami and Houston for hurricane and flood exposure\" (`hubs=miami,houston&hazards=Hurricane&hazards=Flood`).")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/model", DescribeModel)
            .WithName("DescribeRiskModel")
            .WithSummary("How scores are computed")
            .WithDescription("Signals, thresholds, weights, rating bands, data sources and assumptions of the deterministic model.");

        return app;
    }

    /// <summary>Ranks hubs.</summary>
    /// <param name="service">Application service.</param>
    /// <param name="region">Optional region filter.</param>
    /// <param name="hazard">Optional hazard to rank by (default: composite).</param>
    /// <param name="top">Optional number of hubs to return (positive).</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>200 with the ranking.</returns>
    internal static async Task<Ok<RankingResponse>> RankAsync(
        HubRiskService service, Region? region, HazardType? hazard, int? top, CancellationToken cancellationToken) =>
        TypedResults.Ok((await service.RankAsync(new RankingCriteria(region, hazard, top), cancellationToken)).ToResponse());

    /// <summary>Compares hubs.</summary>
    /// <param name="service">Application service.</param>
    /// <param name="hubs">Hub ids — repeated and/or comma-separated (2–15).</param>
    /// <param name="hazards">Hazards to include (repeated); empty = all.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>200 with the comparison.</returns>
    internal static async Task<Ok<ComparisonResponse>> CompareAsync(
        HubRiskService service, string[]? hubs, HazardType[]? hazards, CancellationToken cancellationToken)
    {
        // Accept both ?hubs=a&hubs=b and ?hubs=a,b — the latter is easier to type by hand.
        List<string> ids = (hubs ?? [])
            .SelectMany(h => h.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();
        ComparisonResult result = await service.CompareAsync(ids, hazards, cancellationToken);
        return TypedResults.Ok(result.ToResponse());
    }

    /// <summary>Describes the risk model.</summary>
    /// <param name="describer">Model describer.</param>
    /// <param name="service">Application service (for the current data window).</param>
    /// <returns>200 with the model description.</returns>
    internal static Ok<RiskModelResponse> DescribeModel(RiskModelDescriber describer, HubRiskService service) =>
        TypedResults.Ok(describer.Describe(service.Window));
}
