using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using WeatherRisk.Api.Contracts.System;
using WeatherRisk.Api.Tests.Infrastructure;

namespace WeatherRisk.Api.Tests.Endpoints;

/// <summary>The static chat UI is served correctly and securely by the real host.</summary>
public sealed partial class ChatUiTests : IClassFixture<WeatherRiskApiFactory>
{
    private readonly WeatherRiskApiFactory _factory;

    public ChatUiTests(WeatherRiskApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Index_IsServedAtRoot_WithModuleScriptAndChatControls()
    {
        string html = await _factory.CreateClient().GetStringAsync("/", Ct);

        Assert.Contains("<script type=\"module\" src=\"/js/app.js\"></script>", html);
        Assert.Contains("id=\"composer\"", html);
        Assert.Contains("id=\"mic\"", html);
        Assert.Contains("href=\"/swagger\"", html);
    }

    [Theory]
    [InlineData("/js/app.js", "text/javascript")]
    [InlineData("/js/markdown.js", "text/javascript")]
    [InlineData("/js/format.js", "text/javascript")]
    [InlineData("/js/voice.js", "text/javascript")]
    [InlineData("/css/app.css", "text/css")]
    public async Task Assets_AreServedWithCorrectContentType(string path, string mediaType)
    {
        HttpResponseMessage response = await _factory.CreateClient().GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task EveryAssetReferencedByTheUi_Exists()
    {
        HttpClient client = _factory.CreateClient();
        string html = await client.GetStringAsync("/", Ct);
        string app = await client.GetStringAsync("/js/app.js", Ct);

        IEnumerable<string> referenced = AssetReference().Matches(html).Select(m => m.Groups[1].Value)
            .Concat(ModuleImport().Matches(app).Select(m => "/js/" + m.Groups[1].Value));

        foreach (string path in referenced.Distinct())
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path, Ct)).StatusCode);
        }
    }

    [Fact]
    public async Task UiResponses_CarrySecurityAndRevalidationHeaders()
    {
        HttpResponseMessage response = await _factory.CreateClient().GetAsync("/", Ct);

        string csp = string.Join(";", response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("script-src 'self'", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(response.Headers.CacheControl?.NoCache);
        Assert.NotNull(response.Headers.ETag);
    }

    [Fact]
    public async Task StaticAssets_AreNotCountedInApiMetrics()
    {
        await using var factory = new WeatherRiskApiFactory();
        HttpClient client = factory.CreateClient();
        await client.GetAsync("/", Ct);
        await client.GetAsync("/js/app.js", Ct);

        MetricsSnapshot? metrics = await client.GetFromJsonAsync<MetricsSnapshot>("/api/system/metrics", Ct);

        Assert.Equal(0, metrics!.Overall.Total);
    }

    [GeneratedRegex("""(?:src|href)="(/(?:js|css)/[^"]+)""")]
    private static partial Regex AssetReference();

    [GeneratedRegex("""from "\./([^"]+)";""")]
    private static partial Regex ModuleImport();
}
