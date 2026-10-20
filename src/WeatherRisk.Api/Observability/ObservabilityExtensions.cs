using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WeatherRisk.Api.Observability;

/// <summary>Registers monitoring services: request metrics and health checks.</summary>
public static class ObservabilityExtensions
{
    /// <summary>Adds the request metrics collector, its settings, and the health-check service.</summary>
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

        // Dependency checks (Open-Meteo, FEMA, LLM) are registered by the features that own them.
        services.AddHealthChecks();
        return services;
    }

    /// <summary>Adds <see cref="RequestMetricsMiddleware"/>. Call after routing and before rate limiting.</summary>
    /// <param name="app">Application builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IApplicationBuilder UseRequestMetrics(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestMetricsMiddleware>();
}
