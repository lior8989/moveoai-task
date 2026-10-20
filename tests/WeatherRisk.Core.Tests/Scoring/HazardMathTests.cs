using WeatherRisk.Core.Scoring.Hazards;

namespace WeatherRisk.Core.Tests.Scoring;

public sealed class HazardMathTests
{
    [Theory]
    [InlineData(0, 40, 0)]
    [InlineData(20, 40, 50)]
    [InlineData(40, 40, 100)]
    [InlineData(80, 40, 100)]  // clamped above
    [InlineData(-5, 40, 0)]    // clamped below
    public void Normalize_MapsLinearlyToCapAndClamps(double value, double cap, double expected)
    {
        Assert.Equal(expected, HazardMath.Normalize(value, cap), precision: 10);
    }

    [Fact]
    public void Blend_BothSignals_WeightsThem()
    {
        Assert.Equal(46.0, HazardMath.Blend(40, 60, 0.7), precision: 10);
    }

    [Fact]
    public void Blend_OneSignalMissing_UsesTheOther()
    {
        Assert.Equal(40, HazardMath.Blend(40, null, 0.7));
        Assert.Equal(60, HazardMath.Blend(null, 60, 0.7));
    }

    [Fact]
    public void Blend_BothMissing_IsZero()
    {
        Assert.Equal(0, HazardMath.Blend(null, null, 0.5));
    }

    [Fact]
    public void AverageOfAvailable_SkipsNulls()
    {
        Assert.Equal(60, HazardMath.AverageOfAvailable(40, null, 80));
        Assert.Null(HazardMath.AverageOfAvailable(null, null));
    }

    [Fact]
    public void Round_MidpointRoundsAwayFromZero()
    {
        Assert.Equal(2.5, HazardMath.Round(2.45));
        Assert.Equal(46.8, HazardMath.Round(46.8000000001));
    }
}
