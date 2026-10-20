using System.Text.Json;
using System.Text.Json.Nodes;

namespace WeatherRisk.Core.Agent;

/// <summary>
/// Enforces the answer contract in two steps:
/// <list type="number">
/// <item><b>Schema</b> — strict parse into <see cref="AgentAnswer"/> plus semantic limits. Failures go back to the model.</item>
/// <item><b>Grounding</b> — every cited number must appear in a tool result from this turn; unknown hubs and
/// unsupported numbers are flagged, so the LLM can't present invented scores as facts.</item>
/// </list>
/// </summary>
public static class AnswerValidator
{
    /// <summary>Maximum answer length in characters.</summary>
    public const int MaxAnswerLength = 4000;

    /// <summary>Maximum number of follow-up suggestions.</summary>
    public const int MaxFollowUps = 3;

    /// <summary>Maximum number of findings.</summary>
    public const int MaxFindings = 30;

    // Tool values have ≤ 2 decimals; models often round to 1 → accept a half-step at 1 decimal.
    private const double Tolerance = 0.051;

    /// <summary>Parses and checks the model's <c>submit_answer</c> arguments.</summary>
    /// <param name="argumentsJson">Arguments JSON from the tool call.</param>
    /// <param name="answer">The parsed answer when valid.</param>
    /// <param name="error">Explanation for the model when invalid.</param>
    /// <returns>True when the answer satisfies the schema and limits.</returns>
    public static bool TryParse(string argumentsJson, out AgentAnswer? answer, out string? error)
    {
        answer = null;
        try
        {
            answer = JsonSerializer.Deserialize<AgentAnswer>(argumentsJson, AgentJson.Strict);
        }
        catch (JsonException ex)
        {
            error = $"submit_answer arguments do not match the schema: {ex.Message}";
            return false;
        }

        error = answer switch
        {
            null => "submit_answer arguments were empty.",
            { Answer: var text } when string.IsNullOrWhiteSpace(text) => "'answer' must not be empty.",
            { Answer.Length: > MaxAnswerLength } => $"'answer' must be at most {MaxAnswerLength} characters.",
            { Uncertainty: var u } when string.IsNullOrWhiteSpace(u) => "'uncertainty' must not be empty.",
            { FollowUpQuestions.Count: > MaxFollowUps } => $"'followUpQuestions' may contain at most {MaxFollowUps} items.",
            { Findings.Count: > MaxFindings } => $"'findings' may contain at most {MaxFindings} items.",
            _ => null,
        };

        if (error is not null)
        {
            answer = null;
            return false;
        }

        return true;
    }

    /// <summary>Checks each finding against the numbers and hub ids seen in this turn's tool results.</summary>
    /// <param name="answer">Parsed answer.</param>
    /// <param name="evidence">Numbers and hub ids collected from tool results.</param>
    /// <returns>Per-finding verdicts plus notes for anything unverified.</returns>
    public static GroundingReport Ground(AgentAnswer answer, ToolEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(evidence);

        var checks = new List<FindingCheck>(answer.Findings.Count);
        var notes = new List<string>();
        foreach (HubFinding finding in answer.Findings)
        {
            bool knownHub = evidence.HubIds.Contains(finding.HubId);
            bool knownNumber = evidence.Contains(finding.Value);
            checks.Add(new FindingCheck(finding, knownHub && knownNumber));

            if (!knownHub)
            {
                notes.Add($"'{finding.HubId}' was not returned by any tool in this turn.");
            }
            else if (!knownNumber)
            {
                notes.Add($"{finding.HubId} {finding.Metric} = {finding.Value} does not match any tool result.");
            }
        }

        return new GroundingReport(checks, notes);
    }

    /// <summary>Collects the evidence (numbers and hub ids) contained in a tool result.</summary>
    /// <param name="toolResultJson">Tool result JSON.</param>
    /// <param name="evidence">Accumulator to add to.</param>
    public static void Collect(string toolResultJson, ToolEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(toolResultJson);
        }
        catch (JsonException)
        {
            return; // Tool results are always JSON; be defensive anyway.
        }

        Walk(root, propertyName: null, evidence);
    }

    private static void Walk(JsonNode? node, string? propertyName, ToolEvidence evidence)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach ((string name, JsonNode? child) in obj)
                {
                    Walk(child, name, evidence);
                }

                break;
            case JsonArray array:
                foreach (JsonNode? item in array)
                {
                    Walk(item, propertyName, evidence);
                }

                break;
            case JsonValue value when value.TryGetValue(out double number):
                evidence.AddNumber(number);
                break;
            case JsonValue value when value.TryGetValue(out string? text) && propertyName is "hubId" or "id":
                evidence.HubIds.Add(text!);
                break;
        }
    }

    /// <summary>Numbers and hub ids observed in tool results during one turn.</summary>
    public sealed class ToolEvidence
    {
        private readonly List<double> _numbers = [];

        /// <summary>Hub ids that appeared in tool results.</summary>
        public HashSet<string> HubIds { get; } = new(StringComparer.Ordinal);

        /// <summary>Adds a number.</summary>
        /// <param name="number">Number from a tool result.</param>
        public void AddNumber(double number) => _numbers.Add(number);

        /// <summary>
        /// Whether a cited value matches a tool number: within ±0.05, or the tool number rounded to an
        /// integer when the model cited an integer (e.g. "71" for 70.8).
        /// </summary>
        /// <param name="value">Cited value.</param>
        /// <returns>True when supported by a tool result.</returns>
        public bool Contains(double value)
        {
            bool citedInteger = Math.Abs(value - Math.Round(value)) < 1e-9;
            foreach (double n in _numbers)
            {
                if (Math.Abs(n - value) <= Tolerance || (citedInteger && Math.Abs(Math.Round(n) - value) < 1e-9))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

/// <summary>Grounding verdict for one finding.</summary>
/// <param name="Finding">The finding.</param>
/// <param name="Verified">True when the hub and number both appear in this turn's tool results.</param>
public sealed record FindingCheck(HubFinding Finding, bool Verified);

/// <summary>Grounding verdicts for an answer.</summary>
/// <param name="Findings">Per-finding verdicts.</param>
/// <param name="Notes">Explanations for unverified findings.</param>
public sealed record GroundingReport(IReadOnlyList<FindingCheck> Findings, IReadOnlyList<string> Notes)
{
    /// <summary>True when every finding is verified (vacuously true with no findings).</summary>
    public bool AllGrounded => Findings.All(f => f.Verified);
}
