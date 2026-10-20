using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Data.External;
using WeatherRisk.Data.External.Fema;
using WeatherRisk.Data.External.OpenMeteo;

namespace WeatherRisk.Data;

/// <summary>Registers the external data sources (resilient typed HTTP clients) and the cache-first provider.</summary>
public static class DataSourceServiceCollectionExtensions
{
    /// <summary>
    /// Adds Open-Meteo and FEMA NRI clients with timeout, jittered retry and circuit breaker, plus
    /// <see cref="IHubDataProvider"/>. Requires <see cref="IDependencyMetrics"/> and <see cref="TimeProvider"/>
    /// to be registered by the host.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Section with <see cref="HubDataSettings"/> values (<c>HubData</c>).</param>
    /// <param name="configureClient">
    /// Optional hook per client, called with the dependency name before resilience is added — the host uses it
    /// to add a metrics handler that measures each logical call (including retries).
    /// </param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddWeatherRiskDataSources(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<string, IHttpClientBuilder>? configureClient = null)
    {
        services.AddOptions<HubDataSettings>()
            .Bind(configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Open-Meteo enforces a per-minute quota in which a multi-year request counts as many calls, so a cold
        // warm-up of all hubs can hit 429. Longer backoff (5→10→20→40 s, honouring Retry-After) lets retries
        // cross into the next quota minute; only cold-cache fetches ever wait this long.
        IHttpClientBuilder openMeteo = services.AddHttpClient<IWeatherDataSource, OpenMeteoClient>(
            client => client.BaseAddress = OpenMeteoClient.BaseAddress);
        configureClient?.Invoke(OpenMeteoClient.DependencyName, openMeteo);
        openMeteo.AddStandardResilienceHandler(options =>
            Configure(options, attemptSeconds: 15, totalSeconds: 150, retries: 4, firstRetryDelaySeconds: 5));

        // FEMA pages are larger (2,000 rows each), so it gets more time per attempt.
        IHttpClientBuilder fema = services.AddHttpClient<IHazardBaselineSource, FemaNriClient>(
            client => client.BaseAddress = FemaNriClient.BaseAddress);
        configureClient?.Invoke(FemaNriClient.DependencyName, fema);
        fema.AddStandardResilienceHandler(options =>
            Configure(options, attemptSeconds: 30, totalSeconds: 90, retries: 2, firstRetryDelaySeconds: 2));

        services.AddScoped<IHubDataProvider, HubDataProvider>();
        return services;
    }

    /// <summary>
    /// Standard pipeline (rate limiter → total timeout → retry → circuit breaker → attempt timeout) with
    /// timeouts suited to batch-style historical queries. Retries use exponential backoff with jitter on
    /// transient failures (5xx, 408, 429, network errors) and honour <c>Retry-After</c> when sent.
    /// </summary>
    private static void Configure(
        HttpStandardResilienceOptions options, int attemptSeconds, int totalSeconds, int retries, int firstRetryDelaySeconds)
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(attemptSeconds);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(totalSeconds);
        options.Retry.MaxRetryAttempts = retries;
        options.Retry.Delay = TimeSpan.FromSeconds(firstRetryDelaySeconds);
        options.Retry.ShouldRetryAfterHeader = true;

        // Must be at least twice the attempt timeout (validated by the library).
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(attemptSeconds * 4);
    }
}
