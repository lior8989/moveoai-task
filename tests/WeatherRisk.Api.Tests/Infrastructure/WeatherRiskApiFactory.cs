using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

    /// <summary>Optional service overrides (e.g. a stub network handler for all HTTP clients).</summary>
    public Action<IServiceCollection>? TestServices { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Connection string goes in as a host setting: Program.cs reads it before the app is built.
        builder.UseSetting("ConnectionStrings:WeatherRisk", $"Data Source={_databasePath}");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Test defaults first (no background warm-up against the disabled network), then per-test overrides.
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["HubData:WarmUpOnStartup"] = "false" });
            config.AddInMemoryCollection(Settings);
        });

        // Tests never touch the internet: unless a test stubs the network, every outbound call fails fast.
        builder.ConfigureTestServices(services =>
        {
            services.ConfigureHttpClientDefaults(client =>
                client.ConfigurePrimaryHttpMessageHandler(() => new OfflineHandler()));
            TestServices?.Invoke(services);
        });
    }

    /// <summary>Primary handler that refuses all outbound traffic.</summary>
    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException($"Network disabled in tests ({request.RequestUri}).");
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
