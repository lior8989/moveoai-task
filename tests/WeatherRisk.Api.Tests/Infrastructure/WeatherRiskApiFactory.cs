using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace WeatherRisk.Api.Tests.Infrastructure;

/// <summary>
/// Hosts the real API pipeline in memory for endpoint tests, backed by a private temporary SQLite file
/// (migrated on startup, deleted on dispose). <see cref="Settings"/> lets a test override configuration
/// (e.g. a tiny rate limit) without touching production config.
/// </summary>
public sealed class WeatherRiskApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"weatherrisk-test-{Guid.NewGuid():N}.db");

    /// <summary>Configuration overrides applied on top of appsettings.json, e.g. <c>["RateLimiting:Global:PermitLimit"] = "2"</c>.</summary>
    public IReadOnlyDictionary<string, string?> Settings { get; init; } = new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Connection string goes in as a host setting: Program.cs reads it before the app is built.
        builder.UseSetting("ConnectionStrings:WeatherRisk", $"Data Source={_databasePath}");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(Settings));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        // Pooled connections keep the file locked on Windows; release them before deleting.
        SqliteConnection.ClearAllPools();
        foreach (string suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            File.Delete(_databasePath + suffix);
        }
    }
}
