using WeatherRisk.Eval;
using WeatherRisk.Eval.Reporting;

// Evaluation runner. Exit codes: 0 = all passed, 1 = some checks failed, 2 = setup problem.
EvalOptions options;
EvalSuite suite;
try
{
    options = EvalOptions.Parse(args);
    suite = EvalSuite.Load(options.CasesPath);
}
catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException or System.Text.Json.JsonException or FormatException or UriFormatException)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine(EvalOptions.Usage);
    return 2;
}

using var api = new ApiClient(options.BaseUrl);
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };

try
{
    // Fail fast with a clear message when the API isn't running.
    await api.GetFreshAsync("/api/system/health", cancellation.Token);
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Cannot reach the API at {options.BaseUrl} ({ex.Message}). Start it first: dotnet run --project src/WeatherRisk.Api");
    return 2;
}

Console.WriteLine($"Evaluating {options.BaseUrl} — {suite.ModelChecks.Count} model checks, " +
                  $"{(options.SkipAgent ? "agent cases skipped" : $"{suite.AgentCases.Count} agent cases ({options.DelaySeconds:0} s apart)")}");
Console.WriteLine();

EvalReport report = await new EvalRunner(api, options).RunAsync(suite, cancellation.Token);
(string json, string markdown) = ReportWriter.Write(report, options.OutputDirectory);
ReportWriter.PrintSummary(report);
Console.WriteLine($"Reports: {markdown}");
Console.WriteLine($"         {json}");
return report.Summary.AllPassed ? 0 : 1;
