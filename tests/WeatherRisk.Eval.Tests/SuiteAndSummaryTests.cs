using WeatherRisk.Eval;
using WeatherRisk.Eval.Checks;
using WeatherRisk.Eval.Reporting;

namespace WeatherRisk.Eval.Tests;

public sealed class SuiteAndSummaryTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"eval-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_file);

    [Fact]
    public void Load_ShippedSuite_IsValidAndCoversTheBriefQuestions()
    {
        EvalSuite suite = EvalSuite.Load(Path.Combine(AppContext.BaseDirectory, "eval-cases.json"));

        Assert.InRange(suite.AgentCases.Count, 10, 20);
        Assert.NotEmpty(suite.ModelChecks);
        string questions = string.Join("\n", suite.AgentCases.Select(c => c.Question));
        Assert.Contains("Midwest", questions);
        Assert.Contains("Miami and Houston", questions);
        Assert.Contains("Denver last year", questions);
        Assert.Contains("Dallas", questions);
        Assert.Contains(suite.AgentCases, c => c.FollowUpOf is not null);
        Assert.All(suite.AgentCases, c => Assert.NotEmpty(c.Checks));
    }

    [Fact]
    public void Load_UnknownProperty_FailsLoudly()
    {
        File.WriteAllText(_file, """{"modelChecks":[],"agentCases":[],"typo":1}""");

        Assert.ThrowsAny<Exception>(() => EvalSuite.Load(_file));
    }

    [Fact]
    public void Load_FollowUpOfLaterOrUnknownCase_IsRejected()
    {
        File.WriteAllText(_file, """{"modelChecks":[],"agentCases":[{"id":"A2","question":"q","purpose":"p","checks":[],"followUpOf":"A1"}]}""");

        Assert.Throws<InvalidDataException>(() => EvalSuite.Load(_file));
    }

    [Fact]
    public void Summarize_ComputesPassRatesLatencyAndModelUsage()
    {
        ModelCheckOutcome[] models = [new("M1", "d", CheckResult.Pass("M1", "ok")), new("M2", "d", CheckResult.Fail("M2", "x"))];
        AgentCaseOutcome Case(string id, bool passed, double ms, string? model, int status = 200) =>
            new(id, "q", "p", passed, [new CheckResult("c", passed, "")], "a", "Ranking", [], model, ms, 100, status);
        AgentCaseOutcome[] agents = [Case("A1", true, 1000, "Groq/a"), Case("A2", true, 3000, "Groq/a"), Case("A3", false, 9000, null, 503)];

        EvalSummary s = EvalRunner.Summarize(models, agents, fallbacks: 4);

        Assert.Equal((1, 2, 2, 3), (s.ModelChecksPassed, s.ModelChecksTotal, s.AgentCasesPassed, s.AgentCasesTotal));
        Assert.Equal(2000, s.AverageLatencyMs); // Failed HTTP requests excluded from latency.
        Assert.Equal(3000, s.P95LatencyMs);
        Assert.Equal(2, s.ModelsUsed["Groq/a"]);
        Assert.Equal(4, s.LlmFallbacks);
        Assert.False(s.AllPassed);
    }

    [Fact]
    public void Options_ParseAllFlags_AndRejectUnknown()
    {
        EvalOptions o = EvalOptions.Parse(["--base-url", "http://h:1", "--skip-agent", "--delay", "3", "--only", "A1, M2"]);

        Assert.Equal(("http://h:1/", true, 3.0), (o.BaseUrl.ToString(), o.SkipAgent, o.DelaySeconds));
        Assert.True(o.Only.SetEquals(["A1", "M2"]));
        Assert.Throws<ArgumentException>(() => EvalOptions.Parse(["--nope"]));
    }

    [Fact]
    public void Markdown_ContainsSummaryAndPerCaseDetails()
    {
        var report = new EvalReport(
            DateTimeOffset.UnixEpoch, "http://localhost:5050/",
            [new ModelCheckOutcome("M1", "Minneapolis leads winter", CheckResult.Pass("M1", "minneapolis = minneapolis"))],
            [new AgentCaseOutcome("A1", "Which hubs?", "brief", true, [CheckResult.Pass("intent", "Ranking")], "Answer text", "Ranking", ["rank_hubs"], "Groq/x", 3100, 500, 200)],
            EvalRunner.Summarize([], [], 0));

        string md = ReportWriter.ToMarkdown(report);

        Assert.Contains("# Evaluation report", md);
        Assert.Contains("Minneapolis leads winter", md);
        Assert.Contains("| ✅ | A1 | Which hubs? | rank_hubs | Groq/x | 3.1 s | 1/1 |", md);
        Assert.Contains("Answer text", md);
    }
}
