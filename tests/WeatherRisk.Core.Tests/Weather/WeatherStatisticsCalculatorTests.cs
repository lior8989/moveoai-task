using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Tests.Weather;

public sealed class WeatherStatisticsCalculatorTests
{
    private static readonly DateOnly Jan1 = new(2025, 1, 1);
    private readonly WeatherStatisticsCalculator _calculator = new(new WeatherThresholds());

    private static DailyWeather Day(
        int dayOffset, double? snow = 0, double? precip = 0, double? gust = 10, double? high = 20, double? low = 5) =>
        new(Jan1.AddDays(dayOffset), snow, precip, gust, high, low);

    [Fact]
    public void Summarize_ValuesExactlyAtThresholds_CountAsDisruptionDays()
    {
        DailyWeather[] days =
        [
            Day(0, snow: 1.0),        // snow day (inclusive)
            Day(1, low: -12.0),       // hard freeze (inclusive)
            Day(2, precip: 50.0),     // heavy rain (inclusive)
            Day(3, gust: 75.0),       // damaging gust only
            Day(4, gust: 90.0),       // damaging AND tropical-storm gust
            Day(5, high: 38.0),       // extreme heat (inclusive)
        ];

        WeatherSummary s = _calculator.Summarize(days, Jan1, Jan1.AddDays(5));

        Assert.Equal(6, s.DaysObserved);
        Assert.Equal(1, s.SnowDays);
        Assert.Equal(1, s.HardFreezeDays);
        Assert.Equal(1, s.HeavyRainDays);
        Assert.Equal(2, s.DamagingGustDays);
        Assert.Equal(1, s.TropicalStormGustDays);
        Assert.Equal(1, s.ExtremeHeatDays);
    }

    [Fact]
    public void Summarize_ValuesJustBelowThresholds_DoNotCount()
    {
        DailyWeather[] days = [Day(0, snow: 0.99, low: -11.9, precip: 49.9, gust: 74.9, high: 37.9)];

        WeatherSummary s = _calculator.Summarize(days, Jan1, Jan1);

        Assert.Equal(0, s.SnowDays + s.HardFreezeDays + s.HeavyRainDays + s.DamagingGustDays + s.ExtremeHeatDays);
    }

    [Fact]
    public void Summarize_NullValues_CountAsObservedButNeverAsDisruption()
    {
        DailyWeather[] days = [new(Jan1, null, null, null, null, null)];

        WeatherSummary s = _calculator.Summarize(days, Jan1, Jan1);

        Assert.Equal(1, s.DaysObserved);
        Assert.Equal(0, s.SnowDays);
        Assert.Null(s.MaxWindGustKmh);
        Assert.Null(s.MaxTemperatureC);
        Assert.Null(s.MinTemperatureC);
    }

    [Fact]
    public void Summarize_DaysOutsidePeriod_AreIgnored()
    {
        DailyWeather[] days = [Day(-1, snow: 30), Day(0, snow: 5), Day(1, snow: 30)];

        WeatherSummary s = _calculator.Summarize(days, Jan1, Jan1);

        Assert.Equal(1, s.DaysObserved);
        Assert.Equal(5, s.TotalSnowfallCm);
    }

    [Fact]
    public void Summarize_ComputesTotalsAndExtremes()
    {
        DailyWeather[] days =
        [
            Day(0, snow: 2, precip: 10, gust: 40, high: 30, low: -3),
            Day(1, snow: 3, precip: 5, gust: 60, high: 25, low: -8),
        ];

        WeatherSummary s = _calculator.Summarize(days, Jan1, Jan1.AddDays(1));

        Assert.Equal(5, s.TotalSnowfallCm);
        Assert.Equal(15, s.TotalPrecipitationMm);
        Assert.Equal(60, s.MaxWindGustKmh);
        Assert.Equal(30, s.MaxTemperatureC);
        Assert.Equal(-8, s.MinTemperatureC);
    }

    [Fact]
    public void Summarize_YearWithSnowEveryFifthDay_Reports20PercentSnowDays()
    {
        // "What percentage of days in Denver last year had snowfall?" — the stat behind that question.
        DailyWeather[] year = Enumerable.Range(0, 365).Select(i => Day(i, snow: i % 5 == 0 ? 4 : 0)).ToArray();

        WeatherSummary s = _calculator.Summarize(year, Jan1, new DateOnly(2025, 12, 31));

        Assert.Equal(73, s.SnowDays);
        Assert.Equal(20.0, s.PercentOfDays(s.SnowDays), precision: 10);
        Assert.Equal(1.0, s.Coverage);
    }

    [Fact]
    public void Summarize_EndBeforeStart_Throws()
    {
        Assert.Throws<ArgumentException>(() => _calculator.Summarize([], Jan1, Jan1.AddDays(-1)));
    }

    [Fact]
    public void Summarize_CustomThresholds_AreApplied()
    {
        var calculator = new WeatherStatisticsCalculator(new WeatherThresholds { SnowDayCm = 10 });

        WeatherSummary s = calculator.Summarize([Day(0, snow: 5), Day(1, snow: 10)], Jan1, Jan1.AddDays(1));

        Assert.Equal(1, s.SnowDays);
    }
}
