using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using WeatherRisk.Api.Contracts.System;
using WeatherRisk.Api.Tests.Infrastructure;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Domain;

namespace WeatherRisk.Api.Tests.Endpoints;

/// <summary>
/// End-to-end wiring of the external data path inside the real host: DI → provider → typed client →
/// metrics handler → resilience pipeline → (stubbed) network → SQLite cache → /metrics and /health.
/// </summary>
public sealed class DataSourcePipelineTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WeatherFetch_ThroughRealPipeline_IsCachedAndVisibleInMetricsAndHealth()
    {
        var network = new OpenMeteoStub();
        await using var factory = new WeatherRiskApiFactory
        {
            TestServices = services => services.ConfigureHttpClientDefaults(c => c.ConfigurePrimaryHttpMessageHandler(() => network)),
        };
        HttpClient client = factory.CreateClient();

        Hub denver;
        HubWeatherData first, second;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            denver = (await scope.ServiceProvider.GetRequiredService<IHubRepository>().GetByIdAsync("denver", Ct))!;
            first = (await scope.ServiceProvider.GetRequiredService<IHubDataProvider>().GetWeatherAsync([denver], Ct))[denver.Id];
        }

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            second = (await scope.ServiceProvider.GetRequiredService<IHubDataProvider>().GetWeatherAsync([denver], Ct))[denver.Id];
        }

        Assert.Equal(DataFreshness.Fresh, first.Freshness);
        Assert.Equal(DataFreshness.Cached, second.Freshness);
        Assert.Equal(1, network.Calls); // Second request served from SQLite.

        MetricsSnapshot? metrics = await client.GetFromJsonAsync<MetricsSnapshot>("/api/system/metrics", Ct);
        DependencyMetrics openMeteo = Assert.Single(metrics!.Dependencies, d => d.Dependency == "open-meteo");
        Assert.Equal((1L, 1L, true), (openMeteo.Calls, openMeteo.Succeeded, openMeteo.LastCallSucceeded));

        HealthResponse? health = await client.GetFromJsonAsync<HealthResponse>("/api/system/health", Ct);
        Assert.Equal("Healthy", health!.Status);
        Assert.Contains(health.Checks, c => c is { Name: "open-meteo", Status: "Healthy" });
        Assert.Contains(health.Checks, c => c is { Name: "fema-nri", Status: "Healthy" });
    }

    [Fact]
    public async Task BaselineFetch_NetworkDown_FallsBackAndReportsDegraded()
    {
        // Default factory: the network is disabled, so the call fails after the resilience retries
        // (FEMA is used here because its backoff is short; Open-Meteo's is tuned for its minute quota).
        await using var factory = new WeatherRiskApiFactory();
        HttpClient client = factory.CreateClient();

        HubBaselineData result;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            Hub denver = (await scope.ServiceProvider.GetRequiredService<IHubRepository>().GetByIdAsync("denver", Ct))!;
            result = (await scope.ServiceProvider.GetRequiredService<IHubDataProvider>().GetBaselinesAsync([denver], Ct))[denver.CountyFips];
        }

        Assert.Equal(DataFreshness.Unavailable, result.Freshness);

        MetricsSnapshot? metrics = await client.GetFromJsonAsync<MetricsSnapshot>("/api/system/metrics", Ct);
        DependencyMetrics fema = Assert.Single(metrics!.Dependencies, d => d.Dependency == "fema-nri");
        Assert.Equal((1L, 1L), (fema.Failed, fema.FallbacksServed));

        HttpResponseMessage healthResponse = await client.GetAsync("/api/system/health", Ct);
        HealthResponse? health = await healthResponse.Content.ReadFromJsonAsync<HealthResponse>(Ct);
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode); // Degraded still serves traffic.
        Assert.Equal("Degraded", health!.Status);
    }

    /// <summary>Answers Open-Meteo archive requests with one row per requested day.</summary>
    private sealed class OpenMeteoStub : HttpMessageHandler
    {
        private int _calls;

        public int Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            DateOnly from = DateOnly.Parse(query["start_date"]!);
            DateOnly to = DateOnly.Parse(query["end_date"]!);
            int days = to.DayNumber - from.DayNumber + 1;

            string Series(string value) => "[" + string.Join(',', Enumerable.Repeat(value, days)) + "]";
            string time = "[" + string.Join(',', Enumerable.Range(0, days).Select(i => $"\"{from.AddDays(i):yyyy-MM-dd}\"")) + "]";
            string json = $$$"""
                {"daily":{"time":{{{time}}},"snowfall_sum":{{{Series("1.5")}}},"precipitation_sum":{{{Series("2")}}},
                 "wind_gusts_10m_max":{{{Series("30")}}},"temperature_2m_max":{{{Series("10")}}},"temperature_2m_min":{{{Series("-2")}}}}}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
