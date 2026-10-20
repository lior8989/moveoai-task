using System.Text.Json;
using System.Text.Json.Serialization;

namespace WeatherRisk.Eval;

/// <summary>The evaluation set (loaded from <c>eval-cases.json</c>).</summary>
/// <param name="ModelChecks">Deterministic expectations about the risk model, checked via REST (no LLM).</param>
/// <param name="AgentCases">Questions for the agent with checks on its structured answers.</param>
internal sealed record EvalSuite(IReadOnlyList<ModelCheck> ModelChecks, IReadOnlyList<AgentCase> AgentCases)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, // Typos in case files fail loudly.
    };

    /// <summary>Loads and validates a suite file.</summary>
    /// <param name="path">Path to the JSON file.</param>
    /// <returns>The suite.</returns>
    /// <exception cref="InvalidDataException">When the file is malformed or references unknown cases.</exception>
    public static EvalSuite Load(string path)
    {
        EvalSuite suite = JsonSerializer.Deserialize<EvalSuite>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"'{path}' is empty.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (AgentCase c in suite.AgentCases)
        {
            if (c.FollowUpOf is { } parent && !ids.Contains(parent))
            {
                throw new InvalidDataException($"Case '{c.Id}' follows up '{parent}', which must appear earlier in the file.");
            }

            if (!ids.Add(c.Id))
            {
                throw new InvalidDataException($"Duplicate case id '{c.Id}'.");
            }
        }

        return suite;
    }
}

/// <summary>A deterministic expectation: the value at <see cref="Path"/> in the response of <see cref="Url"/> must satisfy <see cref="Expect"/>.</summary>
/// <param name="Id">Stable id, e.g. <c>M1</c>.</param>
/// <param name="Description">What the expectation means in domain terms.</param>
/// <param name="Url">API path to GET, e.g. <c>/api/risk/rankings?region=Midwest&amp;hazard=Winter</c>.</param>
/// <param name="Path">Value path in the JSON response (see <see cref="JsonPath"/>).</param>
/// <param name="Expect">The condition.</param>
internal sealed record ModelCheck(string Id, string Description, string Url, string Path, Expectation Expect);

/// <summary>Condition on a resolved value. Exactly one member should be set.</summary>
/// <param name="EqualTo">Equal to this value (numbers compared numerically, strings case-insensitively).</param>
/// <param name="AnyOf">Equal to one of these values.</param>
/// <param name="Between">Number within [min, max].</param>
/// <param name="AtLeast">Number ≥ this.</param>
/// <param name="GreaterThan">Number greater than another resolved value.</param>
/// <param name="AllEqual">Every value matched by a wildcard path equals this.</param>
internal sealed record Expectation(
    string? EqualTo = null,
    IReadOnlyList<string>? AnyOf = null,
    IReadOnlyList<double>? Between = null,
    double? AtLeast = null,
    ValueRef? GreaterThan = null,
    string? AllEqual = null);

/// <summary>A value taken from another API response.</summary>
/// <param name="Url">API path to GET.</param>
/// <param name="Path">Value path in the response.</param>
internal sealed record ValueRef(string Url, string Path);

/// <summary>A question for the agent with checks on its answer.</summary>
/// <param name="Id">Stable id, e.g. <c>A1</c>.</param>
/// <param name="Question">What the analyst asks.</param>
/// <param name="Purpose">What the case tests.</param>
/// <param name="Checks">Checks on the structured answer.</param>
/// <param name="FollowUpOf">Id of an earlier case whose conversation this question continues.</param>
internal sealed record AgentCase(string Id, string Question, string Purpose, IReadOnlyList<AgentCheck> Checks, string? FollowUpOf = null);

/// <summary>
/// One check on an agent answer. <see cref="Type"/> selects the check; other members are its parameters:
/// <c>intent</c>(anyOf) · <c>toolCalled</c>(tool, args?) · <c>noToolCalls</c> · <c>grounded</c>(count? = min findings) ·
/// <c>mentionsHubs</c>(hubs) · <c>mentionsTopRanked</c>(query, count) · <c>citesApiValue</c>(url, path, tolerance?) ·
/// <c>mentionsApiText</c>(url, path) · <c>mentionsText</c>(anyOf).
/// </summary>
/// <param name="Type">Check type.</param>
/// <param name="AnyOf">Accepted values/phrases.</param>
/// <param name="Tool">Tool name.</param>
/// <param name="Args">Expected subset of tool arguments.</param>
/// <param name="Hubs">Hub names that must be mentioned.</param>
/// <param name="Query">Ranking query string, e.g. <c>region=Midwest&amp;hazard=Winter</c>.</param>
/// <param name="Count">Number of top hubs / minimum findings.</param>
/// <param name="Url">API path for ground truth.</param>
/// <param name="Path">Value path in the ground-truth response.</param>
/// <param name="Tolerance">Allowed numeric difference (default 0.06).</param>
internal sealed record AgentCheck(
    string Type,
    IReadOnlyList<string>? AnyOf = null,
    string? Tool = null,
    JsonElement? Args = null,
    IReadOnlyList<string>? Hubs = null,
    string? Query = null,
    int? Count = null,
    string? Url = null,
    string? Path = null,
    double? Tolerance = null);
