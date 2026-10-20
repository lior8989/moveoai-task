using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using WeatherRisk.Api.Contracts.System;
using WeatherRisk.Api.Observability;
using WeatherRisk.Api.Tests.Infrastructure;

namespace WeatherRisk.Api.Tests.Endpoints;

/// <summary>Full-pipeline tests for /api/system/*, Swagger/OpenAPI, and error handling.</summary>
public sealed class SystemEndpointsTests : IClassFixture<WeatherRiskApiFactory>
{
    private readonly WeatherRiskApiFactory _factory;

    public SystemEndpointsTests(WeatherRiskApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetHealth_DatabaseReachable_Returns200HealthyWithDatabaseCheck()
    {
        HttpResponseMessage response = await _factory.CreateClient().GetAsync("/api/system/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        HealthResponse? body = await response.Content.ReadFromJsonAsync<HealthResponse>(Ct);
        Assert.NotNull(body);
        Assert.Equal("Healthy", body.Status);
        HealthCheckEntry database = Assert.Single(body.Checks, c => c.Name == "database");
        Assert.Equal("Healthy", database.Status);
    }

    [Fact]
    public async Task GetSchema_DescribesAllTablesWithColumnsKeysAndComments()
    {
        DatabaseSchemaResponse? schema = await _factory.CreateClient()
            .GetFromJsonAsync<DatabaseSchemaResponse>("/api/system/schema", Ct);

        Assert.NotNull(schema);
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", schema.Provider);
        Assert.Equal(
            ["DailyWeather", "HazardBaselines", "Hubs", "RiskScoreSnapshots", "WeatherSync"],
            schema.Tables.Select(t => t.Name));

        TableSchema daily = schema.Tables.Single(t => t.Name == "DailyWeather");
        Assert.False(string.IsNullOrWhiteSpace(daily.Comment));
        Assert.Equal(["HubId", "Date"], daily.Columns.Where(c => c.PrimaryKey).Select(c => c.Name));
        ColumnSchema snow = daily.Columns.Single(c => c.Name == "SnowfallCm");
        Assert.True(snow.Nullable);
        Assert.Equal("REAL", snow.Type);
        Assert.Contains("cm", snow.Comment);
        ForeignKeySchema fk = Assert.Single(daily.ForeignKeys);
        Assert.Equal("Hubs", fk.PrincipalTable);
        Assert.Equal("Cascade", fk.OnDelete);

        Assert.Contains(schema.Tables.Single(t => t.Name == "RiskScoreSnapshots").Indexes,
            i => i.Columns.SequenceEqual(["HubId", "Id"]));
    }

    [Fact]
    public async Task GetMetrics_AfterRequests_ReflectsCountsPerEndpoint()
    {
        // Fresh host so other tests' traffic doesn't affect the counts.
        await using var factory = new WeatherRiskApiFactory();
        HttpClient client = factory.CreateClient();
        await client.GetAsync("/api/system/health", Ct);
        await client.GetAsync("/api/system/health", Ct);
        await client.GetAsync("/api/does-not-exist", Ct);

        HttpResponseMessage response = await client.GetAsync("/api/system/metrics", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        MetricsSnapshot? snapshot = await response.Content.ReadFromJsonAsync<MetricsSnapshot>(Ct);
        Assert.NotNull(snapshot);

        // The metrics request itself is recorded after its response is built, so it isn't in its own snapshot.
        Assert.Equal(3, snapshot.Overall.Total);
        EndpointMetrics health = Assert.Single(snapshot.Endpoints, e => e.Endpoint == "GET /api/system/health");
        Assert.Equal(2, health.Succeeded);
        Assert.Equal(2, health.RequestsInWindow);
        EndpointMetrics unmatched = Assert.Single(
            snapshot.Endpoints, e => e.Endpoint == RequestMetricsCollector.UnmatchedEndpoint);
        Assert.Equal(1, unmatched.ClientErrors);
    }

    [Fact]
    public async Task GetMetrics_NonApiPaths_AreNotRecorded()
    {
        await using var factory = new WeatherRiskApiFactory();
        HttpClient client = factory.CreateClient();
        await client.GetAsync("/swagger/index.html", Ct);
        await client.GetAsync("/openapi/v1.json", Ct);

        MetricsSnapshot? snapshot = await client.GetFromJsonAsync<MetricsSnapshot>("/api/system/metrics", Ct);

        Assert.NotNull(snapshot);
        Assert.Equal(0, snapshot.Overall.Total);
    }

    [Fact]
    public async Task UnknownApiRoute_Returns404ProblemDetails()
    {
        HttpResponseMessage response = await _factory.CreateClient().GetAsync("/api/nope", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal(404, problem?.Status);
    }

    [Fact]
    public async Task OpenApiDocument_DescribesSystemEndpoints()
    {
        string document = await _factory.CreateClient().GetStringAsync("/openapi/v1.json", Ct);

        Assert.Contains("/api/system/health", document);
        Assert.Contains("/api/system/metrics", document);
        Assert.Contains("/api/system/schema", document);
        Assert.Contains("Health of the API and its dependencies", document);
        Assert.Contains("\"503\"", document);
    }

    [Fact]
    public async Task SwaggerUi_IsServed()
    {
        HttpResponseMessage response = await _factory.CreateClient().GetAsync("/swagger/index.html", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Root_ServesChatUiDirectly()
    {
        HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        HttpResponseMessage response = await client.GetAsync("/", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
