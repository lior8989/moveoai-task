using Microsoft.EntityFrameworkCore;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Data.Persistence.Entities;

namespace WeatherRisk.Data.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IRiskScoreHistoryRepository"/>.</summary>
/// <remarks>"Newest" is defined by the auto-increment id (insertion order), served by the (HubId, Id) index.</remarks>
internal sealed class RiskScoreHistoryRepository : IRiskScoreHistoryRepository
{
    private readonly WeatherRiskDbContext _db;

    /// <summary>Creates the repository.</summary>
    /// <param name="db">Database context (scoped).</param>
    public RiskScoreHistoryRepository(WeatherRiskDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task AddAsync(IReadOnlyCollection<RiskScoreSnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        if (snapshots.Count == 0)
        {
            return;
        }

        _db.RiskScoreSnapshots.AddRange(snapshots.Select(RiskScoreSnapshotEntity.FromDomain));
        await _db.SaveChangesAsync(cancellationToken);
        _db.ChangeTracker.Clear();
    }

    /// <inheritdoc />
    public async Task<RiskScoreSnapshot?> GetLatestAsync(string hubId, CancellationToken cancellationToken = default)
    {
        string id = Hub.NormalizeId(hubId);
        RiskScoreSnapshotEntity? entity = await _db.RiskScoreSnapshots
            .AsNoTracking()
            .Where(s => s.HubId == id)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return entity?.ToDomain();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RiskScoreSnapshot>> GetHistoryAsync(
        string hubId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        string id = Hub.NormalizeId(hubId);

        List<RiskScoreSnapshotEntity> entities = await _db.RiskScoreSnapshots
            .AsNoTracking()
            .Where(s => s.HubId == id)
            .OrderByDescending(s => s.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return entities.ConvertAll(e => e.ToDomain());
    }
}
