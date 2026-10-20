using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using WeatherRisk.Api.Contracts.System;
using WeatherRisk.Api.Tests.Infrastructure;

namespace WeatherRisk.Api.Tests.RateLimiting;

/// <summary>Full-pipeline rate-limiting tests with a deliberately tiny limit (2 requests per minute).</summary>
public sealed class RateLimitingTests : IAsyncLifetime
{
    // No business endpoints exist yet; unmatched /api paths still pass through the limiter.
    private const string LimitedPath = "/api/anything";

    private readonly WeatherRiskApiFactory _factory = new()
    {
        Settings = new Dictionary<string, string?>
        {
            ["RateLimiting:Global:PermitLimit"] = "2",
            ["RateLimiting:Global:WindowSeconds"] = "60",
            ["RateLimiting:Global:SegmentsPerWindow"] = "1",
        },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task ExceedingLimit_Returns429WithRetryAfterAndProblemDetails()
    {
        HttpClient client = _factory.CreateClient();
        await client.GetAsync(LimitedPath, Ct);
        await client.GetAsync(LimitedPath, Ct);

        HttpResponseMessage rejected = await client.GetAsync(LimitedPath, Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta >= TimeSpan.FromSeconds(1));
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        ProblemDetails? problem = await rejected.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal(429, problem?.Status);
        Assert.Equal("Too many requests", problem?.Title);
    }

    [Fact]
    public async Task SystemEndpoints_AreExemptFromRateLimiting()
    {
        HttpClient client = _factory.CreateClient();

        for (int i = 0; i < 5; i++)
        {
            HttpResponseMessage response = await client.GetAsync("/api/system/health", Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task RejectedRequests_AreCountedInMetrics()
    {
        HttpClient client = _factory.CreateClient();
        for (int i = 0; i < 3; i++)
        {
            await client.GetAsync(LimitedPath, Ct);
        }

        MetricsSnapshot? snapshot = await client.GetFromJsonAsync<MetricsSnapshot>("/api/system/metrics", Ct);

        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.Overall.RateLimited);
        Assert.Equal(2, snapshot.Overall.ClientErrors); // The two allowed requests hit 404.
    }
}
