using Microsoft.EntityFrameworkCore;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Data.Persistence;
using WeatherRisk.Data.Persistence.Repositories;

namespace WeatherRisk.Data.Tests.Persistence;

public sealed class RiskScoreHistoryRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private SqliteTestDatabase _database = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _database = await SqliteTestDatabase.CreateAsync();

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    private static RiskScoreSnapshot Snapshot(string hubId, double composite, int minutesAfter) => new(
        hubId, T0.AddMinutes(minutesAfter), composite, RiskRatings.FromScore(composite), ScoreConfidence.High,
        10, 20, 30, 40, 50);

    private async Task AddAsync(params RiskScoreSnapshot[] snapshots)
    {
        await using WeatherRiskDbContext db = _database.CreateContext();
        await new RiskScoreHistoryRepository(db).AddAsync(snapshots, Ct);
    }

    [Fact]
    public async Task GetLatest_ReturnsMostRecentlyAddedSnapshotForThatHub()
    {
        await AddAsync(Snapshot("dallas", 55, 0), Snapshot("miami", 80, 1));
        await AddAsync(Snapshot("dallas", 61.5, 2));
        await using WeatherRiskDbContext db = _database.CreateContext();

        RiskScoreSnapshot? latest = await new RiskScoreHistoryRepository(db).GetLatestAsync("Dallas", Ct);

        Assert.Equal(Snapshot("dallas", 61.5, 2), latest);
    }

    [Fact]
    public async Task GetLatest_NoHistory_ReturnsNull()
    {
        await using WeatherRiskDbContext db = _database.CreateContext();

        Assert.Null(await new RiskScoreHistoryRepository(db).GetLatestAsync("dallas", Ct));
    }

    [Fact]
    public async Task GetHistory_ReturnsNewestFirstUpToLimit()
    {
        await AddAsync(Snapshot("dallas", 50, 0), Snapshot("dallas", 52, 1), Snapshot("dallas", 54, 2));
        await using WeatherRiskDbContext db = _database.CreateContext();

        IReadOnlyList<RiskScoreSnapshot> history = await new RiskScoreHistoryRepository(db).GetHistoryAsync("dallas", 2, Ct);

        Assert.Equal([54.0, 52.0], history.Select(h => h.CompositeScore));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task GetHistory_NonPositiveLimit_Throws(int limit)
    {
        await using WeatherRiskDbContext db = _database.CreateContext();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => new RiskScoreHistoryRepository(db).GetHistoryAsync("dallas", limit, Ct));
    }

    [Fact]
    public async Task Add_UnknownHub_ViolatesForeignKey()
    {
        await Assert.ThrowsAsync<DbUpdateException>(() => AddAsync(Snapshot("atlantis", 10, 0)));
    }

    [Fact]
    public async Task Add_EmptyBatch_IsNoOp()
    {
        await AddAsync();
        await using WeatherRiskDbContext db = _database.CreateContext();

        Assert.Equal(0, await db.RiskScoreSnapshots.CountAsync(Ct));
    }
}
