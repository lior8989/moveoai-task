using System.Globalization;
using System.Text;
using System.Text.Json;
using WeatherRisk.Eval.Checks;

namespace WeatherRisk.Eval.Reporting;

/// <summary>Writes the report as JSON (machine-readable) and Markdown (for reviewers), and prints a console summary.</summary>
internal static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Writes <c>eval-report.json</c> and <c>eval-report.md</c> into the output folder.</summary>
    /// <param name="report">The report.</param>
    /// <param name="outputDirectory">Destination folder (created if missing).</param>
    /// <returns>Paths written.</returns>
    public static (string Json, string Markdown) Write(EvalReport report, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        string json = Path.Combine(outputDirectory, "eval-report.json");
        string markdown = Path.Combine(outputDirectory, "eval-report.md");
        File.WriteAllText(json, JsonSerializer.Serialize(report, JsonOptions));
        File.WriteAllText(markdown, ToMarkdown(report));
        return (json, markdown);
    }

    /// <summary>Renders the Markdown report.</summary>
    /// <param name="report">The report.</param>
    /// <returns>Markdown text.</returns>
    public static string ToMarkdown(EvalReport report)
    {
        EvalSummary s = report.Summary;
        var md = new StringBuilder();
        md.AppendLine("# Evaluation report").AppendLine();
        md.AppendLine(Invariant($"Run {report.StartedAt:yyyy-MM-dd HH:mm} UTC against `{report.BaseUrl}` — **{(s.AllPassed ? "ALL PASSED" : "FAILURES")}**")).AppendLine();
        md.AppendLine("| Metric | Value |").AppendLine("|---|---|");
        md.AppendLine(Invariant($"| Model checks (deterministic, no LLM) | {s.ModelChecksPassed}/{s.ModelChecksTotal} |"));
        md.AppendLine(Invariant($"| Agent cases (all checks pass) | {s.AgentCasesPassed}/{s.AgentCasesTotal} |"));
        md.AppendLine(Invariant($"| Agent checks | {s.AgentChecksPassed}/{s.AgentChecksTotal} |"));
        md.AppendLine(Invariant($"| Agent latency avg / p95 | {s.AverageLatencyMs / 1000:F1} s / {s.P95LatencyMs / 1000:F1} s |"));
        md.AppendLine(Invariant($"| Tokens | {s.TotalTokens:N0} |"));
        md.AppendLine(Invariant($"| Models used | {string.Join(", ", s.ModelsUsed.Select(m => $"{m.Key} ×{m.Value}"))} |"));
        md.AppendLine(Invariant($"| LLM fallbacks during run | {s.LlmFallbacks} |")).AppendLine();

        md.AppendLine("## Model checks").AppendLine();
        md.AppendLine("| | Id | Expectation | Result |").AppendLine("|---|---|---|---|");
        foreach (ModelCheckOutcome m in report.ModelChecks)
        {
            md.AppendLine($"| {Mark(m.Result.Passed)} | {m.Id} | {Escape(m.Description)} | {Escape(m.Result.Detail)} |");
        }

        md.AppendLine().AppendLine("## Agent cases").AppendLine();
        md.AppendLine("| | Id | Question | Tools | Model | Latency | Checks |").AppendLine("|---|---|---|---|---|---|---|");
        foreach (AgentCaseOutcome a in report.AgentCases)
        {
            md.AppendLine(Invariant(
                $"| {Mark(a.Passed)} | {a.Id} | {Escape(a.Question)} | {string.Join(", ", a.Tools)} | {a.Model} | {a.LatencyMs / 1000:F1} s | {a.Checks.Count(c => c.Passed)}/{a.Checks.Count} |"));
        }

        md.AppendLine().AppendLine("## Details").AppendLine();
        foreach (AgentCaseOutcome a in report.AgentCases)
        {
            md.AppendLine($"### {Mark(a.Passed)} {a.Id} — {a.Question}").AppendLine();
            md.AppendLine($"*Purpose:* {a.Purpose}").AppendLine();
            foreach (CheckResult c in a.Checks)
            {
                md.AppendLine($"- {Mark(c.Passed)} `{c.Name}` — {c.Detail}");
            }

            md.AppendLine().AppendLine("<details><summary>Answer</summary>").AppendLine();
            md.AppendLine(a.Answer).AppendLine().AppendLine("</details>").AppendLine();
        }

        return md.ToString();
    }

    /// <summary>Prints a compact summary to the console.</summary>
    /// <param name="report">The report.</param>
    public static void PrintSummary(EvalReport report)
    {
        EvalSummary s = report.Summary;
        Console.WriteLine();
        Console.WriteLine(Invariant($"Model checks: {s.ModelChecksPassed}/{s.ModelChecksTotal}   Agent cases: {s.AgentCasesPassed}/{s.AgentCasesTotal}   Agent checks: {s.AgentChecksPassed}/{s.AgentChecksTotal}"));
        if (s.AgentCasesTotal > 0)
        {
            Console.WriteLine(Invariant($"Latency avg {s.AverageLatencyMs / 1000:F1} s, p95 {s.P95LatencyMs / 1000:F1} s · tokens {s.TotalTokens:N0} · LLM fallbacks {s.LlmFallbacks}"));
        }

        Console.WriteLine(s.AllPassed ? "RESULT: ALL PASSED" : "RESULT: FAILURES (see report)");
    }

    private static string Mark(bool passed) => passed ? "✅" : "❌";

    private static string Escape(string text) => text.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
