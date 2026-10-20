using WeatherRisk.Core.Agent;

namespace WeatherRisk.Core.Tests.Agent;

public sealed class AnswerCleanerTests
{
    [Fact]
    public void RemoveDuplicatedSections_TrailingStructuredSections_AreRemoved()
    {
        const string answer = """
            **Prioritized Resilience Investment**

            1. **New Orleans Hub** – Composite score **71.2** (High).

            ---

            **Assumptions**
            - Weights uncalibrated.

            **Follow‑up questions**
            1. Cost-benefit?

            **Intent**: Ranking
            """;

        string cleaned = AnswerCleaner.RemoveDuplicatedSections(answer);

        Assert.Equal("**Prioritized Resilience Investment**\n\n1. **New Orleans Hub** – Composite score **71.2** (High).", cleaned);
    }

    [Theory]
    [InlineData("### Uncertainty")]
    [InlineData("Key factors:")]
    [InlineData("**Confidence & caveats:**")]
    [InlineData("Follow-up questions")]
    [InlineData("**Suggested follow-ups**")]
    public void RemoveDuplicatedSections_RecognizesLabelVariants(string label)
    {
        string cleaned = AnswerCleaner.RemoveDuplicatedSections($"Dallas is High.\n{label}\n- something");

        Assert.Equal("Dallas is High.", cleaned);
    }

    [Fact]
    public void RemoveDuplicatedSections_NoLabels_ReturnsUnchanged()
    {
        const string answer = "Minneapolis leads.\n\n- Chicago second\n- Detroit third";

        Assert.Same(answer, AnswerCleaner.RemoveDuplicatedSections(answer));
    }

    [Fact]
    public void RemoveDuplicatedSections_LabelOnFirstLine_IsKept()
    {
        const string answer = "**Uncertainty**\nThe data covers 2021–2025.";

        Assert.Same(answer, AnswerCleaner.RemoveDuplicatedSections(answer));
    }

    [Fact]
    public void RemoveDuplicatedSections_WordsInsideSentences_AreNotLabels()
    {
        const string answer = "Dallas is High.\nKey factors are flood and severe storms, with some uncertainty about hail.";

        Assert.Same(answer, AnswerCleaner.RemoveDuplicatedSections(answer));
    }
}
