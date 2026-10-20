using Microsoft.EntityFrameworkCore;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Data.Persistence.Entities;

namespace WeatherRisk.Data.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IHazardBaselineRepository"/>.</summary>
internal sealed class HazardBaselineRepository : IHazardBaselineRepository
{
    private readonly WeatherRiskDbContext _db;

    /// <summary>Creates the repository.</summary>
    /// <param name="db">Database context (scoped).</param>
    public HazardBaselineRepository(WeatherRiskDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<StoredBaseline?> GetAsync(string countyFips, CancellationToken cancellationToken = default)
    {
        HazardBaselineEntity? entity = await _db.HazardBaselines
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.CountyFips == countyFips, cancellationToken);
        return entity is null ? null : new StoredBaseline(entity.CountyFips, entity.ToDomain(), entity.FetchedAt);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(
        string countyFips, HazardBaseline baseline, DateTimeOffset fetchedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        HazardBaselineEntity? entity = await _db.HazardBaselines.FindAsync([countyFips], cancellationToken);
        if (entity is null)
        {
            entity = new HazardBaselineEntity { CountyFips = countyFips };
            _db.HazardBaselines.Add(entity);
        }

        entity.Apply(baseline, fetchedAt);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
