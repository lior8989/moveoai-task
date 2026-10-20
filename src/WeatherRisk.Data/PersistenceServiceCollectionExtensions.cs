using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Data.Persistence;
using WeatherRisk.Data.Persistence.Repositories;

namespace WeatherRisk.Data;

/// <summary>Registers persistence (SQLite database + repositories) and initializes the database.</summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>Adds the pooled <see cref="WeatherRiskDbContext"/> and all repositories (scoped).</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="connectionString">SQLite connection string, e.g. <c>Data Source=App_Data/weatherrisk.db</c>.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddWeatherRiskPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Pooling reuses context instances across requests — fewer allocations per request.
        services.AddDbContextPool<WeatherRiskDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<IHubRepository, HubRepository>();
        services.AddScoped<IWeatherRepository, WeatherRepository>();
        services.AddScoped<IHazardBaselineRepository, HazardBaselineRepository>();
        services.AddScoped<IRiskScoreHistoryRepository, RiskScoreHistoryRepository>();
        return services;
    }

    /// <summary>
    /// Applies pending migrations (creating the database on first run, including the hub seed) and enables
    /// SQLite WAL mode so background writes don't block API reads.
    /// </summary>
    /// <param name="services">Root service provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task InitializeWeatherRiskDatabaseAsync(
        this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WeatherRiskDbContext>();

        await db.Database.MigrateAsync(cancellationToken);

        // WAL is persistent per database file; harmless (no-op) for in-memory databases.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
    }
}
