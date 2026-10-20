namespace WeatherRisk.Data.External.Llm;

/// <summary>
/// LLM providers and fallback order, bound from the <c>Llm</c> configuration section. API keys come from
/// user-secrets or environment variables (<c>Llm:Providers:Groq:ApiKey</c>) — never from committed files.
/// </summary>
public sealed class LlmSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Llm";

    /// <summary>Providers to try, in order. Each provider's models are tried in their listed order.</summary>
    public List<string> ProviderOrder { get; init; } = [];

    /// <summary>Provider settings by name (case-insensitive).</summary>
    public Dictionary<string, LlmProviderSettings> Providers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>One OpenAI-compatible provider (Groq, Gemini's OpenAI endpoint, Ollama, …).</summary>
public sealed class LlmProviderSettings
{
    /// <summary>Base URL ending with <c>/</c>, e.g. <c>https://api.groq.com/openai/v1/</c>.</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>Models to try in order (each has its own rate limit on most providers).</summary>
    public List<string> Models { get; init; } = [];

    /// <summary>API key (secret). A provider without a key is skipped unless <see cref="RequiresApiKey"/> is false.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Whether the provider needs a key (false for a local Ollama).</summary>
    public bool RequiresApiKey { get; init; } = true;

    /// <summary>Sampling temperature (default 0.2: factual, low-variance answers).</summary>
    public double Temperature { get; init; } = 0.2;

    /// <summary>
    /// Maximum output tokens per step (default 1500 — a final answer uses ~600–900). Kept modest because some
    /// providers (Groq) count the requested output budget against the per-minute token quota.
    /// </summary>
    public int MaxTokens { get; init; } = 1500;

    /// <summary>
    /// Optional <c>reasoning_effort</c> for reasoning models. Measured on Groq gpt-oss: <c>low</c> made the model
    /// emit an invalid tool name ("json") for the final structured answer in 3/3 trials; <c>medium</c> was 3/3 correct.
    /// </summary>
    public string? ReasoningEffort { get; init; }

    /// <summary>Whether this provider can be used.</summary>
    public bool IsConfigured =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) && Models.Count > 0 && (!RequiresApiKey || !string.IsNullOrWhiteSpace(ApiKey));
}
