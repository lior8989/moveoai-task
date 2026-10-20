using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using WeatherRisk.Data.Persistence;

namespace WeatherRisk.Data.Tests.Persistence;

public sealed class SchemaTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrations_ApplyCleanlyAndSeedAllHubs()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        await using WeatherRiskDbContext db = database.CreateContext();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync(Ct));
        Assert.Equal(15, await db.Hubs.CountAsync(Ct));
    }

    [Fact]
    public async Task Model_MatchesLatestMigration_NoPendingModelChanges()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        await using WeatherRiskDbContext db = database.CreateContext();

        // Fails if someone changes an entity/configuration without adding a migration.
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Model_EveryTableAndColumnHasAComment()
    {
        // Enforces the api-swagger skill: the schema endpoint must explain every column.
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        await using WeatherRiskDbContext db = database.CreateContext();
        IModel model = db.GetService<IDesignTimeModel>().Model;

        Assert.All(model.GetEntityTypes(), entity =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entity.GetComment()), $"Table {entity.GetTableName()} lacks a comment.");
            Assert.All(entity.GetProperties(), p =>
                Assert.False(string.IsNullOrWhiteSpace(p.GetComment()), $"{entity.GetTableName()}.{p.Name} lacks a comment."));
        });
    }
}
