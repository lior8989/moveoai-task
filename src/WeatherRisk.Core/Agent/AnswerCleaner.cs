using System.Text.RegularExpressions;

namespace WeatherRisk.Core.Agent;

/// <summary>
/// Presentation cleanup for the answer text. Some models (notably the fallback gpt-oss-20b) append sections
/// such as "Assumptions", "Follow-up questions" or "Uncertainty" to <see cref="AgentAnswer.Answer"/> even
/// though those already have their own structured fields — so the UI would show them twice.
/// </summary>
/// <remarks>
/// Deterministic and conservative: it only cuts from the first line that is a <i>label line</i> for one of
/// those fields (e.g. <c>**Assumptions**</c>, <c>### Uncertainty</c>, <c>Follow-up questions:</c>) to the
/// end, never removes the first line, and leaves the text unchanged when nothing matches. Chosen over asking
/// the model to rewrite (which costs an extra LLM call under tight free-tier quotas).
/// </remarks>
public static partial class AnswerCleaner
{
    /// <summary>Removes trailing sections that duplicate structured fields.</summary>
    /// <param name="answer">Answer text from the model.</param>
    /// <returns>Answer text without duplicated trailing sections.</returns>
    public static string RemoveDuplicatedSections(string answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        string[] lines = answer.ReplaceLineEndings("\n").Split('\n');

        int cut = -1;
        for (int i = 1; i < lines.Length; i++)
        {
            if (LabelLine().IsMatch(lines[i]))
            {
                cut = i;
                break;
            }
        }

        if (cut < 0)
        {
            return answer;
        }

        // Drop a horizontal rule or blank lines left just above the cut.
        int end = cut;
        while (end > 0 && (string.IsNullOrWhiteSpace(lines[end - 1]) || HorizontalRule().IsMatch(lines[end - 1])))
        {
            end--;
        }

        return end == 0 ? answer : string.Join('\n', lines, 0, end).TrimEnd();
    }

    // A line that only introduces one of the structured fields: optional heading marks / bold, the label,
    // optional closing bold, optional colon, and optionally a short value (e.g. "**Intent**: Ranking").
    [GeneratedRegex(
        @"^\s*(#{1,6}\s*)?(\*\*|__)?\s*(assumptions?|key factors|uncertainty|confidence(\s*(&|and)\s*caveats)?|intent|(suggested\s+)?follow[\s‐-‑-]?ups?(\s+questions)?)\s*(\*\*|__)?\s*:?\s*(\*\*)?\s*([A-Za-z]{0,20})?\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex LabelLine();

    [GeneratedRegex(@"^\s*([-*_]\s*){3,}$")]
    private static partial Regex HorizontalRule();
}
