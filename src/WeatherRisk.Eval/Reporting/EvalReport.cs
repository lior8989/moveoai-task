using WeatherRisk.Eval.Checks;

namespace WeatherRisk.Eval.Reporting;

/// <summary>Everything one evaluation run produced — serialized to JSON and rendered to Markdown.</summary>
/// <param name="StartedAt">Run start (UTC).</param>
/// <param name="BaseUrl">API evaluated.</param>
/// <param name="ModelChecks">Deterministic model expectations.</param>
/// <param name="AgentCases">Agent questions and their checks.</param>
/// <param name="Summary">Aggregates.</param>
internal sealed record EvalReport(
    DateTimeOffset StartedAt,
    string BaseUrl,
    IReadOnlyList<ModelCheckOutcome> ModelChecks,
    IReadOnlyList<AgentCaseOutcome> AgentCases,
    EvalSummary Summary);

/// <summary>Result of one model expectation.</summary>
/// <param name="Id">Check id.</param>
/// <param name="Description">What it expects.</param>
/// <param name="Result">Pass/fail and detail.</param>
internal sealed record ModelCheckOutcome(string Id, string Description, CheckResult Result);

/// <summary>Result of one agent question.</summary>
/// <param name="Id">Case id.</param>
/// <param name="Question">Question asked.</param>
/// <param name="Purpose">What the case tests.</param>
/// <param name="Passed">True when every check passed.</param>
/// <param name="Checks">Individual check results.</param>
/// <param name="Answer">Answer text (or error).</param>
/// <param name="Intent">Intent reported by the agent.</param>
/// <param name="Tools">Tools called, in order.</param>
/// <param name="Model">Provider/model that answered.</param>
/// <param name="LatencyMs">Client-measured round trip.</param>
/// <param name="Tokens">Prompt + completion tokens.</param>
/// <param name="HttpStatus">HTTP status of the chat request.</param>
internal sealed record AgentCaseOutcome(
    string Id,
    string Question,
    string Purpose,
    bool Passed,
    IReadOnlyList<CheckResult> Checks,
    string Answer,
    string? Intent,
    IReadOnlyList<string> Tools,
    string? Model,
    double LatencyMs,
    int Tokens,
    int HttpStatus);

/// <summary>Aggregates over the run.</summary>
/// <param name="ModelChecksPassed">Model checks passed.</param>
/// <param name="ModelChecksTotal">Model checks run.</param>
/// <param name="AgentCasesPassed">Agent cases with every check passing.</param>
/// <param name="AgentCasesTotal">Agent cases run.</param>
/// <param name="AgentChecksPassed">Individual agent checks passed.</param>
/// <param name="AgentChecksTotal">Individual agent checks run.</param>
/// <param name="AverageLatencyMs">Mean agent latency.</param>
/// <param name="P95LatencyMs">95th-percentile agent latency (nearest rank).</param>
/// <param name="TotalTokens">Tokens used by the agent.</param>
/// <param name="ModelsUsed">How many answers each provider/model produced.</param>
/// <param name="LlmFallbacks">LLM fallbacks observed via /api/system/metrics during the run.</param>
internal sealed record EvalSummary(
    int ModelChecksPassed,
    int ModelChecksTotal,
    int AgentCasesPassed,
    int AgentCasesTotal,
    int AgentChecksPassed,
    int AgentChecksTotal,
    double AverageLatencyMs,
    double P95LatencyMs,
    int TotalTokens,
    IReadOnlyDictionary<string, int> ModelsUsed,
    long LlmFallbacks)
{
    /// <summary>True when every model check and every agent case passed.</summary>
    public bool AllPassed => ModelChecksPassed == ModelChecksTotal && AgentCasesPassed == AgentCasesTotal;
}
