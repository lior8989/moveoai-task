using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeatherRisk.Data.Persistence.Entities;

namespace WeatherRisk.Data.Persistence.Configurations;

/// <summary>Schema for <c>HazardBaselines</c> — cached FEMA National Risk Index scores.</summary>
internal sealed class HazardBaselineConfiguration : IEntityTypeConfiguration<HazardBaselineEntity>
{
    private const string ScoreComment = "risk score: national percentile 0-100 (null = not rated).";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<HazardBaselineEntity> builder)
    {
        builder.ToTable("HazardBaselines", t => t.HasComment("Cached FEMA National Risk Index hazard scores per county."));

        // Keyed by county, not hub: NRI is county-level and several hubs could share a county.
        builder.HasKey(b => b.CountyFips);

        builder.Property(b => b.CountyFips).HasMaxLength(5).HasComment("5-digit county FIPS code (PK).");
        builder.Property(b => b.WinterWeather).HasComment($"NRI Winter Weather {ScoreComment}");
        builder.Property(b => b.IceStorm).HasComment($"NRI Ice Storm {ScoreComment}");
        builder.Property(b => b.ColdWave).HasComment($"NRI Cold Wave {ScoreComment}");
        builder.Property(b => b.Hurricane).HasComment($"NRI Hurricane {ScoreComment}");
        builder.Property(b => b.RiverineFlooding).HasComment($"NRI Riverine Flooding {ScoreComment}");
        builder.Property(b => b.CoastalFlooding).HasComment($"NRI Coastal Flooding {ScoreComment}");
        builder.Property(b => b.Tornado).HasComment($"NRI Tornado {ScoreComment}");
        builder.Property(b => b.Hail).HasComment($"NRI Hail {ScoreComment}");
        builder.Property(b => b.StrongWind).HasComment($"NRI Strong Wind {ScoreComment}");
        builder.Property(b => b.HeatWave).HasComment($"NRI Heat Wave {ScoreComment}");
        builder.Property(b => b.FetchedAt).HasComment("When the scores were fetched from FEMA (UTC, stored as 64-bit integer).");
    }
}
