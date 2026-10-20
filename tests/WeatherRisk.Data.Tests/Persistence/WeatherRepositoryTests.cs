using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Weather;
using WeatherRisk.Data.Persistence;
using WeatherRisk.Data.Persistence.Repositories;

namespace WeatherRisk.Data.Tests.Persistence;

public sealed class WeatherRepositoryTests : IAsyncLifetime
{
    private static readonly DateOnly Jan1 = new(2025, 1, 1);
    private static readonly DateTimeOffset SyncedAt = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private SqliteTestDatabase _database = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _database = await SqliteTestDatabase.CreateAsync();

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    private static DailyWeather[] Days(int count, double snow = 1) =>
        Enumerable.Range(0, count).Select(i => new DailyWeather(Jan1.AddDays(i), snow, 2, 30, 10, -1)).ToArray();

    private async Task ReplaceAsync(string hubId, DateOnly from, DateOnly to, IReadOnlyCollection<DailyWeather> days)
    {
        await using WeatherRiskDbContext db = _database.CreateContext();
        await new WeatherRepository(db).ReplaceDailyAsync(hubId, from, to, days, SyncedAt, Ct);
    }

    private async Task<IReadOnlyList<DailyWeather>> GetAsync(string hubId, DateOnly from, DateOnly to)
    {
        await using WeatherRiskDbContext db = _database.CreateContext();
        return await new WeatherRepository(db).GetDailyAsync(hubId, from, to, Ct);
    }

    [Fact]
    public async Task ReplaceThenGet_RoundTripsRowsInDateOrder()
    {
        DailyWeather[] days = Days(10);
        await ReplaceAsync("denver", Jan1, Jan1.AddDays(9), days.Reverse().ToArray());

        IReadOnlyList<DailyWeather> stored = await GetAsync("denver", Jan1, Jan1.AddDays(9));

        Assert.Equal(days, stored); // Value equality on the record struct, ordered by date.
    }

    [Fact]
    public async Task Replace_NullValues_RoundTripAsNull()
    {
        await ReplaceAsync("denver", Jan1, Jan1, [new DailyWeather(Jan1, null, null, null, null, null)]);

        DailyWeather stored = Assert.Single(await GetAsync("denver", Jan1, Jan1));

        Assert.Null(stored.SnowfallCm);
        Assert.Null(stored.MinTemperatureC);
    }

    [Fact]
    public async Task Replace_Twice_ReplacesInsteadOfDuplicating()
    {
        await ReplaceAsync("denver", Jan1, Jan1.AddDays(9), Days(10, snow: 1));
        await ReplaceAsync("denver", Jan1, Jan1.AddDays(9), Days(5, snow: 7));

        IReadOnlyList<DailyWeather> stored = await GetAsync("denver", Jan1, Jan1.AddDays(9));

        Assert.Equal(5, stored.Count);
        Assert.All(stored, d => Assert.Equal(7, d.SnowfallCm));
    }

    [Fact]
    public async Task Replace_RowsOutsidePeriodOrDuplicated_AreIgnored()
    {
        DailyWeather[] days = [.. Days(3), Days(1)[0], new DailyWeather(Jan1.AddDays(-1), 9, 0, 0, 0, 0)];

        await ReplaceAsync("denver", Jan1, Jan1.AddDays(2), days);

        Assert.Equal(3, (await GetAsync("denver", Jan1.AddDays(-5), Jan1.AddDays(5))).Count);
    }

    [Fact]
    public async Task Replace_OneHub_LeavesOtherHubsUntouched()
    {
        await ReplaceAsync("denver", Jan1, Jan1.AddDays(4), Days(5));
        await ReplaceAsync("chicago", Jan1, Jan1.AddDays(4), Days(2));

        Assert.Equal(5, (await GetAsync("denver", Jan1, Jan1.AddDays(4))).Count);
        Assert.Equal(2, (await GetAsync("chicago", Jan1, Jan1.AddDays(4))).Count);
    }

    [Fact]
    public async Task Get_SubRange_ReturnsOnlyRowsInRange()
    {
        await ReplaceAsync("denver", Jan1, Jan1.AddDays(9), Days(10));

        IReadOnlyList<DailyWeather> stored = await GetAsync("denver", Jan1.AddDays(2), Jan1.AddDays(4));

        Assert.Equal([Jan1.AddDays(2), Jan1.AddDays(3), Jan1.AddDays(4)], stored.Select(d => d.Date));
    }

    [Fact]
    public async Task Replace_RecordsSyncState()
    {
        await ReplaceAsync("Denver", Jan1, Jan1.AddDays(9), Days(10));
        await using WeatherRiskDbContext db = _database.CreateContext();

        WeatherSyncState? state = await new WeatherRepository(db).GetSyncStateAsync("denver", Ct);

        Assert.Equal(new WeatherSyncState("denver", Jan1, Jan1.AddDays(9), SyncedAt), state);
    }

    [Fact]
    public async Task GetSyncState_NothingCached_ReturnsNull()
    {
        await using WeatherRiskDbContext db = _database.CreateContext();

        Assert.Null(await new WeatherRepository(db).GetSyncStateAsync("denver", Ct));
    }

    [Fact]
    public async Task Replace_EndBeforeStart_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ReplaceAsync("denver", Jan1, Jan1.AddDays(-1), []));
    }
}
