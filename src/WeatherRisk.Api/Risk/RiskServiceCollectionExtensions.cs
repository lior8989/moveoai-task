using Microsoft.Extensions.Options;
using WeatherRisk.Core.Application;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Scoring.Hazards;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Api.Risk;

/// <summary>Registers the deterministic risk model and the application service.</summary>
public static class RiskServiceCollectionExtensions
{
    /// <summary>Configuration section overriding <see cref="ScoringSettings"/> (optional).</summary>
    public const string ScoringSection = "Scoring";

    /// <summary>Configuration section overriding <see cref="WeatherThresholds"/> (optional).</summary>
    public const string ThresholdsSection = "WeatherThresholds";

    /// <summary>
    /// Adds settings (code defaults, optionally overridden by configuration), the five hazard scorers,
    /// the engine, the statistics calculator, the model describer and <see cref="HubRiskService"/>.
    /// </summary>
    /// <remarks>
    /// Settings use the options pattern so they bind from the <i>final</i> configuration (reading them during
    /// registration would miss providers added later), and are validated at startup so a bad configuration
    /// stops the app with a clear message. Core stays free of the options abstraction: the plain settings
    /// objects are exposed as singletons.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">App configuration.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddRiskModel(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WeatherThresholds>().Bind(configuration.GetSection(ThresholdsSection));
        services.AddOptions<ScoringSettings>().Bind(configuration.GetSection(ScoringSection)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ScoringSettings>, ScoringSettingsValidator>();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<WeatherThresholds>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ScoringSettings>>().Value);

        // Stateless, thread-safe model components: one instance for the app's lifetime.
        // One registration per hazard strategy; the engine receives them all as IEnumerable<IHazardScorer>.
        services.AddSingleton<WeatherStatisticsCalculator>();
        services.AddSingleton<IHazardScorer, WinterScorer>();
        services.AddSingleton<IHazardScorer, HurricaneScorer>();
        services.AddSingleton<IHazardScorer, FloodScorer>();
        services.AddSingleton<IHazardScorer, SevereStormScorer>();
        services.AddSingleton<IHazardScorer, HeatScorer>();
        services.AddSingleton<RiskScoringEngine>();
        services.AddSingleton<RiskModelDescriber>(); // Core.Application describer (shared with the agent tool).

        // Scoped: depends on the scoped data provider / repositories.
        services.AddScoped<HubRiskService>();
        return services;
    }

    /// <summary>Adapts <see cref="ScoringSettings.Validate"/> to the options validation pipeline.</summary>
    private sealed class ScoringSettingsValidator : IValidateOptions<ScoringSettings>
    {
        /// <inheritdoc />
        public ValidateOptionsResult Validate(string? name, ScoringSettings options)
        {
            try
            {
                options.Validate();
                return ValidateOptionsResult.Success;
            }
            catch (ArgumentException ex)
            {
                return ValidateOptionsResult.Fail($"Invalid '{ScoringSection}' configuration: {ex.Message}");
            }
        }
    }
}
