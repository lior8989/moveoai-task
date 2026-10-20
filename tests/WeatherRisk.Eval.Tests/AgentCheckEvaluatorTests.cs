using System.Text.Json;
using WeatherRisk.Eval;
using WeatherRisk.Eval.Checks;

namespace WeatherRisk.Eval.Tests;

public sealed class AgentCheckEvaluatorTests
{
    // A realistic /api/chat response; "New Orleans" uses a narrow no-break space, as LLMs often emit.
    private static readonly JsonElement Response = JsonDocument.Parse("""
        {
          "conversationId": "c1",
          "answer": "1. New Orleans (71.2) leads, then Miami (70.9). Dallas has 3.6 heavy‑rain days/yr; severe storm risk is high. Rated Low overall? No.",
          "intent": "Ranking",
          "findings": [ { "hubId": "new-orleans", "metric": "Composite", "value": 71.2, "unit": "score", "verified": true } ],
          "toolCalls": [ { "tool": "rank_hubs", "arguments": { "region": "South", "top": 3, "hubIds": ["miami","houston"] }, "succeeded": true, "durationMs": 12 } ],
          "verification": { "allFindingsGrounded": true, "notes": [] },
          "llm": { "provider": "Groq", "model": "m", "steps": 2, "promptTokens": 100, "completionTokens": 20, "durationMs": 3000 }
        }
        """).RootElement;

    private readonly FakeApi _api = new(new()
    {
        ["/api/risk/rankings?top=2"] = """{"entries":[{"hubName":"New Orleans Hub"},{"hubName":"Miami Hub"}]}""",
        ["/api/risk/rankings?top=3"] = """{"entries":[{"hubName":"New Orleans Hub"},{"hubName":"Miami Hub"},{"hubName":"Houston Hub"}]}""",
        ["/w"] = """{"days":[{"metric":"heavyRainDays","perYear":3.6}],"score":71.24,"driver":"SevereStorm"}""",
    });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<CheckResult> Run(AgentCheck check, JsonElement? response = null) =>
        AgentCheckEvaluator.EvaluateAsync(check, response ?? Response, _api, Ct);

    [Fact]
    public async Task Intent_MatchesAnyAcceptedValue()
    {
        Assert.True((await Run(new AgentCheck("intent", AnyOf: ["Comparison", "Ranking"]))).Passed);
        Assert.False((await Run(new AgentCheck("intent", AnyOf: ["HubRisk"]))).Passed);
    }

    [Fact]
    public async Task ToolCalled_ArgsAreASubsetMatch_WithArraysAsSets()
    {
        JsonElement args = JsonDocument.Parse("""{"region":"south","hubIds":["houston","miami"]}""").RootElement;
        JsonElement wrong = JsonDocument.Parse("""{"region":"Midwest"}""").RootElement;

        Assert.True((await Run(new AgentCheck("toolCalled", Tool: "rank_hubs"))).Passed);
        Assert.True((await Run(new AgentCheck("toolCalled", Tool: "rank_hubs", Args: args))).Passed);
        Assert.False((await Run(new AgentCheck("toolCalled", Tool: "rank_hubs", Args: wrong))).Passed);
        Assert.False((await Run(new AgentCheck("toolCalled", Tool: "compare_hubs"))).Passed);
    }

    [Fact]
    public async Task NoToolCalls_FailsWhenToolsWereUsed()
    {
        Assert.False((await Run(new AgentCheck("noToolCalls"))).Passed);
    }

    [Fact]
    public async Task Grounded_RequiresAllGroundedAndMinimumFindings()
    {
        Assert.True((await Run(new AgentCheck("grounded", Count: 1))).Passed);
        Assert.False((await Run(new AgentCheck("grounded", Count: 2))).Passed);
    }

    [Fact]
    public async Task MentionsTopRanked_NormalizesUnicodeSpacesAndStripsHubSuffix()
    {
        Assert.True((await Run(new AgentCheck("mentionsTopRanked", Query: "top=2", Count: 2))).Passed);

        CheckResult top3 = await Run(new AgentCheck("mentionsTopRanked", Query: "top=3", Count: 3));
        Assert.False(top3.Passed);
        Assert.Contains("Houston", top3.Detail);
    }

    [Fact]
    public async Task MentionsText_NormalizesNonBreakingHyphens()
    {
        Assert.True((await Run(new AgentCheck("mentionsText", AnyOf: ["heavy-rain"]))).Passed);
        Assert.False((await Run(new AgentCheck("mentionsText", AnyOf: ["hurricane"]))).Passed);
    }

    [Fact]
    public async Task CitesApiValue_AcceptsFindingsOrNumbersInTextWithinTolerance()
    {
        Assert.True((await Run(new AgentCheck("citesApiValue", Url: "/w", Path: "score"))).Passed);               // 71.24 ≈ finding 71.2
        Assert.True((await Run(new AgentCheck("citesApiValue", Url: "/w", Path: "days[metric=heavyRainDays].perYear", Tolerance: 0))).Passed); // "3.6" in text
        Assert.False((await Run(new AgentCheck("citesApiValue", Url: "/w", Path: "score", Tolerance: 0))).Passed);
    }

    [Fact]
    public async Task MentionsApiText_AcceptsHumanizedIdentifier()
    {
        Assert.True((await Run(new AgentCheck("mentionsApiText", Url: "/w", Path: "driver"))).Passed); // "SevereStorm" ↔ "severe storm"
    }

    [Fact]
    public async Task UnknownTypeOrBrokenGroundTruth_FailsGracefully()
    {
        Assert.False((await Run(new AgentCheck("telepathy"))).Passed);
        Assert.False((await Run(new AgentCheck("citesApiValue", Url: "/missing", Path: "x"))).Passed);
    }

    [Theory]
    [InlineData("New Orleans", "New Orleans")]
    [InlineData("heavy‑rain", "heavy-rain")]
    [InlineData("a b–c", "a b-c")]
    [InlineData("we don’t have", "we don't have")]
    public void Normalize_MapsUnicodeSpacesAndDashes(string input, string expected)
    {
        Assert.Equal(expected, AgentCheckEvaluator.Normalize(input));
    }
}
