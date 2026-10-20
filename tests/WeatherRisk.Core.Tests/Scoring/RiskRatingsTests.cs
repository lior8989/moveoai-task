using WeatherRisk.Core.Scoring;

namespace WeatherRisk.Core.Tests.Scoring;

public sealed class RiskRatingsTests
{
    [Theory]
    [InlineData(0, RiskRating.Low)]
    [InlineData(24.9, RiskRating.Low)]
    [InlineData(25, RiskRating.Moderate)]
    [InlineData(49.99, RiskRating.Moderate)]
    [InlineData(50, RiskRating.High)]
    [InlineData(74.9, RiskRating.High)]
    [InlineData(75, RiskRating.VeryHigh)]
    [InlineData(100, RiskRating.VeryHigh)]
    public void FromScore_BandBoundaries_AreInclusiveLowerBounds(double score, RiskRating expected)
    {
        Assert.Equal(expected, RiskRatings.FromScore(score));
    }
}
