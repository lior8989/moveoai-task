using WeatherRisk.Core.Agent;

namespace WeatherRisk.Core.Tests.Agent;

public sealed class AnswerValidatorTests
{
    private static string Answer(string answer = "Text", string uncertainty = "Some", string findings = "[]", string followUps = "[]") =>
        $$"""
        {"answer":"{{answer}}","intent":"HubRisk","findings":{{findings}},"keyFactors":["Flood"],"assumptions":["Uncalibrated"],
         "uncertainty":"{{uncertainty}}","followUpQuestions":{{followUps}}}
        """;

    [Fact]
    public void TryParse_ValidAnswer_Succeeds()
    {
        Assert.True(AnswerValidator.TryParse(Answer(), out AgentAnswer? answer, out string? error));
        Assert.Null(error);
        Assert.Equal(["Flood"], answer!.KeyFactors);
    }

    [Theory]
    [InlineData("", "Some", "[]", "'answer' must not be empty")]
    [InlineData("Text", " ", "[]", "'uncertainty' must not be empty")]
    [InlineData("Text", "Some", """["a","b","c","d"]""", "at most 3")]
    public void TryParse_SemanticViolations_ReturnExplanation(string answer, string uncertainty, string followUps, string expected)
    {
        Assert.False(AnswerValidator.TryParse(Answer(answer, uncertainty, followUps: followUps), out AgentAnswer? parsed, out string? error));
        Assert.Null(parsed);
        Assert.Contains(expected, error);
    }

    [Fact]
    public void TryParse_MalformedJson_ReturnsSchemaError()
    {
        Assert.False(AnswerValidator.TryParse("{not json", out _, out string? error));
        Assert.Contains("do not match the schema", error);
    }

    private static AnswerValidator.ToolEvidence Evidence()
    {
        var evidence = new AnswerValidator.ToolEvidence();
        AnswerValidator.Collect("""{"entries":[{"hubId":"dallas","score":64.63,"nested":{"days":3.6}}],"hubs":[{"id":"miami","score":98.6}]}""", evidence);
        return evidence;
    }

    [Fact]
    public void Collect_FindsHubIdsUnderHubIdAndIdAndNestedNumbers()
    {
        AnswerValidator.ToolEvidence evidence = Evidence();

        Assert.Equal(["dallas", "miami"], evidence.HubIds.Order());
        Assert.True(evidence.Contains(3.6));
    }

    [Theory]
    [InlineData(64.63, true)]  // exact
    [InlineData(64.6, true)]   // rounded to 1 decimal
    [InlineData(65, true)]     // rounded to integer
    [InlineData(64.0, false)]  // not a rounding of 64.63
    [InlineData(70.1, false)]  // invented
    public void Ground_NumberMatching_ToleratesRoundingOnly(double value, bool verified)
    {
        AgentAnswer answer = Parse(Answer(findings: $$"""[{"hubId":"dallas","metric":"Composite","value":{{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"unit":"score"}]"""));

        GroundingReport report = AnswerValidator.Ground(answer, Evidence());

        Assert.Equal(verified, report.Findings.Single().Verified);
        Assert.Equal(verified, report.AllGrounded);
        Assert.Equal(verified ? 0 : 1, report.Notes.Count);
    }

    [Fact]
    public void Ground_HubNotReturnedByTools_IsUnverifiedWithNote()
    {
        AgentAnswer answer = Parse(Answer(findings: """[{"hubId":"denver","metric":"Composite","value":64.63,"unit":"score"}]"""));

        GroundingReport report = AnswerValidator.Ground(answer, Evidence());

        Assert.False(report.AllGrounded);
        Assert.Contains("denver", report.Notes.Single());
    }

    [Fact]
    public void Ground_NoFindings_IsVacuouslyGrounded()
    {
        Assert.True(AnswerValidator.Ground(Parse(Answer()), Evidence()).AllGrounded);
    }

    private static AgentAnswer Parse(string json)
    {
        Assert.True(AnswerValidator.TryParse(json, out AgentAnswer? answer, out string? error), error);
        return answer!;
    }
}
