using Microsoft.EntityFrameworkCore;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Domain;
using WeatherRisk.Data.Persistence.Entities;

namespace WeatherRisk.Data.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IHubRepository"/>.</summary>
/// <remarks>
/// No in-memory cache: 15 rows from a local SQLite file is sub-millisecond, so caching would add
/// invalidation complexity for no measurable gain.
/// </remarks>
internal sealed class HubRepository : IHubRepository
{
    private readonly WeatherRiskDbContext _db;

    /// <summary>Creates the repository.</summary>
    /// <param name="db">Database context (scoped).</param>
    public HubRepository(WeatherRiskDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Hub>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Hubs
            .AsNoTracking()
            .OrderBy(h => h.Name)
            .Select(h => new Hub(h.Id, h.Name, h.City, h.State, h.Region, h.Latitude, h.Longitude, h.CountyFips))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<Hub?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        string normalized = Hub.NormalizeId(id);
        HubEntity? entity = await _db.Hubs.AsNoTracking().FirstOrDefaultAsync(h => h.Id == normalized, cancellationToken);
        return entity?.ToDomain();
    }
}
