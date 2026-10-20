using System.Text.Json;
using WeatherRisk.Eval.Checks;
using WeatherRisk.Eval.Reporting;

namespace WeatherRisk.Eval;

/// <summary>Runs the suite against the API: model checks first (fast, deterministic), then agent cases (live LLM).</summary>
internal sealed class EvalRunner
{
    private readonly ApiClient _api;
    private readonly EvalOptions _options;

    /// <summary>Creates the runner.</summary>
    /// <param name="api">API client.</param>
    /// <param name="options">Options.</param>
    public EvalRunner(ApiClient api, EvalOptions options)
    {
        _api = api;
        _options = options;
    }

    /// <summary>Runs the suite.</summary>
    /// <param name="suite">Cases and checks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The report.</returns>
    public async Task<EvalReport> RunAsync(EvalSuite suite, CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;

        var modelOutcomes = new List<ModelCheckOutcome>();
        foreach (ModelCheck check in suite.ModelChecks.Where(c => Selected(c.Id)))
        {
            CheckResult result = await ModelCheckEvaluator.EvaluateAsync(check, _api, cancellationToken);
            modelOutcomes.Add(new ModelCheckOutcome(check.Id, check.Description, result));
            Console.WriteLine($"{(result.Passed ? "PASS" : "FAIL")}  {check.Id,-4} {check.Description} — {result.Detail}");
        }

        var agentOutcomes = new List<AgentCaseOutcome>();
        long fallbacksBefore = 0, fallbacksAfter = 0;
        if (!_options.SkipAgent)
        {
            fallbacksBefore = await LlmFallbacksAsync(cancellationToken);
            var conversations = new Dictionary<string, string>(StringComparer.Ordinal);
            List<AgentCase> cases = suite.AgentCases.Where(c => Selected(c.Id) || (c.FollowUpOf is { } p && Selected(p))).ToList();
            for (int i = 0; i < cases.Count; i++)
            {
                if (i > 0 && _options.DelaySeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.DelaySeconds), cancellationToken);
                }

                AgentCaseOutcome outcome = await RunCaseAsync(cases[i], conversations, cancellationToken);
                agentOutcomes.Add(outcome);
                Console.WriteLine($"{(outcome.Passed ? "PASS" : "FAIL")}  {outcome.Id,-4} {outcome.Question} ({outcome.LatencyMs / 1000:F1} s, {outcome.Model})");
                foreach (CheckResult failed in outcome.Checks.Where(c => !c.Passed))
                {
                    Console.WriteLine($"        ✗ {failed.Name}: {failed.Detail}");
                }
            }

            fallbacksAfter = await LlmFallbacksAsync(cancellationToken);
        }

        return new EvalReport(started, _options.BaseUrl.ToString(), modelOutcomes, agentOutcomes,
            Summarize(modelOutcomes, agentOutcomes, fallbacksAfter - fallbacksBefore));
    }

    private async Task<AgentCaseOutcome> RunCaseAsync(
        AgentCase agentCase, Dictionary<string, string> conversations, CancellationToken cancellationToken)
    {
        string? conversationId = agentCase.FollowUpOf is { } parent ? conversations.GetValueOrDefault(parent) : null;
        if (agentCase.FollowUpOf is not null && conversationId is null)
        {
            return Failed(agentCase, 0, 0, $"Skipped: parent case '{agentCase.FollowUpOf}' did not produce a conversation.");
        }

        ChatOutcome chat = await _api.AskAsync(agentCase.Question, conversationId, cancellationToken);
        if (chat.Body is not { } body)
        {
            return Failed(agentCase, chat.Status, chat.ElapsedMs, $"HTTP {chat.Status}: {chat.Error}");
        }

        conversations[agentCase.Id] = body.GetProperty("conversationId").GetString()!;
        var checks = new List<CheckResult>(agentCase.Checks.Count);
        foreach (AgentCheck check in agentCase.Checks)
        {
            checks.Add(await AgentCheckEvaluator.EvaluateAsync(check, body, _api, cancellationToken));
        }

        JsonElement llm = body.GetProperty("llm");
        return new AgentCaseOutcome(
            agentCase.Id,
            agentCase.Question,
            agentCase.Purpose,
            checks.TrueForAll(c => c.Passed),
            checks,
            body.GetProperty("answer").GetString() ?? string.Empty,
            body.GetProperty("intent").GetString(),
            body.GetProperty("toolCalls").EnumerateArray().Select(t => t.GetProperty("tool").GetString()!).ToList(),
            $"{llm.GetProperty("provider").GetString()}/{llm.GetProperty("model").GetString()}",
            chat.ElapsedMs,
            llm.GetProperty("promptTokens").GetInt32() + llm.GetProperty("completionTokens").GetInt32(),
            chat.Status);
    }

    private static AgentCaseOutcome Failed(AgentCase c, int status, double ms, string error) =>
        new(c.Id, c.Question, c.Purpose, false, [CheckResult.Fail("request", error)], error, null, [], null, ms, 0, status);

    /// <summary>Sum of fallbacks across LLM dependencies, read from the API's metrics.</summary>
    private async Task<long> LlmFallbacksAsync(CancellationToken cancellationToken)
    {
        JsonElement metrics = await _api.GetFreshAsync("/api/system/metrics", cancellationToken);
        return metrics.GetProperty("dependencies").EnumerateArray()
            .Where(d => d.GetProperty("dependency").GetString()!.StartsWith("llm-", StringComparison.Ordinal))
            .Sum(d => d.GetProperty("fallbacksServed").GetInt64());
    }

    private bool Selected(string id) => _options.Only.Count == 0 || _options.Only.Contains(id);

    internal static EvalSummary Summarize(
        IReadOnlyList<ModelCheckOutcome> models, IReadOnlyList<AgentCaseOutcome> agents, long fallbacks)
    {
        double[] latencies = agents.Where(a => a.HttpStatus == 200).Select(a => a.LatencyMs).Order().ToArray();
        double p95 = latencies.Length == 0 ? 0 : latencies[(int)Math.Ceiling(0.95 * latencies.Length) - 1];
        return new EvalSummary(
            models.Count(m => m.Result.Passed),
            models.Count,
            agents.Count(a => a.Passed),
            agents.Count,
            agents.Sum(a => a.Checks.Count(c => c.Passed)),
            agents.Sum(a => a.Checks.Count),
            latencies.Length == 0 ? 0 : latencies.Average(),
            p95,
            agents.Sum(a => a.Tokens),
            agents.Where(a => a.Model is not null).GroupBy(a => a.Model!).ToDictionary(g => g.Key, g => g.Count()),
            fallbacks);
    }
}
