using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using WeatherRisk.Api.Contracts.Risk;
using WeatherRisk.Api.Contracts.System;
using WeatherRisk.Api.Tests.Infrastructure;
using WeatherRisk.Core.Domain;

namespace WeatherRisk.Api.Tests.Endpoints;

/// <summary>Full-pipeline tests for /api/risk/* with deterministic stub data.</summary>
public sealed class RiskEndpointsTests : IClassFixture<StubDataApiFactory>
{
    private readonly HttpClient _client;

    public RiskEndpointsTests(StubDataApiFactory fixture) => _client = fixture.Factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        HttpResponseMessage response = await client.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options, Ct))!;
    }

    private Task<T> GetAsync<T>(string url) => GetAsync<T>(_client, url);

    // ---------- Rankings ----------

    [Fact]
    public async Task Rankings_MidwestWinter_MostSnowFirst()
    {
        // "Which hubs in the Midwest are most exposed to winter disruption?" — stub snow increases with latitude.
        RankingResponse ranking = await GetAsync<RankingResponse>("/api/risk/rankings?region=Midwest&hazard=Winter");

        Assert.Equal("Winter", ranking.RankedBy);
        Assert.Equal(Region.Midwest, ranking.Region);
        Assert.Equal(5, ranking.Entries.Count);
        Assert.Equal("minneapolis", ranking.Entries[0].HubId);
        Assert.Equal([1, 2, 3, 4, 5], ranking.Entries.Select(e => e.Rank));
        Assert.True(ranking.Entries.Zip(ranking.Entries.Skip(1)).All(p => p.First.Score >= p.Second.Score));
    }

    [Fact]
    public async Task Rankings_DefaultIsCompositeAcrossAllHubs_TopLimits()
    {
        RankingResponse ranking = await GetAsync<RankingResponse>("/api/risk/rankings?top=3");

        Assert.Equal("Composite", ranking.RankedBy);
        Assert.Null(ranking.Region);
        Assert.Equal(3, ranking.Entries.Count);
        Assert.All(ranking.Entries, e => Assert.Equal(e.CompositeScore, e.Score));
    }

    [Theory]
    [InlineData("/api/risk/rankings?hazard=Tsunami")]
    [InlineData("/api/risk/rankings?top=0")]
    [InlineData("/api/risk/rankings?top=abc")]
    public async Task Rankings_InvalidParameters_Return400Problem(string url)
    {
        HttpResponseMessage response = await _client.GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // ---------- Compare ----------

    [Fact]
    public async Task Compare_MiamiHoustonHurricaneFlood_ReturnsRequestedRowsAndDetails()
    {
        // "Compare Miami and Houston in terms of hurricane and flood exposure."
        ComparisonResponse comparison = await GetAsync<ComparisonResponse>(
            "/api/risk/compare?hubs=miami,houston&hazards=Hurricane&hazards=Flood");

        Assert.Equal(["miami", "houston"], comparison.HubIds);
        Assert.Equal(["Composite", "Hurricane", "Flood"], comparison.Rows.Select(r => r.Metric));
        Assert.All(comparison.Rows, r => Assert.Equal(2, r.Values.Count));
        Assert.Equal(["miami", "houston"], comparison.Hubs.Select(h => h.Hub.Id));
    }

    [Fact]
    public async Task Compare_RepeatedAndCommaSeparatedHubs_AreEquivalent()
    {
        ComparisonResponse comma = await GetAsync<ComparisonResponse>("/api/risk/compare?hubs=miami,houston");
        ComparisonResponse repeated = await GetAsync<ComparisonResponse>("/api/risk/compare?hubs=miami&hubs=houston");

        Assert.Equal(comma.HubIds, repeated.HubIds);
        Assert.Equal(6, comma.Rows.Count); // Composite + 5 hazards.
    }

    [Fact]
    public async Task Compare_UnknownHub_Returns404ListingIt()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/risk/compare?hubs=miami,gotham", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Contains("gotham", problem!.Detail);
    }

    [Theory]
    [InlineData("/api/risk/compare?hubs=miami")]
    [InlineData("/api/risk/compare?hubs=miami,Miami")]
    [InlineData("/api/risk/compare")]
    public async Task Compare_FewerThanTwoDistinctHubs_Returns400(string url)
    {
        HttpResponseMessage response = await _client.GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Model ----------

    [Fact]
    public async Task Model_DescribesAllHazardsWithNormalizedWeightsAndBands()
    {
        RiskModelResponse model = await GetAsync<RiskModelResponse>("/api/risk/model");

        Assert.Equal(Enum.GetValues<HazardType>(), model.Hazards.Select(h => h.Hazard));
        Assert.Equal(1.0, model.Hazards.Sum(h => h.CompositeWeight), precision: 2);
        Assert.Equal(0, model.Hazards.Single(h => h.Hazard == HazardType.Hurricane).ObservedWeight);
        Assert.Equal(4, model.RatingBands.Count);
        Assert.Equal(0.5, model.WorstHazardBlend);
        Assert.NotEmpty(model.Assumptions);
        Assert.Equal(StubHubDataProvider.FixedWindow.To, model.WindowTo);
    }

    [Fact]
    public async Task Model_ScoringConfigurationOverride_IsApplied()
    {
        await using var factory = StubDataApiFactory.Create(new Dictionary<string, string?>
        {
            ["Scoring:WorstHazardBlend"] = "0.25",
            ["Scoring:Heat:CompositeWeight"] = "0.5",
            ["WeatherThresholds:SnowDayCm"] = "2.5",
        });

        RiskModelResponse model = await GetAsync<RiskModelResponse>(factory.CreateClient(), "/api/risk/model");

        Assert.Equal(0.25, model.WorstHazardBlend);
        Assert.Contains("snowfall ≥ 2.5 cm", model.Hazards[0].ObservedSignal);
        Assert.True(model.Hazards.Single(h => h.Hazard == HazardType.Heat).CompositeWeight > 0.3);
    }

    [Fact]
    public async Task InvalidScoringConfiguration_StopsStartupWithClearMessage()
    {
        await using var factory = StubDataApiFactory.Create(new Dictionary<string, string?>
        {
            ["Scoring:WorstHazardBlend"] = "1.7",
        });

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("WorstHazardBlend", ex.ToString());
    }

    // ---------- Cross-cutting ----------

    [Fact]
    public async Task DomainNotFound_IsRecordedInMetricsAs404UnderItsRoute()
    {
        await using var factory = StubDataApiFactory.Create();
        HttpClient client = factory.CreateClient();
        await client.GetAsync("/api/hubs/atlantis/risk", Ct);

        MetricsSnapshot? metrics = await client.GetFromJsonAsync<MetricsSnapshot>("/api/system/metrics", Ct);

        EndpointMetrics risk = Assert.Single(metrics!.Endpoints, e => e.Endpoint == "GET /api/hubs/{id}/risk");
        Assert.Equal((1L, 0L), (risk.ClientErrors, risk.ServerErrors));
    }

    [Fact]
    public async Task OpenApi_DocumentsHubAndRiskEndpointsWithEnumValues()
    {
        string document = await _client.GetStringAsync("/openapi/v1.json", Ct);

        foreach (string path in new[] { "/api/hubs", "/api/hubs/{id}/risk", "/api/hubs/{id}/weather", "/api/risk/rankings", "/api/risk/compare", "/api/risk/model" })
        {
            Assert.Contains($"\"{path}\"", document);
        }

        Assert.Contains("\"SevereStorm\"", document);
        Assert.Contains("\"Midwest\"", document);
    }
}
