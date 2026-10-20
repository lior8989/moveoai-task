using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeatherRisk.Data.Persistence.Entities;

namespace WeatherRisk.Data.Persistence.Configurations;

/// <summary>Schema for <c>RiskScoreSnapshots</c> — append-only score history.</summary>
internal sealed class RiskScoreSnapshotConfiguration : IEntityTypeConfiguration<RiskScoreSnapshotEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RiskScoreSnapshotEntity> builder)
    {
        builder.ToTable("RiskScoreSnapshots", t => t.HasComment("History of computed hub risk scores (trends and change alerts)."));
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasComment("Auto-increment id; also the chronological order of snapshots.");
        builder.Property(s => s.HubId).HasMaxLength(64).HasComment("Hub id (FK to Hubs).");
        builder.Property(s => s.ComputedAt).HasComment("When the score was computed (UTC, stored as 64-bit integer).");
        builder.Property(s => s.CompositeScore).HasComment("Composite risk score 0-100.");
        builder.Property(s => s.Rating).HasConversion<string>().HasMaxLength(16)
            .HasComment("Composite rating: Low, Moderate, High, VeryHigh.");
        builder.Property(s => s.Confidence).HasConversion<string>().HasMaxLength(16)
            .HasComment("Data confidence: Low, Medium, High.");
        builder.Property(s => s.WinterScore).HasComment("Winter hazard score 0-100.");
        builder.Property(s => s.HurricaneScore).HasComment("Hurricane hazard score 0-100.");
        builder.Property(s => s.FloodScore).HasComment("Flood hazard score 0-100.");
        builder.Property(s => s.SevereStormScore).HasComment("Severe-storm hazard score 0-100.");
        builder.Property(s => s.HeatScore).HasComment("Heat hazard score 0-100.");

        // Serves "latest for hub" and "history for hub" (newest first) without a table scan.
        builder.HasIndex(s => new { s.HubId, s.Id });

        builder.HasOne<HubEntity>().WithMany().HasForeignKey(s => s.HubId).OnDelete(DeleteBehavior.Cascade);
    }
}
