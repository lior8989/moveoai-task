using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeatherRisk.Data.Persistence.Entities;

namespace WeatherRisk.Data.Persistence.Configurations;

/// <summary>Schema for <c>WeatherSync</c> — cache bookkeeping for daily weather.</summary>
internal sealed class WeatherSyncConfiguration : IEntityTypeConfiguration<WeatherSyncEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WeatherSyncEntity> builder)
    {
        builder.ToTable("WeatherSync", t => t.HasComment("Which weather period is cached per hub and when it was fetched (cache freshness)."));
        builder.HasKey(s => s.HubId);

        builder.Property(s => s.HubId).HasMaxLength(64).HasComment("Hub id (PK, FK to Hubs).");
        builder.Property(s => s.CoveredFrom).HasComment("First day of the last refreshed period.");
        builder.Property(s => s.CoveredTo).HasComment("Last day of the last refreshed period.");
        builder.Property(s => s.SyncedAt).HasComment("When the period was fetched from Open-Meteo (UTC, stored as 64-bit integer).");

        builder.HasOne<HubEntity>().WithOne().HasForeignKey<WeatherSyncEntity>(s => s.HubId).OnDelete(DeleteBehavior.Cascade);
    }
}
