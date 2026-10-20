using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Agent.Llm;
using WeatherRisk.Data.External.Llm;

namespace WeatherRisk.Data.Tests.External.Llm;

public sealed class FallbackLlmClientTests
{
    private const string Answer = """{"choices":[{"message":{"role":"assistant","content":"ok"}}]}""";
    private static readonly LlmRequest Request = new([LlmMessage.User("hi")], [], RequireToolCall: false);

    private readonly Dictionary<string, StubHttpMessageHandler> _stubs = new()
    {
        ["llm-groq"] = new StubHttpMessageHandler(),
        ["llm-gemini"] = new StubHttpMessageHandler(),
    };

    private readonly RecordingMetrics _metrics = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private FallbackLlmClient Create(string? groqKey = "g", string? geminiKey = "m") => new(
        new StubFactory(_stubs),
        Options.Create(new LlmSettings
        {
            ProviderOrder = ["Groq", "Gemini"],
            Providers = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Groq"] = new() { BaseUrl = "https://groq.test/v1/", Models = ["big", "small"], ApiKey = groqKey },
                ["Gemini"] = new() { BaseUrl = "https://gemini.test/v1/", Models = ["flash"], ApiKey = geminiKey },
            },
        }),
        _metrics,
        NullLogger<FallbackLlmClient>.Instance);

    [Fact]
    public async Task CompleteAsync_FirstModelSucceeds_UsesIt()
    {
        _stubs["llm-groq"].RespondJson(Answer);

        LlmCompletion completion = await Create().CompleteAsync(Request, Ct);

        Assert.Equal(("Groq", "big"), (completion.Provider, completion.Model));
        Assert.Empty(_metrics.Fallbacks);
    }

    [Fact]
    public async Task CompleteAsync_FirstModelRateLimited_FallsBackToNextModelAndRecordsIt()
    {
        _stubs["llm-groq"].RespondStatus(HttpStatusCode.TooManyRequests).RespondJson(Answer);

        LlmCompletion completion = await Create().CompleteAsync(Request, Ct);

        Assert.Equal(("Groq", "small"), (completion.Provider, completion.Model));
        Assert.Equal(["llm-groq"], _metrics.Fallbacks);
    }

    [Fact]
    public async Task CompleteAsync_WholeProviderDown_FallsBackToNextProvider()
    {
        _stubs["llm-groq"].RespondStatus(HttpStatusCode.ServiceUnavailable).RespondStatus(HttpStatusCode.ServiceUnavailable);
        _stubs["llm-gemini"].RespondJson(Answer);

        LlmCompletion completion = await Create().CompleteAsync(Request, Ct);

        Assert.Equal(("Gemini", "flash"), (completion.Provider, completion.Model));
        Assert.Equal(2, _metrics.Fallbacks.Count);
    }

    [Fact]
    public async Task CompleteAsync_EverythingFails_ThrowsUnavailableWithLastError()
    {
        _stubs["llm-groq"].RespondStatus(HttpStatusCode.InternalServerError).RespondStatus(HttpStatusCode.InternalServerError);
        _stubs["llm-gemini"].RespondStatus(HttpStatusCode.BadGateway);

        var ex = await Assert.ThrowsAsync<LlmUnavailableException>(() => Create().CompleteAsync(Request, Ct));

        Assert.Contains("502", ex.InnerException?.Message);
        Assert.Equal(3, _metrics.Fallbacks.Count);
    }

    [Fact]
    public async Task CompleteAsync_ProviderWithoutKey_IsSkipped()
    {
        _stubs["llm-gemini"].RespondJson(Answer);

        LlmCompletion completion = await Create(groqKey: null).CompleteAsync(Request, Ct);

        Assert.Equal("Gemini", completion.Provider);
        Assert.Empty(_stubs["llm-groq"].Requests);
    }

    [Fact]
    public async Task CompleteAsync_NothingConfigured_ThrowsWithSetupHint()
    {
        var ex = await Assert.ThrowsAsync<LlmUnavailableException>(() => Create(null, null).CompleteAsync(Request, Ct));

        Assert.Contains("user-secrets", ex.Message);
    }

    [Fact]
    public async Task CompleteAsync_CallerCancels_PropagatesWithoutFallback()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().CompleteAsync(Request, cts.Token));
        Assert.Empty(_metrics.Fallbacks);
    }

    private sealed class StubFactory(Dictionary<string, StubHttpMessageHandler> stubs) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(stubs[name], disposeHandler: false);
    }

    private sealed class RecordingMetrics : IDependencyMetrics
    {
        public List<string> Fallbacks { get; } = [];

        public void RecordFallback(string dependency) => Fallbacks.Add(dependency);
    }
}
