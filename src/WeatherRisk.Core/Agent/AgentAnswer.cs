using System.ComponentModel;

namespace WeatherRisk.Core.Agent;

/// <summary>
/// The structured final answer the model must produce (via the <c>submit_answer</c> tool). Its JSON schema is
/// generated from this record and enforced by <see cref="AnswerValidator"/> — the contract between the LLM and our code.
/// </summary>
/// <param name="Answer">The answer for the analyst.</param>
/// <param name="Intent">What kind of question this was.</param>
/// <param name="Findings">Hub numbers cited in the answer.</param>
/// <param name="KeyFactors">Main reasons behind the answer.</param>
/// <param name="Assumptions">Assumptions the answer relies on.</param>
/// <param name="Uncertainty">How confident the answer is and why.</param>
/// <param name="FollowUpQuestions">Suggested next questions.</param>
[Description("Final structured answer to the analyst's question.")]
public sealed record AgentAnswer(
    [property: Description("Answer for the analyst in concise Markdown (max ~200 words). Explain the reasoning: drivers, contributions, evidence.")]
    string Answer,
    [property: Description("Question category.")]
    AnswerIntent Intent,
    [property: Description("Every hub number cited in the answer, copied exactly from tool results. Empty if none.")]
    IReadOnlyList<HubFinding> Findings,
    [property: Description("Main reasons behind the answer (short phrases).")]
    IReadOnlyList<string> KeyFactors,
    [property: Description("Assumptions the answer relies on (e.g. uncalibrated weights, data window).")]
    IReadOnlyList<string> Assumptions,
    [property: Description("Confidence and caveats: data confidence/freshness and model limitations.")]
    string Uncertainty,
    [property: Description("Up to 3 useful follow-up questions.")]
    IReadOnlyList<string> FollowUpQuestions);

/// <summary>A number about a hub cited in the answer — checked against tool results.</summary>
/// <param name="HubId">Hub id, e.g. <c>dallas</c>.</param>
/// <param name="Metric">What the number is, e.g. <c>Composite score</c>, <c>Winter score</c>, <c>Snow days %</c>.</param>
/// <param name="Value">The number exactly as in the tool result.</param>
/// <param name="Unit">Unit, e.g. <c>score 0-100</c>, <c>%</c>, <c>days/yr</c>, <c>percentile</c>.</param>
public sealed record HubFinding(
    [property: Description("Hub id exactly as in tool results, e.g. 'dallas'.")]
    string HubId,
    [property: Description("What the number measures, e.g. 'Composite score', 'Winter score', 'Snow days %'.")]
    string Metric,
    [property: Description("The number copied exactly from a tool result.")]
    double Value,
    [property: Description("Unit, e.g. 'score 0-100', '%', 'days/yr', 'percentile'.")]
    string Unit);

/// <summary>Question categories.</summary>
public enum AnswerIntent
{
    /// <summary>Which hubs are most exposed.</summary>
    Ranking,

    /// <summary>Comparing hubs.</summary>
    Comparison,

    /// <summary>One hub's risk and why.</summary>
    HubRisk,

    /// <summary>Weather statistics (counts, percentages of days).</summary>
    WeatherStatistics,

    /// <summary>How the model works.</summary>
    Methodology,

    /// <summary>Anything else within scope.</summary>
    Other,

    /// <summary>Not about hub weather risk — politely declined.</summary>
    OutOfScope,
}
