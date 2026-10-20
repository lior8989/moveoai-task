using System.Text.Json;

namespace WeatherRisk.Core.Agent.Llm;

/// <summary>
/// Port to a chat-completion LLM with tool calling. Provider-neutral: the agent never knows whether it talks
/// to Groq, Gemini or a local model, or how many fallbacks were tried.
/// </summary>
public interface ILlmClient
{
    /// <summary>Runs one completion step.</summary>
    /// <param name="request">Conversation so far, available tools and whether a tool call is mandatory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model's reply: tool calls and/or text.</returns>
    /// <exception cref="LlmUnavailableException">When no provider is configured or every provider failed.</exception>
    Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Chat roles in the OpenAI-compatible protocol.</summary>
public enum LlmRole
{
    /// <summary>Instructions for the model.</summary>
    System,

    /// <summary>The analyst's message.</summary>
    User,

    /// <summary>A model reply (text and/or tool calls).</summary>
    Assistant,

    /// <summary>The result of a tool call, linked by <see cref="LlmMessage.ToolCallId"/>.</summary>
    Tool,
}

/// <summary>One message in the conversation sent to the model.</summary>
/// <param name="Role">Role.</param>
/// <param name="Content">Text content (null for an assistant message that only calls tools).</param>
/// <param name="ToolCalls">Tool calls made by the assistant (assistant messages only).</param>
/// <param name="ToolCallId">Which tool call this message answers (tool messages only).</param>
public sealed record LlmMessage(
    LlmRole Role, string? Content, IReadOnlyList<LlmToolCall>? ToolCalls = null, string? ToolCallId = null)
{
    /// <summary>Creates a system message.</summary>
    /// <param name="content">Instructions.</param>
    /// <returns>The message.</returns>
    public static LlmMessage System(string content) => new(LlmRole.System, content);

    /// <summary>Creates a user message.</summary>
    /// <param name="content">Text.</param>
    /// <returns>The message.</returns>
    public static LlmMessage User(string content) => new(LlmRole.User, content);

    /// <summary>Creates an assistant message.</summary>
    /// <param name="content">Text (may be null when only calling tools).</param>
    /// <param name="toolCalls">Tool calls, if any.</param>
    /// <returns>The message.</returns>
    public static LlmMessage Assistant(string? content, IReadOnlyList<LlmToolCall>? toolCalls = null) =>
        new(LlmRole.Assistant, content, toolCalls);

    /// <summary>Creates a tool-result message.</summary>
    /// <param name="toolCallId">Id of the tool call being answered.</param>
    /// <param name="content">Result (JSON).</param>
    /// <returns>The message.</returns>
    public static LlmMessage Tool(string toolCallId, string content) => new(LlmRole.Tool, content, null, toolCallId);
}

/// <summary>A tool call requested by the model.</summary>
/// <param name="Id">Provider-assigned id; echoed back in the tool result.</param>
/// <param name="Name">Tool name.</param>
/// <param name="ArgumentsJson">Arguments as a JSON object string (unvalidated — tools validate).</param>
public sealed record LlmToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>A tool the model may call.</summary>
/// <param name="Name">Tool name (snake_case).</param>
/// <param name="Description">When and why to use it — the model's main guidance.</param>
/// <param name="ParametersSchema">JSON schema of the arguments object.</param>
public sealed record LlmToolDefinition(string Name, string Description, JsonElement ParametersSchema);

/// <summary>One completion request.</summary>
/// <param name="Messages">Conversation so far.</param>
/// <param name="Tools">Available tools.</param>
/// <param name="RequireToolCall">Force the model to call a tool (used to guarantee a structured final answer).</param>
public sealed record LlmRequest(IReadOnlyList<LlmMessage> Messages, IReadOnlyList<LlmToolDefinition> Tools, bool RequireToolCall);

/// <summary>The model's reply.</summary>
/// <param name="Content">Text, if any.</param>
/// <param name="ToolCalls">Tool calls, if any.</param>
/// <param name="Provider">Provider that answered, e.g. <c>Groq</c>.</param>
/// <param name="Model">Model that answered.</param>
/// <param name="PromptTokens">Input tokens billed.</param>
/// <param name="CompletionTokens">Output tokens billed.</param>
public sealed record LlmCompletion(
    string? Content, IReadOnlyList<LlmToolCall> ToolCalls, string Provider, string Model, int PromptTokens, int CompletionTokens);

/// <summary>No LLM provider is configured, or all of them failed. Mapped to HTTP 503.</summary>
public sealed class LlmUnavailableException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What happened (no secrets).</param>
    /// <param name="innerException">Last provider failure, if any.</param>
    public LlmUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
