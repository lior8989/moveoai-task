using System.Net.Http.Headers;
using System.Net.Http.Json;
using WeatherRisk.Core.Agent.Llm;

namespace WeatherRisk.Data.External.Llm;

/// <summary>
/// One (provider, model) pair speaking the OpenAI-compatible chat-completions protocol. Groq, Gemini's
/// OpenAI endpoint and Ollama all accept the same request, so one class covers every provider.
/// </summary>
/// <remarks>
/// Resilience and metrics come from the named <see cref="HttpClient"/> pipeline. The API key is sent per
/// request and never logged; error bodies are truncated before being put into exception messages.
/// </remarks>
internal sealed class OpenAiCompatibleChatClient
{
    private const int MaxErrorBodyChars = 300;

    // Groq's code for a malformed tool call by the model (e.g. gpt-oss calling a non-existent "json" tool).
    // It's a sampling glitch, not an outage: one retry of the same model usually succeeds.
    private const string ToolUseFailedCode = "tool_use_failed";

    private readonly HttpClient _http;
    private readonly LlmProviderSettings _settings;

    /// <summary>Creates the client.</summary>
    /// <param name="http">Named HttpClient for this provider (resilience + metrics handlers).</param>
    /// <param name="provider">Provider name, e.g. <c>Groq</c>.</param>
    /// <param name="model">Model id.</param>
    /// <param name="settings">Provider settings (URL, key, sampling).</param>
    public OpenAiCompatibleChatClient(HttpClient http, string provider, string model, LlmProviderSettings settings)
    {
        _http = http;
        Provider = provider;
        Model = model;
        _settings = settings;
    }

    /// <summary>Provider name.</summary>
    public string Provider { get; }

    /// <summary>Model id.</summary>
    public string Model { get; }

    /// <summary>Sends one completion request.</summary>
    /// <param name="request">Provider-neutral request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model's reply.</returns>
    /// <exception cref="HttpRequestException">On transport errors or non-success status.</exception>
    /// <exception cref="InvalidDataException">When the response has no choice/message.</exception>
    public async Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        ChatCompletionRequest body = ToWire(request);
        try
        {
            return await SendAsync(body, cancellationToken);
        }
        catch (LlmToolUseFailedException)
        {
            return await SendAsync(body, cancellationToken); // Second failure propagates → next link in the chain.
        }
    }

    private async Task<LlmCompletion> SendAsync(ChatCompletionRequest body, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_settings.BaseUrl), "chat/completions"))
        {
            Content = JsonContent.Create(body, ChatCompletionJsonContext.Default.ChatCompletionRequest),
        };

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        using HttpResponseMessage response = await _http.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync(cancellationToken);
            string detail = $"{Provider}/{Model} returned {(int)response.StatusCode}: {Truncate(error)}";
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest && error.Contains(ToolUseFailedCode, StringComparison.Ordinal))
            {
                throw new LlmToolUseFailedException(detail);
            }

            throw new HttpRequestException(detail, null, response.StatusCode);
        }

        ChatCompletionResponse? payload = await response.Content.ReadFromJsonAsync(
            ChatCompletionJsonContext.Default.ChatCompletionResponse, cancellationToken);
        ChatMessageDto reply = payload?.Choices?.FirstOrDefault()?.Message
            ?? throw new InvalidDataException($"{Provider}/{Model} returned no message.");

        return new LlmCompletion(
            reply.Content,
            reply.ToolCalls?.Select((c, i) => new LlmToolCall(c.Id ?? $"call_{i}", c.Function.Name, c.Function.Arguments ?? "{}")).ToList() ?? [],
            Provider,
            Model,
            payload!.Usage?.PromptTokens ?? 0,
            payload.Usage?.CompletionTokens ?? 0);
    }

    /// <summary>Maps the provider-neutral request to the wire format.</summary>
    private ChatCompletionRequest ToWire(LlmRequest request) => new()
    {
        Model = Model,
        Temperature = _settings.Temperature,
        MaxTokens = _settings.MaxTokens,
        ReasoningEffort = string.IsNullOrWhiteSpace(_settings.ReasoningEffort) ? null : _settings.ReasoningEffort,
        ToolChoice = request.Tools.Count == 0 ? null : request.RequireToolCall ? "required" : "auto",
        Tools = request.Tools.Count == 0
            ? null
            : request.Tools.Select(t => new ToolDto
            {
                Function = new FunctionDto { Name = t.Name, Description = t.Description, Parameters = t.ParametersSchema },
            }).ToList(),
        Messages = request.Messages.Select(m => new ChatMessageDto
        {
            Role = m.Role switch
            {
                LlmRole.System => "system",
                LlmRole.User => "user",
                LlmRole.Assistant => "assistant",
                _ => "tool",
            },
            Content = m.Content,
            ToolCallId = m.ToolCallId,
            ToolCalls = m.ToolCalls?.Select(c => new ToolCallDto
            {
                Id = c.Id,
                Function = new ToolCallFunctionDto { Name = c.Name, Arguments = c.ArgumentsJson },
            }).ToList(),
        }).ToList(),
    };

    private static string Truncate(string text) =>
        text.Length <= MaxErrorBodyChars ? text : string.Concat(text.AsSpan(0, MaxErrorBodyChars), "…");
}

/// <summary>The model produced a malformed tool call (provider code <c>tool_use_failed</c>) — retryable on the same model.</summary>
internal sealed class LlmToolUseFailedException(string message) : HttpRequestException(message, null, System.Net.HttpStatusCode.BadRequest);
