using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WeatherRisk.Data.Persistence;

namespace WeatherRisk.Data.Tests;

/// <summary>
/// A private in-memory SQLite database with all migrations applied. The connection stays open for the
/// test's lifetime (closing it would discard the database). Each <see cref="CreateContext"/> call returns a
/// fresh context on the same database — so tests can verify data really round-trips through SQL.
/// </summary>
internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<WeatherRiskDbContext> _options;

    private SqliteTestDatabase(SqliteConnection connection)
    {
        _connection = connection;
        _options = new DbContextOptionsBuilder<WeatherRiskDbContext>().UseSqlite(connection).Options;
    }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var database = new SqliteTestDatabase(connection);
        await using WeatherRiskDbContext db = database.CreateContext();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        return database;
    }

    public WeatherRiskDbContext CreateContext() => new(_options);

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
