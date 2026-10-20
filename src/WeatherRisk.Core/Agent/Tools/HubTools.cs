using System.ComponentModel;
using WeatherRisk.Core.Application;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Ranking;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Agent.Tools;

/// <summary>Lists hubs (optionally one region) — used to resolve names to ids.</summary>
public sealed class ListHubsTool : AgentTool<ListHubsTool.Args>
{
    private readonly HubRiskService _service;

    /// <summary>Creates the tool.</summary>
    /// <param name="service">Application service.</param>
    public ListHubsTool(HubRiskService service)
        : base("list_hubs", "List distribution hubs with ids, cities and regions. Use to resolve a city name to a hub id.") =>
        _service = service;

    /// <summary>Arguments.</summary>
    /// <param name="Region">Optional region filter.</param>
    public sealed record Args([property: Description("Optional US Census region filter.")] Region? Region = null);

    /// <inheritdoc />
    protected override async Task<object> ExecuteAsync(Args args, CancellationToken cancellationToken) =>
        new
        {
            hubs = (await _service.GetHubsAsync(args.Region, cancellationToken))
                .Select(h => new ToolResults.HubItem(h.Id, h.Name, h.City, h.State, h.Region)),
        };
}

/// <summary>Ranks hubs by composite or one hazard.</summary>
public sealed class RankHubsTool : AgentTool<RankHubsTool.Args>
{
    private readonly HubRiskService _service;

    /// <summary>Creates the tool.</summary>
    /// <param name="service">Application service.</param>
    public RankHubsTool(HubRiskService service)
        : base("rank_hubs",
            "Rank hubs by weather-disruption exposure: overall (composite) or for one hazard, optionally within a region and top N. " +
            "Use for 'which hubs are most exposed / should be prioritized' questions.") =>
        _service = service;

    /// <summary>Arguments.</summary>
    /// <param name="Region">Optional region filter.</param>
    /// <param name="Hazard">Optional hazard to rank by; omit for the composite score.</param>
    /// <param name="Top">Optional number of hubs to return.</param>
    public sealed record Args(
        [property: Description("Optional US Census region filter.")] Region? Region = null,
        [property: Description("Rank by this hazard's score; omit to rank by the composite score.")] HazardType? Hazard = null,
        [property: Description("Return only the top N hubs (1-15).")] int? Top = null);

    /// <inheritdoc />
    protected override async Task<object> ExecuteAsync(Args args, CancellationToken cancellationToken)
    {
        RankingResult result = await _service.RankAsync(new RankingCriteria(args.Region, args.Hazard, args.Top), cancellationToken);
        return new
        {
            rankedBy = args.Hazard?.ToString() ?? HubRanker.CompositeMetric,
            region = args.Region,
            entries = result.Entries.Select(e => new ToolResults.RankItem(
                e.Rank, e.Details.Hub.Id, e.Details.Hub.Name, e.Details.Hub.Region, e.Score, e.Rating,
                e.Details.CompositeScore, e.Details.TopDrivers, e.Details.Confidence)),
            dataWindow = _service.Window,
        };
    }
}

/// <summary>Compares hubs side by side.</summary>
public sealed class CompareHubsTool : AgentTool<CompareHubsTool.Args>
{
    private readonly HubRiskService _service;

    /// <summary>Creates the tool.</summary>
    /// <param name="service">Application service.</param>
    public CompareHubsTool(HubRiskService service)
        : base("compare_hubs",
            "Compare 2-15 hubs side by side on the composite and per-hazard scores, with the most exposed hub and the spread per metric.") =>
        _service = service;

    /// <summary>Arguments.</summary>
    /// <param name="HubIds">Hub ids to compare.</param>
    /// <param name="Hazards">Hazards to include; omit for all.</param>
    public sealed record Args(
        [property: Description("Hub ids to compare, e.g. ['miami','houston'].")] IReadOnlyList<string> HubIds,
        [property: Description("Hazards to include; omit for all hazards.")] IReadOnlyList<HazardType>? Hazards = null);

    /// <inheritdoc />
    protected override async Task<object> ExecuteAsync(Args args, CancellationToken cancellationToken)
    {
        ComparisonResult result = await _service.CompareAsync(args.HubIds, args.Hazards, cancellationToken);
        HashSet<HazardType> compared = result.Comparison.Rows.Where(r => r.Hazard is not null).Select(r => r.Hazard!.Value).ToHashSet();
        return new
        {
            rows = result.Comparison.Rows.Select(r => new
            {
                metric = r.Metric,
                values = r.Values.Select(v => new { hubId = v.HubId, score = v.Score, rating = v.Rating }),
                mostExposedHubId = r.MostExposedHubId,
                spread = r.Spread,
            }),
            hubs = result.Comparison.Details.Select(d => new
            {
                hubId = d.Hub.Id,
                compositeScore = d.CompositeScore,
                rating = d.Rating,
                confidence = d.Confidence,
                topDrivers = d.TopDrivers,
                notes = d.Notes,

                // Evidence for the compared hazards only (no descriptions) so explanations cite real causes
                // without the token cost of full breakdowns.
                evidence = d.Hazards.Where(h => compared.Contains(h.Detail.Hazard)).Select(h => new
                {
                    hazard = h.Detail.Hazard,
                    observedScore = h.Detail.ObservedScore,
                    baselineScore = h.Detail.BaselineScore,
                    factors = h.Detail.Factors.Select(f => new { name = f.Name, value = f.Value, unit = f.Unit }),
                }),
            }),
        };
    }
}

/// <summary>Full explainable assessment of one hub.</summary>
public sealed class GetHubRiskTool : AgentTool<GetHubRiskTool.Args>
{
    private readonly HubRiskService _service;

    /// <summary>Creates the tool.</summary>
    /// <param name="service">Application service.</param>
    public GetHubRiskTool(HubRiskService service)
        : base("get_hub_risk",
            "Full risk breakdown of one hub: composite score, rating, confidence, top drivers, per-hazard scores with observed " +
            "and FEMA baseline parts, contributions and raw evidence, data freshness. Use for 'why' questions.") =>
        _service = service;

    /// <summary>Arguments.</summary>
    /// <param name="HubId">Hub id.</param>
    public sealed record Args([property: Description("Hub id, e.g. 'dallas'.")] string HubId);

    /// <inheritdoc />
    protected override async Task<object> ExecuteAsync(Args args, CancellationToken cancellationToken) =>
        ToolResults.ToItem(await _service.AssessAsync(args.HubId, cancellationToken));
}

/// <summary>Observed weather statistics for a hub and period.</summary>
public sealed class GetWeatherStatisticsTool : AgentTool<GetWeatherStatisticsTool.Args>
{
    private readonly HubRiskService _service;

    /// <summary>Creates the tool.</summary>
    /// <param name="service">Application service.</param>
    public GetWeatherStatisticsTool(HubRiskService service)
        : base("get_weather_statistics",
            "Observed weather for one hub over a period: days with snow, hard freeze, heavy rain, damaging gusts, tropical-storm " +
            "gusts and extreme heat (count, % of days, per year), plus totals and extremes. Defaults to the last full calendar year.") =>
        _service = service;

    /// <summary>Arguments.</summary>
    /// <param name="HubId">Hub id.</param>
    /// <param name="From">First day (yyyy-MM-dd).</param>
    /// <param name="To">Last day (yyyy-MM-dd).</param>
    public sealed record Args(
        [property: Description("Hub id, e.g. 'denver'.")] string HubId,
        [property: Description("First day, yyyy-MM-dd. Omit for 1 Jan of the last full year.")] DateOnly? From = null,
        [property: Description("Last day, yyyy-MM-dd. Omit for 31 Dec of the last full year.")] DateOnly? To = null);

    /// <inheritdoc />
    protected override async Task<object> ExecuteAsync(Args args, CancellationToken cancellationToken)
    {
        WeatherStatisticsResult r = await _service.GetWeatherStatisticsAsync(args.HubId, args.From, args.To, cancellationToken);
        WeatherSummary s = r.Summary;
        WeatherThresholds t = r.Thresholds;
        object Metric(string name, string definition, int days) => new
        {
            metric = name,
            definition,
            days,
            percentOfDays = Math.Round(s.PercentOfDays(days), 2),
            perYear = Math.Round(s.PerYear(days), 2),
        };

        return new
        {
            hubId = r.Hub.Id,
            from = s.From,
            to = s.To,
            daysObserved = s.DaysObserved,
            coverage = Math.Round(s.Coverage, 4),
            disruptionDays = new[]
            {
                Metric("snowDays", $"snowfall >= {t.SnowDayCm} cm", s.SnowDays),
                Metric("hardFreezeDays", $"min temperature <= {t.HardFreezeC} C", s.HardFreezeDays),
                Metric("heavyRainDays", $"precipitation >= {t.HeavyRainMm} mm", s.HeavyRainDays),
                Metric("damagingGustDays", $"gust >= {t.DamagingGustKmh} km/h", s.DamagingGustDays),
                Metric("tropicalStormGustDays", $"gust >= {t.TropicalStormGustKmh} km/h", s.TropicalStormGustDays),
                Metric("extremeHeatDays", $"max temperature >= {t.ExtremeHeatC} C", s.ExtremeHeatDays),
            },
            totalSnowfallCm = Math.Round(s.TotalSnowfallCm, 1),
            totalPrecipitationMm = Math.Round(s.TotalPrecipitationMm, 1),
            maxWindGustKmh = s.MaxWindGustKmh,
            maxTemperatureC = s.MaxTemperatureC,
            minTemperatureC = s.MinTemperatureC,
            freshness = r.Freshness,
        };
    }
}

/// <summary>Describes how the risk model works.</summary>
public sealed class DescribeRiskModelTool : AgentTool<DescribeRiskModelTool.Args>
{
    private readonly RiskModelDescriber _describer;
    private readonly HubRiskService _service;

    /// <summary>Creates the tool.</summary>
    /// <param name="describer">Model describer.</param>
    /// <param name="service">Application service (current data window).</param>
    public DescribeRiskModelTool(RiskModelDescriber describer, HubRiskService service)
        : base("describe_risk_model",
            "How scores are computed: signals, thresholds, weights, rating bands, data sources and assumptions. Use for methodology questions.")
    {
        _describer = describer;
        _service = service;
    }

    /// <summary>No arguments.</summary>
    public sealed record Args;

    /// <inheritdoc />
    protected override Task<object> ExecuteAsync(Args args, CancellationToken cancellationToken) =>
        Task.FromResult<object>(_describer.Describe(_service.Window));
}
