using System.ComponentModel;
using System.Text.Json;
using WeatherRisk.Core.Agent;

namespace WeatherRisk.Api.Contracts.Chat;

/// <summary>A question for the assistant.</summary>
/// <param name="Message">The question, e.g. "Which hubs in the Midwest are most exposed to winter disruption?".</param>
/// <param name="ConversationId">Id returned by a previous answer, to ask a follow-up; omit to start a new conversation.</param>
public sealed record ChatRequest(
    [property: Description("The question (1-2000 characters).")] string Message,
    [property: Description("Conversation id from a previous response, for follow-up questions.")] string? ConversationId = null);

/// <summary>The assistant's validated, structured answer plus an audit trail.</summary>
/// <param name="ConversationId">Send this back with follow-up questions.</param>
/// <param name="Answer">Answer text (Markdown) for the analyst.</param>
/// <param name="Intent">Question category.</param>
/// <param name="Findings">Hub numbers cited, each marked verified (found in this turn's tool results) or not.</param>
/// <param name="KeyFactors">Main reasons behind the answer.</param>
/// <param name="Assumptions">Assumptions the answer relies on.</param>
/// <param name="Uncertainty">Confidence and caveats.</param>
/// <param name="FollowUpQuestions">Suggested next questions.</param>
/// <param name="Verification">Grounding summary.</param>
/// <param name="ToolCalls">Deterministic tools the assistant used, in order.</param>
/// <param name="Llm">Which model answered and what it cost.</param>
public sealed record ChatResponse(
    string ConversationId,
    string Answer,
    AnswerIntent Intent,
    IReadOnlyList<FindingResponse> Findings,
    IReadOnlyList<string> KeyFactors,
    IReadOnlyList<string> Assumptions,
    string Uncertainty,
    IReadOnlyList<string> FollowUpQuestions,
    VerificationResponse Verification,
    IReadOnlyList<ToolCallResponse> ToolCalls,
    LlmUsageResponse Llm);

/// <summary>A cited hub number.</summary>
/// <param name="HubId">Hub id.</param>
/// <param name="Metric">What the number measures.</param>
/// <param name="Value">The number.</param>
/// <param name="Unit">Unit.</param>
/// <param name="Verified">True when the hub and value appear in this turn's tool results.</param>
public sealed record FindingResponse(string HubId, string Metric, double Value, string Unit, bool Verified);

/// <summary>Grounding summary.</summary>
/// <param name="AllFindingsGrounded">True when every cited number was found in tool results.</param>
/// <param name="Notes">Explanations for unverified findings.</param>
public sealed record VerificationResponse(bool AllFindingsGrounded, IReadOnlyList<string> Notes);

/// <summary>One tool call.</summary>
/// <param name="Tool">Tool name.</param>
/// <param name="Arguments">Arguments sent by the model.</param>
/// <param name="Succeeded">False when arguments were invalid or the request failed validation.</param>
/// <param name="DurationMs">Execution time, ms.</param>
public sealed record ToolCallResponse(string Tool, JsonElement Arguments, bool Succeeded, double DurationMs);

/// <summary>LLM usage for the question.</summary>
/// <param name="Provider">Provider that produced the final answer.</param>
/// <param name="Model">Model that produced the final answer.</param>
/// <param name="Steps">LLM round-trips.</param>
/// <param name="PromptTokens">Input tokens.</param>
/// <param name="CompletionTokens">Output tokens.</param>
/// <param name="DurationMs">Total time for the question, ms.</param>
public sealed record LlmUsageResponse(string Provider, string Model, int Steps, int PromptTokens, int CompletionTokens, double DurationMs);

/// <summary>Maps agent results to the chat contract.</summary>
public static class ChatMapper
{
    /// <summary>Maps a chat result.</summary>
    /// <param name="result">Agent result.</param>
    /// <returns>Contract.</returns>
    public static ChatResponse ToResponse(this ChatResult result) => new(
        result.ConversationId,
        result.Answer.Answer,
        result.Answer.Intent,
        result.Grounding.Findings.Select(f => new FindingResponse(f.Finding.HubId, f.Finding.Metric, f.Finding.Value, f.Finding.Unit, f.Verified)).ToList(),
        result.Answer.KeyFactors,
        result.Answer.Assumptions,
        result.Answer.Uncertainty,
        result.Answer.FollowUpQuestions,
        new VerificationResponse(result.Grounding.AllGrounded, result.Grounding.Notes),
        result.ToolCalls.Select(t => new ToolCallResponse(t.Tool, ParseArguments(t.ArgumentsJson), t.Succeeded, Math.Round(t.Duration.TotalMilliseconds, 1))).ToList(),
        new LlmUsageResponse(result.Provider, result.Model, result.Steps, result.PromptTokens, result.CompletionTokens,
            Math.Round(result.Duration.TotalMilliseconds, 0)));

    /// <summary>Embeds the model's arguments as JSON when valid, otherwise as a string.</summary>
    private static JsonElement ParseArguments(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(json);
        }
    }
}
