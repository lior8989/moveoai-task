using System.Globalization;
using System.Text.Json;
using WeatherRisk.Core.Agent;
using WeatherRisk.Core.Agent.Llm;

namespace WeatherRisk.Api.Tests.Infrastructure;

/// <summary>
/// Deterministic stand-in for the LLM in endpoint tests. Default behaviour mimics a well-behaved model:
/// first call rank_hubs(Midwest, Winter), then submit an answer citing the top hub's score from the tool result.
/// </summary>
public sealed class FakeLlmClient : ILlmClient
{
    private int _requests;

    /// <summary>How the fake behaves.</summary>
    public FakeLlmMode Mode { get; set; } = FakeLlmMode.WellBehaved;

    /// <summary>Number of completion requests received.</summary>
    public int Requests => _requests;

    /// <summary>The messages of the most recent request.</summary>
    public IReadOnlyList<LlmMessage> LastMessages { get; private set; } = [];

    public Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _requests);
        LastMessages = [.. request.Messages];

        return Mode switch
        {
            FakeLlmMode.Unavailable => throw new LlmUnavailableException("All configured LLM providers failed."),
            FakeLlmMode.ProseOnly => Task.FromResult(Completion(content: "I refuse to use tools.")),
            _ => Task.FromResult(WellBehaved(request)),
        };
    }

    private static LlmCompletion WellBehaved(LlmRequest request)
    {
        LlmMessage? toolResult = request.Messages.LastOrDefault(m => m.Role == LlmRole.Tool);
        if (request.Messages[^1].Role == LlmRole.User || toolResult is null)
        {
            return Completion(new LlmToolCall("t1", "rank_hubs", """{"region":"Midwest","hazard":"Winter"}"""));
        }

        JsonElement first = JsonDocument.Parse(toolResult.Content!).RootElement.GetProperty("entries")[0];
        string hub = first.GetProperty("hubId").GetString()!;
        string score = first.GetProperty("score").GetDouble().ToString(CultureInfo.InvariantCulture);
        string answer = $$"""
            {"answer":"**{{hub}}** is most exposed to winter disruption.","intent":"Ranking",
             "findings":[{"hubId":"{{hub}}","metric":"Winter score","value":{{score}},"unit":"score 0-100"}],
             "keyFactors":["Snow days"],"assumptions":["Uncalibrated weights"],"uncertainty":"High data confidence.",
             "followUpQuestions":["Compare it with Chicago?"]}
            """;
        return Completion(new LlmToolCall("s1", AgentPrompt.SubmitAnswerTool, answer));
    }

    private static LlmCompletion Completion(params LlmToolCall[] calls) => new(null, calls, "Fake", "fake-model", 100, 20);

    private static LlmCompletion Completion(string content) => new(content, [], "Fake", "fake-model", 100, 20);
}

/// <summary>Behaviours of <see cref="FakeLlmClient"/>.</summary>
public enum FakeLlmMode
{
    /// <summary>Calls a tool, then submits a valid grounded answer.</summary>
    WellBehaved,

    /// <summary>Every provider failed.</summary>
    Unavailable,

    /// <summary>Never calls tools — the agent eventually gives up.</summary>
    ProseOnly,
}
