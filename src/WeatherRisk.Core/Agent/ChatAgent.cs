using System.Diagnostics;
using WeatherRisk.Core.Agent.Llm;
using WeatherRisk.Core.Agent.Tools;
using WeatherRisk.Core.Application;
using WeatherRisk.Core.Domain;

namespace WeatherRisk.Core.Agent;

/// <summary>
/// The conversational agent. Per question it runs a bounded tool-calling loop:
/// <code>
/// [system + history + question] → LLM ─┬─ tool calls → run tools (deterministic) → results back → LLM …
///                                       └─ submit_answer → validate schema → ground numbers → done
/// </code>
/// The LLM decides <i>what</i> to look up and how to explain it; all numbers come from the deterministic
/// tools, and the final answer is schema-validated and grounding-checked before it reaches the analyst.
/// </summary>
/// <remarks>
/// Scoped (tools use the scoped <see cref="HubRiskService"/>). Tool calls within a step run sequentially because
/// they share one DbContext; they are fast (cache-backed) compared with the LLM round-trip.
/// </remarks>
public sealed class ChatAgent
{
    // One schema-repair attempt is usually enough; more just burns the step budget.
    private const int MaxInvalidAnswers = 2;

    private static readonly LlmToolDefinition SubmitAnswerDefinition = new(
        AgentPrompt.SubmitAnswerTool,
        "Submit the final structured answer. Call exactly once, after gathering facts with the other tools.",
        AgentJson.SchemaFor<AgentAnswer>());

    private readonly ILlmClient _llm;
    private readonly Dictionary<string, IAgentTool> _tools;
    private readonly IReadOnlyList<LlmToolDefinition> _definitions;
    private readonly HubRiskService _service;
    private readonly IConversationStore _conversations;
    private readonly AgentSettings _settings;
    private readonly TimeProvider _time;

    /// <summary>Creates the agent.</summary>
    /// <param name="llm">LLM port (with provider fallback behind it).</param>
    /// <param name="tools">Available tools.</param>
    /// <param name="service">Application service (hub catalog and window for the prompt).</param>
    /// <param name="conversations">Conversation memory.</param>
    /// <param name="settings">Turn limits.</param>
    /// <param name="time">Clock.</param>
    public ChatAgent(
        ILlmClient llm,
        IEnumerable<IAgentTool> tools,
        HubRiskService service,
        IConversationStore conversations,
        AgentSettings settings,
        TimeProvider time)
    {
        _llm = llm;
        _tools = tools.ToDictionary(t => t.Definition.Name, StringComparer.Ordinal);
        _definitions = [.. _tools.Values.Select(t => t.Definition), SubmitAnswerDefinition];
        _service = service;
        _conversations = conversations;
        _settings = settings;
        _time = time;
    }

    /// <summary>Answers one question, continuing a conversation when an id is given.</summary>
    /// <param name="message">The analyst's question.</param>
    /// <param name="conversationId">Existing conversation id, or null to start a new one.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Validated answer with audit trail.</returns>
    /// <exception cref="RequestValidationException">When the message is empty or too long.</exception>
    /// <exception cref="LlmUnavailableException">When no LLM provider could answer.</exception>
    /// <exception cref="AgentFailedException">When no valid answer was produced within the step budget or deadline.</exception>
    public async Task<ChatResult> AskAsync(string message, string? conversationId, CancellationToken cancellationToken = default)
    {
        message = message?.Trim() ?? string.Empty;
        if (message.Length == 0 || message.Length > _settings.MaxMessageLength)
        {
            throw new RequestValidationException($"'message' must be 1-{_settings.MaxMessageLength} characters.");
        }

        string id = string.IsNullOrWhiteSpace(conversationId) ? Guid.NewGuid().ToString("N") : conversationId.Trim();
        long started = Stopwatch.GetTimestamp();

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

        try
        {
            List<LlmMessage> messages = await BuildMessagesAsync(id, message, deadline.Token);
            return await RunLoopAsync(id, message, messages, started, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AgentFailedException($"The assistant did not finish within {_settings.TimeoutSeconds} seconds.");
        }
    }

    /// <summary>System prompt + recent history + the new question.</summary>
    private async Task<List<LlmMessage>> BuildMessagesAsync(string conversationId, string message, CancellationToken cancellationToken)
    {
        IReadOnlyList<Hub> hubs = await _service.GetHubsAsync(cancellationToken: cancellationToken);
        IReadOnlyList<ConversationTurn> history = await _conversations.GetAsync(conversationId, cancellationToken);

        var messages = new List<LlmMessage>(2 + (2 * _settings.HistoryTurns) + 8)
        {
            LlmMessage.System(AgentPrompt.Build(hubs, _service.Window, DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime))),
        };

        foreach (ConversationTurn turn in history.TakeLast(_settings.HistoryTurns))
        {
            messages.Add(LlmMessage.User(turn.UserMessage));
            messages.Add(LlmMessage.Assistant(turn.AssistantAnswer));
        }

        messages.Add(LlmMessage.User(message));
        return messages;
    }

    private async Task<ChatResult> RunLoopAsync(
        string conversationId, string message, List<LlmMessage> messages, long started, CancellationToken cancellationToken)
    {
        var evidence = new AnswerValidator.ToolEvidence();
        var trace = new List<ToolCallTrace>();
        int promptTokens = 0, completionTokens = 0, invalidAnswers = 0;

        for (int step = 1; step <= _settings.MaxSteps; step++)
        {
            LlmCompletion completion = await _llm.CompleteAsync(new LlmRequest(messages, _definitions, RequireToolCall: true), cancellationToken);
            promptTokens += completion.PromptTokens;
            completionTokens += completion.CompletionTokens;

            if (completion.ToolCalls.Count == 0)
            {
                // The model answered in prose despite tool_choice=required — remind it of the contract.
                messages.Add(LlmMessage.Assistant(completion.Content));
                messages.Add(LlmMessage.User($"Reply by calling {AgentPrompt.SubmitAnswerTool} with the structured answer."));
                continue;
            }

            messages.Add(LlmMessage.Assistant(completion.Content, completion.ToolCalls));
            foreach (LlmToolCall call in completion.ToolCalls)
            {
                if (call.Name == AgentPrompt.SubmitAnswerTool)
                {
                    if (AnswerValidator.TryParse(call.ArgumentsJson, out AgentAnswer? answer, out string? error))
                    {
                        GroundingReport grounding = AnswerValidator.Ground(answer!, evidence);
                        await _conversations.AppendAsync(
                            conversationId, new ConversationTurn(message, answer!.Answer, _time.GetUtcNow()), cancellationToken);
                        return new ChatResult(
                            conversationId, answer, grounding, trace, completion.Provider, completion.Model, step,
                            promptTokens, completionTokens, Stopwatch.GetElapsedTime(started));
                    }

                    if (++invalidAnswers > MaxInvalidAnswers)
                    {
                        throw new AgentFailedException($"The assistant returned an invalid answer: {error}");
                    }

                    messages.Add(LlmMessage.Tool(call.Id, $"{{\"error\":{System.Text.Json.JsonSerializer.Serialize(error)}}}"));
                    continue;
                }

                messages.Add(LlmMessage.Tool(call.Id, await RunToolAsync(call, evidence, trace, cancellationToken)));
            }
        }

        throw new AgentFailedException($"The assistant did not produce an answer within {_settings.MaxSteps} steps.");
    }

    /// <summary>Executes one tool call, recording evidence for grounding and a trace entry.</summary>
    private async Task<string> RunToolAsync(
        LlmToolCall call, AnswerValidator.ToolEvidence evidence, List<ToolCallTrace> trace, CancellationToken cancellationToken)
    {
        long start = Stopwatch.GetTimestamp();
        if (!_tools.TryGetValue(call.Name, out IAgentTool? tool))
        {
            trace.Add(new ToolCallTrace(call.Name, call.ArgumentsJson, false, Stopwatch.GetElapsedTime(start)));
            return $"{{\"error\":\"Unknown tool '{call.Name}'. Available: {string.Join(", ", _tools.Keys)}, {AgentPrompt.SubmitAnswerTool}.\"}}";
        }

        ToolResult result = await tool.InvokeAsync(call.ArgumentsJson, cancellationToken);
        if (result.Succeeded)
        {
            AnswerValidator.Collect(result.Json, evidence);
        }

        trace.Add(new ToolCallTrace(call.Name, call.ArgumentsJson, result.Succeeded, Stopwatch.GetElapsedTime(start)));
        return result.Json;
    }
}
