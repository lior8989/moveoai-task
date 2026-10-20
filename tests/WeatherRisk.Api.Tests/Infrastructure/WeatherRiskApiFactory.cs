using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace WeatherRisk.Api.Tests.Infrastructure;

/// <summary>
/// Hosts the real API pipeline in memory for endpoint tests. <see cref="Settings"/> lets a test override
/// configuration (e.g. a tiny rate limit) without touching production config.
/// </summary>
public sealed class WeatherRiskApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Configuration overrides applied on top of appsettings.json, e.g. <c>["RateLimiting:Global:PermitLimit"] = "2"</c>.</summary>
    public IReadOnlyDictionary<string, string?> Settings { get; init; } = new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(Settings));
    }
}
