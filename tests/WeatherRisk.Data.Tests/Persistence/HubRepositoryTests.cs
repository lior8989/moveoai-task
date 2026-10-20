using WeatherRisk.Core.Domain;
using WeatherRisk.Data.Persistence;
using WeatherRisk.Data.Persistence.Repositories;

namespace WeatherRisk.Data.Tests.Persistence;

public sealed class HubRepositoryTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _database = await SqliteTestDatabase.CreateAsync();

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task GetAllAsync_ReturnsSeededHubsOrderedByName()
    {
        await using WeatherRiskDbContext db = _database.CreateContext();

        IReadOnlyList<Hub> hubs = await new HubRepository(db).GetAllAsync(Ct);

        Assert.Equal(15, hubs.Count);
        Assert.Equal(hubs.OrderBy(h => h.Name, StringComparer.Ordinal).Select(h => h.Id), hubs.Select(h => h.Id));
    }

    [Theory]
    [InlineData("dallas")]
    [InlineData("Dallas")]
    [InlineData("  DALLAS ")]
    public async Task GetByIdAsync_IsCaseAndWhitespaceInsensitive(string id)
    {
        await using WeatherRiskDbContext db = _database.CreateContext();

        Hub? hub = await new HubRepository(db).GetByIdAsync(id, Ct);

        Assert.NotNull(hub);
        Assert.Equal("Dallas", hub.City);
        Assert.Equal(Region.South, hub.Region);
        Assert.Equal("48113", hub.CountyFips);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        await using WeatherRiskDbContext db = _database.CreateContext();

        Assert.Null(await new HubRepository(db).GetByIdAsync("atlantis", Ct));
    }
}
