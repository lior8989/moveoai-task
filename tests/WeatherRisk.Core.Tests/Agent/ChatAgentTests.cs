using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using WeatherRisk.Core.Agent;
using WeatherRisk.Core.Agent.Llm;
using WeatherRisk.Core.Agent.Tools;
using WeatherRisk.Core.Application;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Tests.Agent;

/// <summary>Agent loop with a scripted LLM and real tools/service over fakes.</summary>
public sealed class ChatAgentTests
{
    private readonly HubRiskService _service = ServiceFactory.Create(ServiceFactory.DefaultProvider());
    private readonly MemoryStore _store = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ChatAgent CreateAgent(ScriptedLlm llm, AgentSettings? settings = null) => new(
        llm,
        [
            new ListHubsTool(_service), new RankHubsTool(_service), new CompareHubsTool(_service), new GetHubRiskTool(_service),
            new GetWeatherStatisticsTool(_service),
            new DescribeRiskModelTool(new RiskModelDescriber(new ScoringSettings(), new WeatherThresholds()), _service),
        ],
        _service,
        _store,
        settings ?? new AgentSettings(),
        _time);

    /// <summary>A valid submit_answer citing the first ranked hub's score from the latest tool result.</summary>
    private static LlmToolCall SubmitFromLastRanking(LlmRequest request, string id = "s1")
    {
        // Latest ranking result (later tool messages may be validation feedback).
        JsonElement tool = JsonDocument.Parse(
            request.Messages.Last(m => m.Role == LlmRole.Tool && m.Content!.Contains("\"entries\"")).Content!).RootElement;
        JsonElement first = tool.GetProperty("entries")[0];
        string hub = first.GetProperty("hubId").GetString()!;
        double score = first.GetProperty("score").GetDouble();
        return new LlmToolCall(id, AgentPrompt.SubmitAnswerTool, Answer($$"""[{"hubId":"{{hub}}","metric":"Winter score","value":{{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"unit":"score"}]"""));
    }

    private static string Answer(string findings = "[]") => $$"""
        {"answer":"Minneapolis is most exposed.","intent":"Ranking","findings":{{findings}},"keyFactors":["Snow days"],
         "assumptions":["Uncalibrated weights"],"uncertainty":"High confidence.","followUpQuestions":["Compare with Chicago?"]}
        """;

    private static readonly LlmToolCall RankMidwestWinter = new("t1", "rank_hubs", """{"region":"Midwest","hazard":"Winter"}""");

    [Fact]
    public async Task AskAsync_ToolThenAnswer_ReturnsValidatedGroundedResultAndStoresTurn()
    {
        var llm = new ScriptedLlm((req, step) => step == 1 ? Calls(RankMidwestWinter) : Calls(SubmitFromLastRanking(req)));

        ChatResult result = await CreateAgent(llm).AskAsync("Which Midwest hubs are most exposed to winter?", null, Ct);

        Assert.Equal(AnswerIntent.Ranking, result.Answer.Intent);
        Assert.True(result.Grounding.AllGrounded);
        Assert.Equal("minneapolis", result.Grounding.Findings.Single().Finding.HubId);
        Assert.Equal(["rank_hubs"], result.ToolCalls.Select(t => t.Tool));
        Assert.True(result.ToolCalls[0].Succeeded);
        Assert.Equal((2, 200, 20), (result.Steps, result.PromptTokens, result.CompletionTokens));
        Assert.Equal(("Test", "test-model"), (result.Provider, result.Model));
        Assert.Single(await _store.GetAsync(result.ConversationId, Ct));
    }

    [Fact]
    public async Task AskAsync_EveryRequest_ForcesToolCallAndOffersAllToolsPlusSubmitAnswer()
    {
        var llm = new ScriptedLlm((req, step) => step == 1 ? Calls(RankMidwestWinter) : Calls(SubmitFromLastRanking(req)));

        await CreateAgent(llm).AskAsync("q", null, Ct);

        Assert.All(llm.Requests, r => Assert.True(r.RequireToolCall));
        Assert.Equal(7, llm.Requests[0].Tools.Count);
        Assert.Contains(llm.Requests[0].Tools, t => t.Name == AgentPrompt.SubmitAnswerTool);
        Assert.Contains("minneapolis: ", llm.Requests[0].Messages[0].Content); // Hub catalog in the system prompt.
        Assert.Contains("'last year' means 2025", llm.Requests[0].Messages[0].Content);
    }

    [Fact]
    public async Task AskAsync_FollowUp_IncludesPreviousTurnInPrompt()
    {
        var llm = new ScriptedLlm((req, step) => step % 2 == 1 ? Calls(RankMidwestWinter) : Calls(SubmitFromLastRanking(req)));
        ChatAgent agent = CreateAgent(llm);
        ChatResult first = await agent.AskAsync("First question", null, Ct);

        await agent.AskAsync("And Chicago?", first.ConversationId, Ct);

        IReadOnlyList<LlmMessage> followUp = llm.Requests[2].Messages;
        Assert.Equal(("First question", "Minneapolis is most exposed."), (followUp[1].Content, followUp[2].Content));
        Assert.Equal("And Chicago?", followUp[3].Content);
        Assert.Equal(2, (await _store.GetAsync(first.ConversationId, Ct)).Count);
    }

    [Fact]
    public async Task AskAsync_InvalidAnswer_IsSentBackAndRepaired()
    {
        var llm = new ScriptedLlm((req, step) => step switch
        {
            1 => Calls(RankMidwestWinter),
            2 => Calls(new LlmToolCall("bad", AgentPrompt.SubmitAnswerTool, """{"answer":"incomplete"}""")),
            _ => Calls(SubmitFromLastRanking(req)),
        });

        ChatResult result = await CreateAgent(llm).AskAsync("q", null, Ct);

        Assert.Equal(3, result.Steps);
        LlmMessage feedback = llm.Requests[2].Messages.Last();
        Assert.Equal((LlmRole.Tool, "bad"), (feedback.Role, feedback.ToolCallId));
        Assert.Contains("do not match the schema", feedback.Content);
    }

    [Fact]
    public async Task AskAsync_InventedNumber_IsFlaggedNotTrusted()
    {
        var llm = new ScriptedLlm((_, step) => step == 1
            ? Calls(RankMidwestWinter)
            : Calls(new LlmToolCall("s", AgentPrompt.SubmitAnswerTool, Answer("""[{"hubId":"minneapolis","metric":"Winter score","value":99.9,"unit":"score"}]"""))));

        ChatResult result = await CreateAgent(llm).AskAsync("q", null, Ct);

        Assert.False(result.Grounding.AllGrounded);
        Assert.False(result.Grounding.Findings.Single().Verified);
        Assert.Contains("99.9", result.Grounding.Notes.Single());
    }

    [Fact]
    public async Task AskAsync_ProseInsteadOfToolCall_IsNudgedToSubmitAnswer()
    {
        var llm = new ScriptedLlm((req, step) => step switch
        {
            1 => new LlmCompletion("Here is my answer in prose.", [], "Test", "test-model", 10, 1),
            2 => Calls(RankMidwestWinter),
            _ => Calls(SubmitFromLastRanking(req)),
        });

        ChatResult result = await CreateAgent(llm).AskAsync("q", null, Ct);

        Assert.Equal(3, result.Steps);
        Assert.Contains(AgentPrompt.SubmitAnswerTool, llm.Requests[1].Messages.Last().Content);
    }

    [Fact]
    public async Task AskAsync_UnknownTool_ReturnsErrorToModelAndContinues()
    {
        var llm = new ScriptedLlm((req, step) => step switch
        {
            1 => Calls(new LlmToolCall("x", "json", "{}")),
            2 => Calls(RankMidwestWinter),
            _ => Calls(SubmitFromLastRanking(req)),
        });

        ChatResult result = await CreateAgent(llm).AskAsync("q", null, Ct);

        Assert.Contains("Unknown tool 'json'", llm.Requests[1].Messages.Last().Content);
        Assert.False(result.ToolCalls[0].Succeeded);
    }

    [Fact]
    public async Task AskAsync_NeverAnswers_FailsAfterMaxSteps()
    {
        var llm = new ScriptedLlm((_, _) => Calls(RankMidwestWinter));

        var ex = await Assert.ThrowsAsync<AgentFailedException>(
            () => CreateAgent(llm, new AgentSettings { MaxSteps = 3 }).AskAsync("q", null, Ct));

        Assert.Contains("3 steps", ex.Message);
        Assert.Equal(3, llm.Requests.Count);
    }

    [Fact]
    public async Task AskAsync_RepeatedlyInvalidAnswers_Fail()
    {
        var llm = new ScriptedLlm((_, _) => Calls(new LlmToolCall("b", AgentPrompt.SubmitAnswerTool, "{}")));

        await Assert.ThrowsAsync<AgentFailedException>(() => CreateAgent(llm).AskAsync("q", null, Ct));
    }

    [Fact]
    public async Task AskAsync_LlmTooSlow_FailsWithTimeoutMessage()
    {
        var llm = new ScriptedLlm(async (_, _, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Calls();
        });

        var ex = await Assert.ThrowsAsync<AgentFailedException>(
            () => CreateAgent(llm, new AgentSettings { TimeoutSeconds = 1 }).AskAsync("q", null, Ct));

        Assert.Contains("1 seconds", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AskAsync_EmptyMessage_ThrowsValidation(string message)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => CreateAgent(new ScriptedLlm((_, _) => Calls())).AskAsync(message, null, Ct));
    }

    [Fact]
    public async Task AskAsync_TooLongMessage_ThrowsValidation()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => CreateAgent(new ScriptedLlm((_, _) => Calls()), new AgentSettings { MaxMessageLength = 10 }).AskAsync(new string('x', 11), null, Ct));
    }

    private static LlmCompletion Calls(params LlmToolCall[] calls) => new(null, calls, "Test", "test-model", 100, 10);

    /// <summary>LLM fake driven by a script of (request, 1-based step) → completion; records every request.</summary>
    private sealed class ScriptedLlm : ILlmClient
    {
        private readonly Func<LlmRequest, int, CancellationToken, Task<LlmCompletion>> _script;

        public ScriptedLlm(Func<LlmRequest, int, LlmCompletion> script)
            : this((r, s, _) => Task.FromResult(script(r, s)))
        {
        }

        public ScriptedLlm(Func<LlmRequest, int, CancellationToken, Task<LlmCompletion>> script) => _script = script;

        public List<LlmRequest> Requests { get; } = [];

        public Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            // Snapshot: the agent keeps appending to the same message list.
            Requests.Add(request with { Messages = [.. request.Messages] });
            return _script(request, Requests.Count, cancellationToken);
        }
    }

    private sealed class MemoryStore : IConversationStore
    {
        private readonly Dictionary<string, List<ConversationTurn>> _turns = [];

        public Task<IReadOnlyList<ConversationTurn>> GetAsync(string conversationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ConversationTurn>>(_turns.TryGetValue(conversationId, out var t) ? [.. t] : []);

        public Task AppendAsync(string conversationId, ConversationTurn turn, CancellationToken cancellationToken = default)
        {
            if (!_turns.TryGetValue(conversationId, out var list))
            {
                _turns[conversationId] = list = [];
            }

            list.Add(turn);
            return Task.CompletedTask;
        }
    }
}
