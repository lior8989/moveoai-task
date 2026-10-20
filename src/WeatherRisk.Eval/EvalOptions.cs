using System.Globalization;

namespace WeatherRisk.Eval;

/// <summary>Command-line options.</summary>
/// <param name="BaseUrl">API base URL.</param>
/// <param name="CasesPath">Path to the suite JSON.</param>
/// <param name="OutputDirectory">Where reports are written.</param>
/// <param name="SkipAgent">Run only the deterministic model checks (no LLM, no API key needed).</param>
/// <param name="DelaySeconds">Pause between agent questions — keeps within the chat rate limit and free LLM quotas.</param>
/// <param name="Only">Run only these case/check ids; empty = all.</param>
internal sealed record EvalOptions(
    Uri BaseUrl, string CasesPath, string OutputDirectory, bool SkipAgent, double DelaySeconds, IReadOnlySet<string> Only)
{
    /// <summary>Usage text.</summary>
    public const string Usage = """
        Usage: dotnet run --project src/WeatherRisk.Eval -- [options]
          --base-url <url>    API to evaluate (default http://localhost:5050)
          --cases <path>      Suite file (default: eval-cases.json next to the executable)
          --out <dir>         Report folder (default ./eval-results)
          --skip-agent        Only deterministic model checks (no LLM calls)
          --delay <seconds>   Pause between agent questions (default 20; free-tier LLM quotas are per minute)
          --only <ids>        Comma-separated case/check ids, e.g. A1,A3,M2
        """;

    /// <summary>Parses arguments.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Options.</returns>
    /// <exception cref="ArgumentException">On unknown options or bad values.</exception>
    public static EvalOptions Parse(IReadOnlyList<string> args)
    {
        var baseUrl = new Uri("http://localhost:5050");
        string cases = Path.Combine(AppContext.BaseDirectory, "eval-cases.json");
        string output = Path.Combine(Environment.CurrentDirectory, "eval-results");
        bool skipAgent = false;
        double delay = 20;
        var only = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Count; i++)
        {
            string Next() => i + 1 < args.Count ? args[++i] : throw new ArgumentException($"Missing value for {args[i]}.");
            switch (args[i])
            {
                case "--base-url": baseUrl = new Uri(Next()); break;
                case "--cases": cases = Next(); break;
                case "--out": output = Next(); break;
                case "--skip-agent": skipAgent = true; break;
                case "--delay": delay = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--only": only.UnionWith(Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)); break;
                default: throw new ArgumentException($"Unknown option '{args[i]}'.");
            }
        }

        return new EvalOptions(baseUrl, cases, output, skipAgent, delay, only);
    }
}
