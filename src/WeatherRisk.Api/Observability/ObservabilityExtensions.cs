using Microsoft.Extensions.DependencyInjection.Extensions;
using WeatherRisk.Core.Abstractions;

namespace WeatherRisk.Api.Observability;

/// <summary>Registers monitoring services: request and dependency metrics, health checks.</summary>
public static class ObservabilityExtensions
{
    /// <summary>Adds the metrics collectors, their settings, and the health-check service.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">App configuration containing the <c>Monitoring</c> section.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MonitoringSettings>()
            .Bind(configuration.GetSection(MonitoringSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<RequestMetricsCollector>();
        services.AddSingleton<DependencyMetricsCollector>();

        // The data layer reports fallbacks through the Core abstraction; same singleton instance.
        services.AddSingleton<IDependencyMetrics>(sp => sp.GetRequiredService<DependencyMetricsCollector>());

        // Dependency checks (database, data sources, LLM) are registered by the features that own them.
        services.AddHealthChecks();
        return services;
    }

    /// <summary>Adds a passive <see cref="DependencyHealthCheck"/> per dependency.</summary>
    /// <param name="builder">Health-checks builder.</param>
    /// <param name="dependencies">Dependency names.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IHealthChecksBuilder AddDependencyChecks(this IHealthChecksBuilder builder, IEnumerable<string> dependencies)
    {
        foreach (string dependency in dependencies)
        {
            builder.AddTypeActivatedCheck<DependencyHealthCheck>(dependency, args: dependency);
        }

        return builder;
    }

    /// <summary>Adds <see cref="DependencyMetricsHandler"/> as the outermost handler of an HTTP client.</summary>
    /// <param name="client">HTTP client builder.</param>
    /// <param name="dependency">Dependency name recorded in metrics.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IHttpClientBuilder AddDependencyMetrics(this IHttpClientBuilder client, string dependency) =>
        client.AddHttpMessageHandler(sp =>
            new DependencyMetricsHandler(dependency, sp.GetRequiredService<DependencyMetricsCollector>()));

    /// <summary>Adds <see cref="RequestMetricsMiddleware"/>. Call after routing and before rate limiting.</summary>
    /// <param name="app">Application builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IApplicationBuilder UseRequestMetrics(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestMetricsMiddleware>();
}
