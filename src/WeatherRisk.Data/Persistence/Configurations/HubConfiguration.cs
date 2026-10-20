using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeatherRisk.Data.Persistence.Entities;
using WeatherRisk.Data.Persistence.Seed;

namespace WeatherRisk.Data.Persistence.Configurations;

/// <summary>Schema for <c>Hubs</c> — reference data seeded from <see cref="HubCatalog"/>.</summary>
internal sealed class HubConfiguration : IEntityTypeConfiguration<HubEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<HubEntity> builder)
    {
        builder.ToTable("Hubs", t => t.HasComment("Distribution hubs assessed for weather risk (reference data)."));
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).HasMaxLength(64).HasComment("Lower-case hub id used in APIs, e.g. 'dallas'.");
        builder.Property(h => h.Name).HasMaxLength(128).HasComment("Display name.");
        builder.Property(h => h.City).HasMaxLength(128).HasComment("City.");
        builder.Property(h => h.State).HasMaxLength(2).HasComment("Two-letter US state code.");
        builder.Property(h => h.Region).HasConversion<string>().HasMaxLength(16)
            .HasComment("US Census region: Midwest, South, West, Northeast.");
        builder.Property(h => h.Latitude).HasComment("Latitude, decimal degrees (WGS84).");
        builder.Property(h => h.Longitude).HasComment("Longitude, decimal degrees (WGS84).");
        builder.Property(h => h.CountyFips).HasMaxLength(5).HasComment("5-digit county FIPS code (FEMA NRI lookup key).");

        builder.HasIndex(h => h.Region); // "Hubs in the Midwest" filters.

        builder.HasData(HubCatalog.All.Select(HubEntity.FromDomain));
    }
}
