using System.Text.Json;
using WeatherRisk.Core.Agent.Tools;
using WeatherRisk.Core.Application;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Tests.Agent;

/// <summary>Each tool against a real HubRiskService over fakes: argument parsing, results, and error mapping.</summary>
public sealed class AgentToolTests
{
    private readonly HubRiskService _service = ServiceFactory.Create(ServiceFactory.DefaultProvider());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JsonElement Json(ToolResult result) => JsonDocument.Parse(result.Json).RootElement;

    [Fact]
    public async Task RankHubs_MidwestWinter_ReturnsRankedEntriesAndWindow()
    {
        ToolResult result = await new RankHubsTool(_service).InvokeAsync("""{"region":"Midwest","hazard":"Winter"}""", Ct);

        Assert.True(result.Succeeded);
        JsonElement json = Json(result);
        Assert.Equal("Winter", json.GetProperty("rankedBy").GetString());
        Assert.Equal(["minneapolis", "chicago"], json.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("hubId").GetString()));
        Assert.Equal("2025-12-31", json.GetProperty("dataWindow").GetProperty("to").GetString());
    }

    [Fact]
    public async Task RankHubs_NoArguments_RanksByComposite()
    {
        ToolResult result = await new RankHubsTool(_service).InvokeAsync("", Ct);

        Assert.Equal("Composite", Json(result).GetProperty("rankedBy").GetString());
    }

    [Theory]
    [InlineData("""{"region":"Atlantis"}""")]
    [InlineData("""{"hazard":"Winter","unexpected":true}""")]
    [InlineData("""{"top":0}""")]
    public async Task RankHubs_InvalidArguments_ReturnErrorForTheModel(string args)
    {
        ToolResult result = await new RankHubsTool(_service).InvokeAsync(args, Ct);

        Assert.False(result.Succeeded);
        Assert.True(Json(result).TryGetProperty("error", out _));
    }

    [Fact]
    public async Task GetHubRisk_ReturnsBreakdownWithEvidence()
    {
        ToolResult result = await new GetHubRiskTool(_service).InvokeAsync("""{"hubId":"Minneapolis"}""", Ct);

        JsonElement json = Json(result);
        Assert.Equal("minneapolis", json.GetProperty("hubId").GetString());
        Assert.Equal(5, json.GetProperty("hazards").GetArrayLength());
        Assert.True(json.GetProperty("hazards")[0].GetProperty("evidence").GetArrayLength() > 0);
        Assert.Equal("Fresh", json.GetProperty("weatherFreshness").GetString());
    }

    [Fact]
    public async Task GetHubRisk_UnknownHub_ReturnsErrorPointingToListHubs()
    {
        ToolResult result = await new GetHubRiskTool(_service).InvokeAsync("""{"hubId":"gotham"}""", Ct);

        Assert.False(result.Succeeded);
        Assert.Contains("list_hubs", Json(result).GetProperty("error").GetString());
    }

    [Fact]
    public async Task CompareHubs_IncludesRowsAndEvidenceForComparedHazardsOnly()
    {
        ToolResult result = await new CompareHubsTool(_service).InvokeAsync("""{"hubIds":["miami","chicago"],"hazards":["Winter"]}""", Ct);

        JsonElement json = Json(result);
        Assert.Equal(["Composite", "Winter"], json.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("metric").GetString()));
        JsonElement evidence = json.GetProperty("hubs")[0].GetProperty("evidence");
        Assert.Equal("Winter", evidence.EnumerateArray().Single().GetProperty("hazard").GetString());
    }

    [Fact]
    public async Task GetWeatherStatistics_Defaults_ToLastFullYear()
    {
        ToolResult result = await new GetWeatherStatisticsTool(_service).InvokeAsync("""{"hubId":"minneapolis"}""", Ct);

        JsonElement json = Json(result);
        Assert.Equal("2025-01-01", json.GetProperty("from").GetString());
        JsonElement snow = json.GetProperty("disruptionDays").EnumerateArray().First(d => d.GetProperty("metric").GetString() == "snowDays");
        Assert.Equal(20.0, snow.GetProperty("percentOfDays").GetDouble());
    }

    [Fact]
    public async Task GetWeatherStatistics_PeriodOutsideWindow_ReturnsError()
    {
        ToolResult result = await new GetWeatherStatisticsTool(_service).InvokeAsync("""{"hubId":"minneapolis","from":"2010-01-01","to":"2010-12-31"}""", Ct);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ListHubs_RegionFilter_ReturnsIds()
    {
        ToolResult result = await new ListHubsTool(_service).InvokeAsync("""{"region":"South"}""", Ct);

        Assert.Equal(["miami"], Json(result).GetProperty("hubs").EnumerateArray().Select(h => h.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task DescribeRiskModel_ReturnsHazardsAndAssumptions()
    {
        var tool = new DescribeRiskModelTool(new RiskModelDescriber(new ScoringSettings(), new WeatherThresholds()), _service);

        JsonElement json = Json(await tool.InvokeAsync("{}", Ct));

        Assert.Equal(5, json.GetProperty("hazards").GetArrayLength());
        Assert.True(json.GetProperty("assumptions").GetArrayLength() > 0);
    }

    [Fact]
    public void Definitions_HaveSnakeCaseNamesAndObjectSchemas()
    {
        IAgentTool[] tools =
        [
            new ListHubsTool(_service), new RankHubsTool(_service), new CompareHubsTool(_service),
            new GetHubRiskTool(_service), new GetWeatherStatisticsTool(_service),
            new DescribeRiskModelTool(new RiskModelDescriber(new ScoringSettings(), new WeatherThresholds()), _service),
        ];

        Assert.All(tools, t =>
        {
            Assert.Matches("^[a-z_]+$", t.Definition.Name);
            Assert.Equal("object", t.Definition.ParametersSchema.GetProperty("type").GetString());
            Assert.False(string.IsNullOrWhiteSpace(t.Definition.Description));
        });
        JsonElement compareRequired = tools[2].Definition.ParametersSchema.GetProperty("required");
        Assert.Equal(["hubIds"], compareRequired.EnumerateArray().Select(e => e.GetString()));
    }
}
