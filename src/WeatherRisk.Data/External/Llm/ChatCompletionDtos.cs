using System.Text.Json;
using System.Text.Json.Serialization;

namespace WeatherRisk.Data.External.Llm;

/// <summary>OpenAI-compatible <c>POST /chat/completions</c> request (subset we use).</summary>
internal sealed class ChatCompletionRequest
{
    /// <summary>Model id.</summary>
    public required string Model { get; init; }

    /// <summary>Conversation.</summary>
    public required List<ChatMessageDto> Messages { get; init; }

    /// <summary>Tools the model may call.</summary>
    public List<ToolDto>? Tools { get; init; }

    /// <summary><c>required</c> forces a tool call; <c>auto</c> lets the model choose.</summary>
    public string? ToolChoice { get; init; }

    /// <summary>Sampling temperature.</summary>
    public double Temperature { get; init; }

    /// <summary>Output token limit.</summary>
    public int MaxTokens { get; init; }

    /// <summary>Reasoning effort for reasoning models (omitted when null).</summary>
    public string? ReasoningEffort { get; init; }
}

/// <summary>A chat message on the wire.</summary>
internal sealed class ChatMessageDto
{
    /// <summary><c>system</c>, <c>user</c>, <c>assistant</c> or <c>tool</c>.</summary>
    public required string Role { get; init; }

    /// <summary>Text content.</summary>
    public string? Content { get; init; }

    /// <summary>Assistant tool calls.</summary>
    public List<ToolCallDto>? ToolCalls { get; init; }

    /// <summary>Tool call being answered (tool messages).</summary>
    public string? ToolCallId { get; init; }
}

/// <summary>Tool definition on the wire.</summary>
internal sealed class ToolDto
{
    /// <summary>Always <c>function</c>.</summary>
    public string Type { get; init; } = "function";

    /// <summary>Function definition.</summary>
    public required FunctionDto Function { get; init; }
}

/// <summary>Function definition: name, description and JSON-schema parameters.</summary>
internal sealed class FunctionDto
{
    /// <summary>Function name.</summary>
    public required string Name { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Parameters schema.</summary>
    public JsonElement? Parameters { get; init; }
}

/// <summary>Tool call on the wire.</summary>
internal sealed class ToolCallDto
{
    /// <summary>Call id.</summary>
    public string? Id { get; init; }

    /// <summary>Always <c>function</c>.</summary>
    public string Type { get; init; } = "function";

    /// <summary>Function name and JSON-encoded arguments.</summary>
    public required ToolCallFunctionDto Function { get; init; }
}

/// <summary>Called function.</summary>
internal sealed class ToolCallFunctionDto
{
    /// <summary>Function name.</summary>
    public required string Name { get; init; }

    /// <summary>Arguments as a JSON string.</summary>
    public string? Arguments { get; init; }
}

/// <summary>Completion response (subset).</summary>
internal sealed class ChatCompletionResponse
{
    /// <summary>Choices; we use the first.</summary>
    public List<ChoiceDto>? Choices { get; init; }

    /// <summary>Token usage.</summary>
    public UsageDto? Usage { get; init; }
}

/// <summary>One choice.</summary>
internal sealed class ChoiceDto
{
    /// <summary>The assistant message.</summary>
    public ChatMessageDto? Message { get; init; }
}

/// <summary>Token usage.</summary>
internal sealed class UsageDto
{
    /// <summary>Input tokens.</summary>
    public int PromptTokens { get; init; }

    /// <summary>Output tokens.</summary>
    public int CompletionTokens { get; init; }
}

/// <summary>Source-generated serializer: snake_case wire format, nulls omitted.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ChatCompletionRequest))]
[JsonSerializable(typeof(ChatCompletionResponse))]
internal sealed partial class ChatCompletionJsonContext : JsonSerializerContext;
