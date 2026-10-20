namespace WeatherRisk.Core.Agent;

/// <summary>Outcome of one question: the validated answer plus everything needed to audit it.</summary>
/// <param name="ConversationId">Conversation id to send with follow-up questions.</param>
/// <param name="Answer">Validated structured answer.</param>
/// <param name="Grounding">Which cited numbers were verified against tool results.</param>
/// <param name="ToolCalls">Tools the model called, in order.</param>
/// <param name="Provider">LLM provider that produced the final answer.</param>
/// <param name="Model">LLM model that produced the final answer.</param>
/// <param name="Steps">LLM round-trips used.</param>
/// <param name="PromptTokens">Total input tokens.</param>
/// <param name="CompletionTokens">Total output tokens.</param>
/// <param name="Duration">Wall-clock time for the question.</param>
public sealed record ChatResult(
    string ConversationId,
    AgentAnswer Answer,
    GroundingReport Grounding,
    IReadOnlyList<ToolCallTrace> ToolCalls,
    string Provider,
    string Model,
    int Steps,
    int PromptTokens,
    int CompletionTokens,
    TimeSpan Duration);

/// <summary>One tool call made by the model.</summary>
/// <param name="Tool">Tool name.</param>
/// <param name="ArgumentsJson">Arguments as sent by the model.</param>
/// <param name="Succeeded">False when the arguments were invalid or the request failed validation.</param>
/// <param name="Duration">Execution time.</param>
public sealed record ToolCallTrace(string Tool, string ArgumentsJson, bool Succeeded, TimeSpan Duration);

/// <summary>The model didn't produce a valid answer within the step budget. Mapped to HTTP 502.</summary>
public sealed class AgentFailedException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What went wrong.</param>
    public AgentFailedException(string message)
        : base(message)
    {
    }
}
