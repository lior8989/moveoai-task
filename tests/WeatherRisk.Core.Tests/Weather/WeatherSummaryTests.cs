using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Tests.Weather;

public sealed class WeatherSummaryTests
{
    [Fact]
    public void PerYear_FourFullYears_DividesByFour()
    {
        WeatherSummary s = TestData.Weather(snow: 80);

        Assert.Equal(4.0, s.YearsObserved, precision: 10);
        Assert.Equal(20.0, s.PerYear(s.SnowDays), precision: 10);
    }

    [Fact]
    public void Coverage_HalfTheDaysObserved_IsHalf()
    {
        WeatherSummary s = TestData.Weather(daysObserved: 730);

        Assert.Equal(1461, s.DaysInPeriod);
        Assert.Equal(730.0 / 1461, s.Coverage, precision: 10);
    }

    [Fact]
    public void RateHelpers_NothingObserved_ReturnZero()
    {
        WeatherSummary s = TestData.Weather(daysObserved: 0);

        Assert.Equal(0, s.PerYear(10));
        Assert.Equal(0, s.PercentOfDays(10));
        Assert.Equal(0, s.Coverage);
    }
}
