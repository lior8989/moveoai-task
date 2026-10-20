using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeatherRisk.Data.Persistence.Entities;

namespace WeatherRisk.Data.Persistence.Configurations;

/// <summary>Schema for <c>DailyWeather</c> — cached Open-Meteo daily history.</summary>
internal sealed class DailyWeatherConfiguration : IEntityTypeConfiguration<DailyWeatherEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DailyWeatherEntity> builder)
    {
        builder.ToTable("DailyWeather", t => t.HasComment("Cached daily weather per hub (from Open-Meteo historical API)."));

        // Composite PK doubles as the index for the only query shape: one hub, a date range.
        builder.HasKey(d => new { d.HubId, d.Date });

        builder.Property(d => d.HubId).HasMaxLength(64).HasComment("Hub id (FK to Hubs).");
        builder.Property(d => d.Date).HasComment("Calendar date (hub local).");
        builder.Property(d => d.SnowfallCm).HasComment("Daily snowfall, cm.");
        builder.Property(d => d.PrecipitationMm).HasComment("Daily precipitation, mm.");
        builder.Property(d => d.MaxWindGustKmh).HasComment("Daily maximum wind gust, km/h.");
        builder.Property(d => d.MaxTemperatureC).HasComment("Daily maximum temperature at 2 m, °C.");
        builder.Property(d => d.MinTemperatureC).HasComment("Daily minimum temperature at 2 m, °C.");

        builder.HasOne<HubEntity>().WithMany().HasForeignKey(d => d.HubId).OnDelete(DeleteBehavior.Cascade);
    }
}
