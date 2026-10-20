using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WeatherRisk.Eval.Checks;

/// <summary>
/// Evaluates checks on an agent's <c>/api/chat</c> response. Expected values are resolved from the
/// deterministic API at run time, so cases stay valid when the underlying data is refreshed.
/// </summary>
internal static partial class AgentCheckEvaluator
{
    private const double DefaultTolerance = 0.06;

    /// <summary>Evaluates one check.</summary>
    /// <param name="check">The check.</param>
    /// <param name="response">Chat response JSON.</param>
    /// <param name="api">Ground-truth reader.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pass/fail with detail.</returns>
    public static async Task<CheckResult> EvaluateAsync(
        AgentCheck check, JsonElement response, IApiReader api, CancellationToken cancellationToken)
    {
        string answer = Normalize(Str(response, "answer"));
        try
        {
            return check.Type switch
            {
                "intent" => Intent(check, response),
                "toolCalled" => ToolCalled(check, response),
                "noToolCalls" => NoToolCalls(response),
                "grounded" => Grounded(check, response),
                "mentionsHubs" => MentionsAll($"mentionsHubs", answer, check.Hubs ?? []),
                "mentionsText" => MentionsAny(answer, check.AnyOf ?? []),
                "mentionsTopRanked" => await MentionsTopRankedAsync(check, answer, api, cancellationToken),
                "citesApiValue" => await CitesApiValueAsync(check, response, answer, api, cancellationToken),
                "mentionsApiText" => await MentionsApiTextAsync(check, answer, api, cancellationToken),
                _ => CheckResult.Fail(check.Type, $"unknown check type '{check.Type}'"),
            };
        }
        catch (Exception ex) when (ex is KeyNotFoundException or HttpRequestException or FormatException or InvalidOperationException)
        {
            return CheckResult.Fail(check.Type, ex.Message);
        }
    }

    private static CheckResult Intent(AgentCheck check, JsonElement response)
    {
        string intent = Str(response, "intent");
        IReadOnlyList<string> accepted = check.AnyOf ?? [];
        return accepted.Contains(intent, StringComparer.OrdinalIgnoreCase)
            ? CheckResult.Pass("intent", intent)
            : CheckResult.Fail("intent", $"expected one of [{string.Join(", ", accepted)}], actual {intent}");
    }

    private static CheckResult ToolCalled(AgentCheck check, JsonElement response)
    {
        string name = $"toolCalled({check.Tool})";
        List<JsonElement> calls = response.GetProperty("toolCalls").EnumerateArray()
            .Where(c => Str(c, "tool") == check.Tool && c.GetProperty("succeeded").GetBoolean())
            .ToList();
        if (calls.Count == 0)
        {
            return CheckResult.Fail(name, $"not called successfully; calls: {ToolList(response)}");
        }

        if (check.Args is not { ValueKind: JsonValueKind.Object } expected)
        {
            return CheckResult.Pass(name, ToolList(response));
        }

        return calls.Any(c => ArgsMatch(expected, c.GetProperty("arguments")))
            ? CheckResult.Pass(name, $"args ⊇ {expected.GetRawText()}")
            : CheckResult.Fail(name, $"expected args ⊇ {expected.GetRawText()}, got {string.Join(" | ", calls.Select(c => c.GetProperty("arguments").GetRawText()))}");
    }

    /// <summary>Every expected property must be present with an equal value; arrays compare as sets (case-insensitive).</summary>
    internal static bool ArgsMatch(JsonElement expected, JsonElement actual)
    {
        if (actual.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (JsonProperty property in expected.EnumerateObject())
        {
            JsonElement value = actual.EnumerateObject()
                .FirstOrDefault(p => string.Equals(p.Name, property.Name, StringComparison.OrdinalIgnoreCase)).Value;
            if (value.ValueKind == JsonValueKind.Undefined)
            {
                return false;
            }

            bool equal = property.Value.ValueKind == JsonValueKind.Array && value.ValueKind == JsonValueKind.Array
                ? new HashSet<string>(property.Value.EnumerateArray().Select(JsonPath.Text), StringComparer.OrdinalIgnoreCase)
                    .SetEquals(value.EnumerateArray().Select(JsonPath.Text))
                : ModelCheckEvaluator.Same(JsonPath.Text(value), JsonPath.Text(property.Value));
            if (!equal)
            {
                return false;
            }
        }

        return true;
    }

    private static CheckResult NoToolCalls(JsonElement response) =>
        response.GetProperty("toolCalls").GetArrayLength() == 0
            ? CheckResult.Pass("noToolCalls", "no tools called")
            : CheckResult.Fail("noToolCalls", $"called: {ToolList(response)}");

    private static CheckResult Grounded(AgentCheck check, JsonElement response)
    {
        bool grounded = response.GetProperty("verification").GetProperty("allFindingsGrounded").GetBoolean();
        int findings = response.GetProperty("findings").GetArrayLength();
        int minimum = check.Count ?? 0;
        string detail = $"{findings} findings, all grounded = {grounded}";
        return grounded && findings >= minimum
            ? CheckResult.Pass("grounded", detail)
            : CheckResult.Fail("grounded", $"{detail} (need ≥ {minimum}); notes: {string.Join("; ", response.GetProperty("verification").GetProperty("notes").EnumerateArray().Select(n => n.GetString()))}");
    }

    private static CheckResult MentionsAll(string name, string answer, IReadOnlyList<string> phrases)
    {
        List<string> missing = phrases.Where(p => !answer.Contains(Normalize(p), StringComparison.OrdinalIgnoreCase)).ToList();
        return missing.Count == 0
            ? CheckResult.Pass(name, $"mentions {string.Join(", ", phrases)}")
            : CheckResult.Fail(name, $"missing: {string.Join(", ", missing)}");
    }

    private static CheckResult MentionsAny(string answer, IReadOnlyList<string> phrases)
    {
        string? found = phrases.FirstOrDefault(p => answer.Contains(Normalize(p), StringComparison.OrdinalIgnoreCase));
        return found is not null
            ? CheckResult.Pass("mentionsText", $"found '{found}'")
            : CheckResult.Fail("mentionsText", $"none of [{string.Join(", ", phrases)}] in answer");
    }

    private static async Task<CheckResult> MentionsTopRankedAsync(
        AgentCheck check, string answer, IApiReader api, CancellationToken cancellationToken)
    {
        int count = check.Count ?? 1;
        JsonElement ranking = await api.GetAsync($"/api/risk/rankings?{check.Query}", cancellationToken);
        List<string> expected = ranking.GetProperty("entries").EnumerateArray().Take(count)
            .Select(e => Str(e, "hubName").Replace(" Hub", string.Empty, StringComparison.Ordinal))
            .ToList();
        return MentionsAll($"mentionsTopRanked({check.Query}, {count})", answer, expected);
    }

    private static async Task<CheckResult> CitesApiValueAsync(
        AgentCheck check, JsonElement response, string answer, IApiReader api, CancellationToken cancellationToken)
    {
        string name = $"citesApiValue({check.Path})";
        JsonElement truth = JsonPath.Single(await api.GetAsync(check.Url!, cancellationToken), check.Path!);
        double expected = truth.GetDouble();
        double tolerance = check.Tolerance ?? DefaultTolerance;

        IEnumerable<double> cited = response.GetProperty("findings").EnumerateArray().Select(f => f.GetProperty("value").GetDouble())
            .Concat(NumberPattern().Matches(answer).Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)));
        return cited.Any(v => Math.Abs(v - expected) <= tolerance)
            ? CheckResult.Pass(name, $"cites {expected}")
            : CheckResult.Fail(name, $"expected {expected} (±{tolerance}) in findings or answer");
    }

    private static async Task<CheckResult> MentionsApiTextAsync(
        AgentCheck check, string answer, IApiReader api, CancellationToken cancellationToken)
    {
        string value = JsonPath.Text(JsonPath.Single(await api.GetAsync(check.Url!, cancellationToken), check.Path!));
        string humanized = Humanize(value); // "SevereStorm" may be written "severe storm".
        bool found = answer.Contains(value, StringComparison.OrdinalIgnoreCase)
                     || answer.Contains(humanized, StringComparison.OrdinalIgnoreCase);
        return found
            ? CheckResult.Pass($"mentionsApiText({check.Path})", $"mentions {value}")
            : CheckResult.Fail($"mentionsApiText({check.Path})", $"'{value}' not in answer");
    }

    internal static string Humanize(string identifier) => PascalBoundary().Replace(identifier, "$1 $2");

    /// <summary>
    /// Makes text comparable: LLMs often emit non-breaking/narrow spaces ("New Orleans"), non-breaking
    /// hyphens ("heavy‑rain") and typographic apostrophes ("don’t"); these are mapped to plain
    /// spaces, hyphens and apostrophes.
    /// </summary>
    internal static string Normalize(string text) =>
        Apostrophes().Replace(DashVariants().Replace(UnicodeSpaces().Replace(text, " "), "-"), "'");

    private static string Str(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;

    private static string ToolList(JsonElement response) =>
        string.Join(", ", response.GetProperty("toolCalls").EnumerateArray().Select(c => Str(c, "tool"))) is { Length: > 0 } list ? list : "(none)";

    [GeneratedRegex(@"-?\d+(?:\.\d+)?")]
    private static partial Regex NumberPattern();

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex PascalBoundary();

    [GeneratedRegex(@"[\p{Zs}   ]")]
    private static partial Regex UnicodeSpaces();

    [GeneratedRegex(@"[‐‑‒–—]")]
    private static partial Regex DashVariants();

    [GeneratedRegex(@"[‘’ʼ]")]
    private static partial Regex Apostrophes();
}
