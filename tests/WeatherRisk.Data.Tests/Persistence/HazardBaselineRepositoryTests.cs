using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Data.Persistence;
using WeatherRisk.Data.Persistence.Repositories;

namespace WeatherRisk.Data.Tests.Persistence;

public sealed class HazardBaselineRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset FetchedAt = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private SqliteTestDatabase _database = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _database = await SqliteTestDatabase.CreateAsync();

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task Get_NotCached_ReturnsNull()
    {
        await using WeatherRiskDbContext db = _database.CreateContext();

        Assert.Null(await new HazardBaselineRepository(db).GetAsync("48113", Ct));
    }

    [Fact]
    public async Task Upsert_NewCounty_InsertsAndRoundTrips()
    {
        var baseline = new HazardBaseline(70, 50, 60, null, 40, null, 95, 90, 30, 80);
        await using (WeatherRiskDbContext db = _database.CreateContext())
        {
            await new HazardBaselineRepository(db).UpsertAsync("48113", baseline, FetchedAt, Ct);
        }

        await using WeatherRiskDbContext read = _database.CreateContext();
        StoredBaseline? stored = await new HazardBaselineRepository(read).GetAsync("48113", Ct);

        Assert.Equal(new StoredBaseline("48113", baseline, FetchedAt), stored);
    }

    [Fact]
    public async Task Upsert_ExistingCounty_UpdatesValues()
    {
        await using (WeatherRiskDbContext db = _database.CreateContext())
        {
            await new HazardBaselineRepository(db).UpsertAsync(
                "48113", new HazardBaseline(1, 1, 1, 1, 1, 1, 1, 1, 1, 1), FetchedAt, Ct);
        }

        var updated = new HazardBaseline(2, 2, 2, 2, 2, 2, 2, 2, 2, 2);
        await using (WeatherRiskDbContext db = _database.CreateContext())
        {
            await new HazardBaselineRepository(db).UpsertAsync("48113", updated, FetchedAt.AddDays(1), Ct);
        }

        await using WeatherRiskDbContext read = _database.CreateContext();
        StoredBaseline? stored = await new HazardBaselineRepository(read).GetAsync("48113", Ct);
        Assert.Equal(updated, stored?.Baseline);
        Assert.Equal(FetchedAt.AddDays(1), stored?.FetchedAt);
    }
}
