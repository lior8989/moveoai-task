using System.ComponentModel.DataAnnotations;

namespace WeatherRisk.Core.Agent;

/// <summary>Limits of one agent turn, bound from the <c>Agent</c> configuration section.</summary>
public sealed class AgentSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Agent";

    /// <summary>Maximum LLM round-trips per question (default 8): bounds latency, cost and runaway loops.</summary>
    [Range(2, 20)]
    public int MaxSteps { get; init; } = 8;

    /// <summary>Previous question/answer pairs included for follow-ups (default 6): bounds prompt size.</summary>
    [Range(0, 20)]
    public int HistoryTurns { get; init; } = 6;

    /// <summary>Overall deadline for one question in seconds (default 60).</summary>
    [Range(5, 300)]
    public int TimeoutSeconds { get; init; } = 60;

    /// <summary>Maximum question length in characters (default 2000).</summary>
    [Range(10, 10_000)]
    public int MaxMessageLength { get; init; } = 2000;
}
