using WeatherRisk.Eval;
using WeatherRisk.Eval.Checks;

namespace WeatherRisk.Eval.Tests;

public sealed class ModelCheckEvaluatorTests
{
    private readonly FakeApi _api = new(new()
    {
        ["/r"] = """{"entries":[{"hubId":"minneapolis","rank":1,"score":70.8,"confidence":"High"},{"hubId":"seattle","rank":15,"score":23.6,"confidence":"Medium"}],"rating":"High"}""",
    });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<CheckResult> Run(string path, Expectation expect) =>
        ModelCheckEvaluator.EvaluateAsync(new ModelCheck("M", "d", "/r", path, expect), _api, Ct);

    [Fact]
    public async Task EqualTo_ComparesTextCaseInsensitivelyAndNumbersNumerically()
    {
        Assert.True((await Run("entries[0].hubId", new Expectation(EqualTo: "MINNEAPOLIS"))).Passed);
        Assert.True((await Run("entries[0].rank", new Expectation(EqualTo: "1.0"))).Passed);
        Assert.False((await Run("entries[0].hubId", new Expectation(EqualTo: "chicago"))).Passed);
    }

    [Fact]
    public async Task AnyOf_Between_AtLeast()
    {
        Assert.True((await Run("rating", new Expectation(AnyOf: ["High", "VeryHigh"]))).Passed);
        Assert.True((await Run("entries[0].score", new Expectation(Between: [70, 71]))).Passed);
        Assert.False((await Run("entries[0].score", new Expectation(Between: [10, 20]))).Passed);
        Assert.True((await Run("entries[hubId=seattle].rank", new Expectation(AtLeast: 13))).Passed);
    }

    [Fact]
    public async Task GreaterThan_ComparesAgainstAnotherResolvedValue()
    {
        CheckResult result = await Run("entries[0].score", new Expectation(GreaterThan: new ValueRef("/r", "entries[1].score")));

        Assert.True(result.Passed);
        Assert.Contains("70.8 > 23.6", result.Detail);
    }

    [Fact]
    public async Task AllEqual_ReportsMismatches()
    {
        CheckResult result = await Run("entries[*].confidence", new Expectation(AllEqual: "High"));

        Assert.False(result.Passed);
        Assert.Contains("Medium", result.Detail);
    }

    [Fact]
    public async Task MissingPathOrUrl_FailsWithMessageInsteadOfThrowing()
    {
        Assert.False((await Run("entries[9].score", new Expectation(AtLeast: 0))).Passed);
        CheckResult badUrl = await ModelCheckEvaluator.EvaluateAsync(
            new ModelCheck("M", "d", "/missing", "x", new Expectation(EqualTo: "1")), _api, Ct);
        Assert.False(badUrl.Passed);
        Assert.Contains("404", badUrl.Detail);
    }
}
