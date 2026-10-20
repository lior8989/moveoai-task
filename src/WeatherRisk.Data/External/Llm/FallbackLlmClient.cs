using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Agent.Llm;

namespace WeatherRisk.Data.External.Llm;

/// <summary>
/// <see cref="ILlmClient"/> that tries each configured (provider, model) pair in order until one answers
/// (Chain of Responsibility). Each failure is logged and counted as a fallback; when every link fails —
/// or none is configured — <see cref="LlmUnavailableException"/> is thrown.
/// </summary>
/// <remarks>
/// Singleton: holds no per-request state. The chain is built lazily from options on first use, so keys
/// supplied by any configuration source are picked up.
/// </remarks>
internal sealed partial class FallbackLlmClient : ILlmClient
{
    private readonly IHttpClientFactory _httpClients;
    private readonly IOptions<LlmSettings> _settings;
    private readonly IDependencyMetrics _metrics;
    private readonly ILogger<FallbackLlmClient> _logger;
    private readonly Lazy<IReadOnlyList<OpenAiCompatibleChatClient>> _chain;

    /// <summary>Creates the client.</summary>
    /// <param name="httpClients">Factory for the per-provider named clients.</param>
    /// <param name="settings">Provider settings.</param>
    /// <param name="metrics">Receives fallback events.</param>
    /// <param name="logger">Logger.</param>
    public FallbackLlmClient(
        IHttpClientFactory httpClients, IOptions<LlmSettings> settings, IDependencyMetrics metrics, ILogger<FallbackLlmClient> logger)
    {
        _httpClients = httpClients;
        _settings = settings;
        _metrics = metrics;
        _logger = logger;
        _chain = new Lazy<IReadOnlyList<OpenAiCompatibleChatClient>>(BuildChain);
    }

    /// <summary>Name of the named HttpClient (and dependency in metrics/health) for a provider.</summary>
    /// <param name="provider">Provider name.</param>
    /// <returns>E.g. <c>llm-groq</c>.</returns>
    public static string DependencyName(string provider) => $"llm-{provider.ToLowerInvariant()}";

    /// <inheritdoc />
    public async Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<OpenAiCompatibleChatClient> chain = _chain.Value;
        if (chain.Count == 0)
        {
            throw new LlmUnavailableException(
                "No LLM provider is configured. Set an API key, e.g. `dotnet user-secrets set \"Llm:Providers:Groq:ApiKey\" <key> --project src/WeatherRisk.Api`.");
        }

        Exception? last = null;
        foreach (OpenAiCompatibleChatClient link in chain)
        {
            // A cancelled request must stop the chain, not be mistaken for a provider failure.
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await link.CompleteAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // Rate limit, outage, timeout, open circuit, malformed reply → try the next model/provider.
                last = ex;
                _metrics.RecordFallback(DependencyName(link.Provider));
                LogFallback(link.Provider, link.Model, ex.Message);
            }
        }

        throw new LlmUnavailableException("All configured LLM providers failed; the assistant is temporarily unavailable.", last);
    }

    /// <summary>Flattens ProviderOrder × Models into the chain, skipping unconfigured providers.</summary>
    private List<OpenAiCompatibleChatClient> BuildChain()
    {
        LlmSettings settings = _settings.Value;
        var chain = new List<OpenAiCompatibleChatClient>();
        foreach (string provider in settings.ProviderOrder)
        {
            if (!settings.Providers.TryGetValue(provider, out LlmProviderSettings? config) || !config.IsConfigured)
            {
                LogSkipped(provider);
                continue;
            }

            HttpClient http = _httpClients.CreateClient(DependencyName(provider));
            chain.AddRange(config.Models.Select(model => new OpenAiCompatibleChatClient(http, provider, model, config)));
        }

        return chain;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "LLM {Provider}/{Model} failed ({Reason}); trying the next one.")]
    private partial void LogFallback(string provider, string model, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "LLM provider {Provider} is not configured (missing URL, models or API key); skipped.")]
    private partial void LogSkipped(string provider);
}
