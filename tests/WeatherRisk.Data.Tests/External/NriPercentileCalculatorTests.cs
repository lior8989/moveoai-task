using WeatherRisk.Core.Scoring;
using WeatherRisk.Data.External.Fema;

namespace WeatherRisk.Data.Tests.External;

public sealed class NriPercentileCalculatorTests
{
    [Theory]
    [InlineData(1.0, 12.5)]   // 0 below, 1 equal → 0.5/4
    [InlineData(4.0, 87.5)]   // 3 below, 1 equal → 3.5/4
    [InlineData(2.5, 50.0)]   // 2 below, 0 equal → 2/4
    public void Percentile_DistinctValues_UsesMidRank(double value, double expected)
    {
        Assert.Equal(expected, NriPercentileCalculator.Percentile([1, 2, 3, 4], value));
    }

    [Fact]
    public void Percentile_Ties_ShareTheSameMidRank()
    {
        // Six zeros and two non-zero: zero → (0 + 0.5×6)/8 = 37.5.
        double[] sorted = [0, 0, 0, 0, 0, 0, 1, 2];

        Assert.Equal(37.5, NriPercentileCalculator.Percentile(sorted, 0));
    }

    [Fact]
    public void Percentile_EmptyColumn_IsZero()
    {
        Assert.Equal(0, NriPercentileCalculator.Percentile([], 5));
    }

    [Fact]
    public void Compute_RanksEachHazardIndependently_AndSkipsNullsInDistribution()
    {
        (string, double?[])[] all =
        [
            ("A", [10, null, 1, 1, 1, 1, 1, 1, 1, 1]),
            ("B", [20, 5, 2, 2, 2, 2, 2, 2, 2, 2]),
            ("C", [30, 6, 3, 3, 3, 3, 3, 3, 3, 3]),
        ];

        Dictionary<string, HazardBaseline> result = NriPercentileCalculator.Compute(all, ["A", "C"]);

        Assert.Equal(16.67, result["A"].WinterWeather); // 0.5/3
        Assert.Null(result["A"].IceStorm);              // not applicable stays null
        Assert.Equal(75.0, result["C"].IceStorm);       // among [5,6] only: 1.5/2
        Assert.Equal(83.33, result["C"].HeatWave);      // 2.5/3
        Assert.False(result.ContainsKey("B"));
    }
}
