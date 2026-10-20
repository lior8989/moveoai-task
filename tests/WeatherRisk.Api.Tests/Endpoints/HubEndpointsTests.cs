using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using WeatherRisk.Api.Contracts.Hubs;
using WeatherRisk.Api.Contracts.Risk;
using WeatherRisk.Api.Tests.Infrastructure;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring;

namespace WeatherRisk.Api.Tests.Endpoints;

/// <summary>Full-pipeline tests for /api/hubs/* with deterministic stub data.</summary>
public sealed class HubEndpointsTests : IClassFixture<StubDataApiFactory>
{
    private readonly HttpClient _client;

    public HubEndpointsTests(StubDataApiFactory fixture) => _client = fixture.Factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<T> GetAsync<T>(string url)
    {
        HttpResponseMessage response = await _client.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options, Ct))!;
    }

    private async Task<ProblemDetails> GetProblemAsync(string url, HttpStatusCode expected)
    {
        HttpResponseMessage response = await _client.GetAsync(url, Ct);
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!;
    }

    // ---------- /api/hubs ----------

    [Fact]
    public async Task ListHubs_ReturnsAllFifteenOrderedByName()
    {
        List<HubResponse> hubs = await GetAsync<List<HubResponse>>("/api/hubs");

        Assert.Equal(15, hubs.Count);
        Assert.Equal(hubs.OrderBy(h => h.Name, StringComparer.Ordinal).Select(h => h.Id), hubs.Select(h => h.Id));
    }

    [Fact]
    public async Task ListHubs_RegionFilter_ReturnsOnlyThatRegion()
    {
        List<HubResponse> hubs = await GetAsync<List<HubResponse>>("/api/hubs?region=Midwest");

        Assert.Equal(5, hubs.Count);
        Assert.All(hubs, h => Assert.Equal(Region.Midwest, h.Region));
    }

    [Fact]
    public async Task ListHubs_UnknownRegion_Returns400Problem()
    {
        await GetProblemAsync("/api/hubs?region=Atlantis", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHub_IdIsCaseInsensitive()
    {
        HubResponse hub = await GetAsync<HubResponse>("/api/hubs/DALLAS");

        Assert.Equal(("dallas", "TX", "48113"), (hub.Id, hub.State, hub.CountyFips));
    }

    [Fact]
    public async Task GetHub_Unknown_Returns404ProblemListingTheId()
    {
        ProblemDetails problem = await GetProblemAsync("/api/hubs/atlantis", HttpStatusCode.NotFound);

        Assert.Equal("Hub not found", problem.Title);
        Assert.Contains("atlantis", problem.Extensions["unknownHubs"]!.ToString());
    }

    // ---------- /api/hubs/{id}/risk ----------

    [Fact]
    public async Task GetRisk_ReturnsExplainableBreakdownWhoseContributionsSumToComposite()
    {
        HubRiskResponse risk = await GetAsync<HubRiskResponse>("/api/hubs/dallas/risk");

        Assert.Equal("dallas", risk.Hub.Id);
        Assert.Equal(Enum.GetValues<HazardType>(), risk.Hazards.Select(h => h.Hazard));
        Assert.Equal(risk.CompositeScore, risk.Hazards.Sum(h => h.Contribution), precision: 1);
        Assert.Single(risk.Hazards, h => h.IsWorstHazard);
        Assert.NotEmpty(risk.TopDrivers);
        Assert.All(risk.Hazards, h => Assert.NotEmpty(h.Evidence));
        Assert.Equal(ScoreConfidence.High, risk.Confidence);
        Assert.Equal(DataFreshness.Cached, risk.Data.WeatherFreshness);
        Assert.Equal(StubHubDataProvider.FixedWindow.From, risk.Data.WindowFrom);
    }

    [Fact]
    public async Task GetRisk_SerializesEnumsAsNames()
    {
        string json = await _client.GetStringAsync("/api/hubs/dallas/risk", Ct);

        Assert.Contains("\"hazard\":\"Winter\"", json);
        Assert.Matches("\"rating\":\"(Low|Moderate|High|VeryHigh)\"", json);
        Assert.Contains("\"weatherFreshness\":\"Cached\"", json);
    }

    [Fact]
    public async Task GetRisk_HurricaneNotApplicableInland_ScoresZeroWithReason()
    {
        HubRiskResponse risk = await GetAsync<HubRiskResponse>("/api/hubs/denver/risk");

        HazardBreakdownResponse hurricane = risk.Hazards.Single(h => h.Hazard == HazardType.Hurricane);
        Assert.Equal(0, hurricane.Score);
        Assert.Contains(hurricane.Evidence, e => e.Name == "notApplicable");
    }

    [Fact]
    public async Task GetRisk_UnknownHub_Returns404()
    {
        await GetProblemAsync("/api/hubs/atlantis/risk", HttpStatusCode.NotFound);
    }

    // ---------- /api/hubs/{id}/weather ----------

    [Fact]
    public async Task GetWeather_NoPeriod_ReturnsLastFullYearWithSnowPercentage()
    {
        // "What percentage of days in Denver last year had snowfall?" — stub: snow every 15th day of the year.
        WeatherStatisticsResponse stats = await GetAsync<WeatherStatisticsResponse>("/api/hubs/denver/weather");

        Assert.Equal((new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)), (stats.From, stats.To));
        Assert.Equal(365, stats.DaysObserved);
        DisruptionDaysResponse snow = stats.DisruptionDays.Single(d => d.Metric == "snowDays");
        Assert.Equal(24, snow.Days);
        Assert.Equal(6.58, snow.PercentOfDays);
        Assert.Equal("snowfall ≥ 1 cm", snow.Definition);
        Assert.Equal(6, stats.DisruptionDays.Count);
    }

    [Fact]
    public async Task GetWeather_CustomPeriod_IsUsed()
    {
        WeatherStatisticsResponse stats = await GetAsync<WeatherStatisticsResponse>(
            "/api/hubs/denver/weather?from=2024-01-01&to=2024-01-31");

        Assert.Equal(31, stats.DaysInPeriod);
        Assert.Equal(1.0, stats.Coverage);
    }

    [Fact]
    public async Task GetWeather_PeriodOutsideWindow_Returns400ExplainingTheWindow()
    {
        ProblemDetails problem = await GetProblemAsync(
            "/api/hubs/denver/weather?from=2010-01-01&to=2010-12-31", HttpStatusCode.BadRequest);

        Assert.Equal("Invalid request", problem.Title);
        Assert.Contains("2021-01-01", problem.Detail);
    }

    [Fact]
    public async Task GetWeather_MalformedDate_Returns400()
    {
        await GetProblemAsync("/api/hubs/denver/weather?from=yesterday", HttpStatusCode.BadRequest);
    }
}
