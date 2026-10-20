using System.Globalization;
using System.Text.Json;

namespace WeatherRisk.Eval.Checks;

/// <summary>Evaluates deterministic model expectations against REST responses (no LLM involved).</summary>
internal static class ModelCheckEvaluator
{
    /// <summary>Evaluates one check.</summary>
    /// <param name="check">The expectation.</param>
    /// <param name="api">Ground-truth reader.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pass/fail with actual value.</returns>
    public static async Task<CheckResult> EvaluateAsync(ModelCheck check, IApiReader api, CancellationToken cancellationToken)
    {
        try
        {
            JsonElement root = await api.GetAsync(check.Url, cancellationToken);
            Expectation e = check.Expect;

            if (e.AllEqual is { } all)
            {
                IReadOnlyList<JsonElement> values = JsonPath.Resolve(root, check.Path);
                List<string> mismatches = values.Select(JsonPath.Text).Where(v => !Same(v, all)).ToList();
                return values.Count > 0 && mismatches.Count == 0
                    ? CheckResult.Pass(check.Id, $"all {values.Count} values = {all}")
                    : CheckResult.Fail(check.Id, values.Count == 0 ? "path matched nothing" : $"expected all '{all}', found: {string.Join(", ", mismatches.Distinct())}");
            }

            string actual = JsonPath.Text(JsonPath.Single(root, check.Path));
            (bool passed, string expected) = e switch
            {
                { EqualTo: { } v } => (Same(actual, v), $"= {v}"),
                { AnyOf: { } options } => (options.Any(o => Same(actual, o)), $"one of [{string.Join(", ", options)}]"),
                { Between: [var min, var max] } => (Number(actual) is { } n && n >= min && n <= max, $"in [{min}, {max}]"),
                { AtLeast: { } least } => (Number(actual) >= least, $">= {least}"),
                { GreaterThan: { } other } => await GreaterThanAsync(actual, other, api, cancellationToken),
                _ => throw new InvalidDataException($"Check {check.Id} has no expectation."),
            };

            return passed
                ? CheckResult.Pass(check.Id, $"{actual} {expected}")
                : CheckResult.Fail(check.Id, $"expected {expected}, actual {actual}");
        }
        catch (Exception ex) when (ex is KeyNotFoundException or HttpRequestException or FormatException or InvalidOperationException)
        {
            return CheckResult.Fail(check.Id, ex.Message);
        }
    }

    private static async Task<(bool, string)> GreaterThanAsync(
        string actual, ValueRef other, IApiReader api, CancellationToken cancellationToken)
    {
        JsonElement otherRoot = await api.GetAsync(other.Url, cancellationToken);
        string otherValue = JsonPath.Text(JsonPath.Single(otherRoot, other.Path));
        return (Number(actual) > Number(otherValue), $"> {otherValue}");
    }

    /// <summary>Equality: numerically when both parse as numbers, otherwise case-insensitive text.</summary>
    internal static bool Same(string actual, string expected) =>
        Number(actual) is { } a && Number(expected) is { } b
            ? Math.Abs(a - b) < 1e-9
            : string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    internal static double? Number(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) ? n : null;
}
