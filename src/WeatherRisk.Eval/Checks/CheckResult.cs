namespace WeatherRisk.Eval.Checks;

/// <summary>Outcome of one check.</summary>
/// <param name="Name">Check name, e.g. <c>toolCalled(rank_hubs)</c>.</param>
/// <param name="Passed">Whether it passed.</param>
/// <param name="Detail">Expected vs actual, for the report.</param>
internal sealed record CheckResult(string Name, bool Passed, string Detail)
{
    /// <summary>Creates a passing result.</summary>
    public static CheckResult Pass(string name, string detail) => new(name, true, detail);

    /// <summary>Creates a failing result.</summary>
    public static CheckResult Fail(string name, string detail) => new(name, false, detail);
}
