using Microsoft.EntityFrameworkCore;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Weather;
using WeatherRisk.Data.Persistence.Entities;

namespace WeatherRisk.Data.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IWeatherRepository"/>.</summary>
internal sealed class WeatherRepository : IWeatherRepository
{
    private readonly WeatherRiskDbContext _db;

    /// <summary>Creates the repository.</summary>
    /// <param name="db">Database context (scoped).</param>
    public WeatherRepository(WeatherRiskDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DailyWeather>> GetDailyAsync(
        string hubId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        string id = Hub.NormalizeId(hubId);

        // Range scan on the (HubId, Date) primary key; projected straight into the value type.
        return await _db.DailyWeather
            .AsNoTracking()
            .Where(d => d.HubId == id && d.Date >= from && d.Date <= to)
            .OrderBy(d => d.Date)
            .Select(d => new DailyWeather(
                d.Date, d.SnowfallCm, d.PrecipitationMm, d.MaxWindGustKmh, d.MaxTemperatureC, d.MinTemperatureC))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Delete-then-insert in one transaction: a set-based delete plus one batched insert is far cheaper than
    /// per-row upserts for ~1,800 rows. The sync state records the period of this (latest) refresh.
    /// </remarks>
    public async Task ReplaceDailyAsync(
        string hubId, DateOnly from, DateOnly to, IReadOnlyCollection<DailyWeather> days, DateTimeOffset syncedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(days);
        if (to < from)
        {
            throw new ArgumentException($"Period end {to} is before start {from}.", nameof(to));
        }

        string id = Hub.NormalizeId(hubId);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        await _db.DailyWeather
            .Where(d => d.HubId == id && d.Date >= from && d.Date <= to)
            .ExecuteDeleteAsync(cancellationToken);
        await _db.WeatherSync.Where(s => s.HubId == id).ExecuteDeleteAsync(cancellationToken);

        _db.DailyWeather.AddRange(days
            .Where(d => d.Date >= from && d.Date <= to)
            .DistinctBy(d => d.Date) // Defensive: a duplicate date would violate the PK and abort the batch.
            .Select(d => DailyWeatherEntity.FromDomain(id, d)));
        _db.WeatherSync.Add(new WeatherSyncEntity { HubId = id, CoveredFrom = from, CoveredTo = to, SyncedAt = syncedAt });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Release tracked rows: the scoped context should not hold thousands of entities after the write.
        _db.ChangeTracker.Clear();
    }

    /// <inheritdoc />
    public async Task<WeatherSyncState?> GetSyncStateAsync(string hubId, CancellationToken cancellationToken = default)
    {
        string id = Hub.NormalizeId(hubId);
        return await _db.WeatherSync
            .AsNoTracking()
            .Where(s => s.HubId == id)
            .Select(s => new WeatherSyncState(s.HubId, s.CoveredFrom, s.CoveredTo, s.SyncedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
