using WeatherRisk.Core.Scoring;

namespace WeatherRisk.Core.Tests.Scoring;

public sealed class RiskScoreSnapshotTests
{
    [Fact]
    public void From_CopiesCompositeAndEveryHazardScore()
    {
        HubRiskScore score = TestData.Score(
            "dallas", 62.5, winter: 30, hurricane: 10, flood: 40, severeStorm: 80, heat: 55);
        var at = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

        RiskScoreSnapshot snapshot = RiskScoreSnapshot.From(score, at);

        Assert.Equal(
            new RiskScoreSnapshot("dallas", at, 62.5, RiskRating.High, ScoreConfidence.High, 30, 10, 40, 80, 55),
            snapshot);
    }
}
