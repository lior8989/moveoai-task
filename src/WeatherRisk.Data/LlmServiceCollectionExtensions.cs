using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WeatherRisk.Core.Agent.Llm;
using WeatherRisk.Data.External.Llm;

namespace WeatherRisk.Data;

/// <summary>Registers the LLM client: one resilient named HttpClient per provider plus the fallback chain.</summary>
public static class LlmServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="ILlmClient"/> backed by every provider in <c>Llm:ProviderOrder</c>.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">The <c>Llm</c> configuration section.</param>
    /// <param name="configureClient">Optional hook per provider client (dependency name, builder) — used for metrics.</param>
    /// <returns>Dependency names of the providers (for health checks).</returns>
    /// <remarks>
    /// Provider <i>names</i> are read at registration (named HttpClients must be registered up front); URLs,
    /// models and keys are read from options at first use.
    /// </remarks>
    public static IReadOnlyList<string> AddWeatherRiskLlm(
        this IServiceCollection services, IConfiguration configuration, Action<string, IHttpClientBuilder>? configureClient = null)
    {
        services.AddOptions<LlmSettings>().Bind(configuration);
        services.AddSingleton<ILlmClient, FallbackLlmClient>();

        var dependencies = new List<string>();
        foreach (string provider in configuration.GetSection(nameof(LlmSettings.ProviderOrder)).Get<string[]>() ?? [])
        {
            string name = FallbackLlmClient.DependencyName(provider);
            IHttpClientBuilder client = services.AddHttpClient(name);
            configureClient?.Invoke(name, client);

            // Fail over quickly: one retry for transient errors, no waiting on long Retry-After — the next
            // model/provider in the chain is a better use of the user's time than a rate-limit wait.
            client.AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(45);
                options.Retry.MaxRetryAttempts = 1;
                options.Retry.Delay = TimeSpan.FromSeconds(1);
                options.Retry.ShouldRetryAfterHeader = false;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
            });
            dependencies.Add(name);
        }

        return dependencies;
    }
}
