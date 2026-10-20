using System.Text.Json;
using System.Text.RegularExpressions;

namespace WeatherRisk.Eval;

/// <summary>
/// Minimal JSON path resolver for evaluation cases. Segments are separated by <c>.</c>; each segment is a
/// property name (case-insensitive) optionally followed by selectors:
/// <c>[2]</c> index · <c>[*]</c> all items · <c>[key=value]</c> first item whose property equals the value.
/// Examples: <c>entries[0].hubId</c>, <c>rows[metric=Hurricane].values[hubId=miami].score</c>, <c>entries[*].confidence</c>.
/// </summary>
internal static partial class JsonPath
{
    /// <summary>Resolves a path to the matching values (more than one only with <c>[*]</c>).</summary>
    /// <param name="root">Document root.</param>
    /// <param name="path">Path expression.</param>
    /// <returns>Matching elements; empty when the path does not exist.</returns>
    public static IReadOnlyList<JsonElement> Resolve(JsonElement root, string path)
    {
        IEnumerable<JsonElement> current = [root];
        foreach (string segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            Match match = SegmentPattern().Match(segment);
            if (!match.Success)
            {
                throw new FormatException($"Invalid path segment '{segment}' in '{path}'.");
            }

            string name = match.Groups["name"].Value;
            current = current.SelectMany(e => name.Length == 0 ? [e] : Property(e, name));
            foreach (Capture selector in match.Groups["selector"].Captures)
            {
                current = current.SelectMany(e => Select(e, selector.Value));
            }
        }

        return current.ToList();
    }

    /// <summary>Resolves a path that must match exactly one value.</summary>
    /// <param name="root">Document root.</param>
    /// <param name="path">Path expression.</param>
    /// <returns>The value.</returns>
    /// <exception cref="KeyNotFoundException">When the path matches nothing or several values.</exception>
    public static JsonElement Single(JsonElement root, string path)
    {
        IReadOnlyList<JsonElement> values = Resolve(root, path);
        return values.Count == 1
            ? values[0]
            : throw new KeyNotFoundException($"Path '{path}' matched {values.Count} values (expected 1).");
    }

    /// <summary>Text form of a scalar: strings unquoted, numbers/booleans as raw JSON.</summary>
    /// <param name="value">Element.</param>
    /// <returns>Text.</returns>
    public static string Text(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();

    private static IEnumerable<JsonElement> Property(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                yield return property.Value;
                yield break;
            }
        }
    }

    private static IEnumerable<JsonElement> Select(JsonElement element, string selector)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        if (selector == "*")
        {
            return element.EnumerateArray().ToList();
        }

        if (int.TryParse(selector, out int index))
        {
            return index < element.GetArrayLength() ? [element[index]] : [];
        }

        int equals = selector.IndexOf('=', StringComparison.Ordinal);
        if (equals <= 0)
        {
            throw new FormatException($"Invalid selector '[{selector}]'.");
        }

        string key = selector[..equals];
        string expected = selector[(equals + 1)..];
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (Property(item, key).FirstOrDefault() is { ValueKind: not JsonValueKind.Undefined } value &&
                string.Equals(Text(value), expected, StringComparison.OrdinalIgnoreCase))
            {
                return [item];
            }
        }

        return [];
    }

    [GeneratedRegex(@"^(?<name>[A-Za-z0-9_]*)(\[(?<selector>[^\]]+)\])*$")]
    private static partial Regex SegmentPattern();
}
