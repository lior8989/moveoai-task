using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WeatherRisk.Data.Persistence.Entities;

namespace WeatherRisk.Data.Persistence;

/// <summary>EF Core unit of work for the SQLite database. Schema lives in <c>Configurations/</c>; changes go through migrations.</summary>
public sealed class WeatherRiskDbContext : DbContext
{
    /// <summary>Creates the context.</summary>
    /// <param name="options">Provider/connection options.</param>
    public WeatherRiskDbContext(DbContextOptions<WeatherRiskDbContext> options)
        : base(options)
    {
    }

    /// <summary>Hub catalog.</summary>
    public DbSet<HubEntity> Hubs => Set<HubEntity>();

    /// <summary>Cached daily weather.</summary>
    public DbSet<DailyWeatherEntity> DailyWeather => Set<DailyWeatherEntity>();

    /// <summary>Weather cache bookkeeping.</summary>
    public DbSet<WeatherSyncEntity> WeatherSync => Set<WeatherSyncEntity>();

    /// <summary>Cached FEMA NRI baselines.</summary>
    public DbSet<HazardBaselineEntity> HazardBaselines => Set<HazardBaselineEntity>();

    /// <summary>Score history.</summary>
    public DbSet<RiskScoreSnapshotEntity> RiskScoreSnapshots => Set<RiskScoreSnapshotEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WeatherRiskDbContext).Assembly);

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no native DateTimeOffset; store as a 64-bit integer so comparisons run in SQL.
        // The encoding sorts chronologically for same-offset values — the app always writes UTC.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }
}
