namespace WeatherRisk.Core.Weather;

/// <summary>
/// Aggregated weather statistics for one hub over a period: disruption-day counts plus extremes.
/// Produced by <see cref="WeatherStatisticsCalculator"/>; consumed by hazard scorers and by stats questions
/// such as "what percentage of days in Denver last year had snowfall?".
/// </summary>
/// <param name="From">First day of the period (inclusive).</param>
/// <param name="To">Last day of the period (inclusive).</param>
/// <param name="DaysObserved">Days in the period for which data was available.</param>
/// <param name="SnowDays">Days with snowfall ≥ <see cref="WeatherThresholds.SnowDayCm"/>.</param>
/// <param name="HardFreezeDays">Days with minimum temperature ≤ <see cref="WeatherThresholds.HardFreezeC"/>.</param>
/// <param name="HeavyRainDays">Days with precipitation ≥ <see cref="WeatherThresholds.HeavyRainMm"/>.</param>
/// <param name="DamagingGustDays">Days with max gust ≥ <see cref="WeatherThresholds.DamagingGustKmh"/>.</param>
/// <param name="TropicalStormGustDays">Days with max gust ≥ <see cref="WeatherThresholds.TropicalStormGustKmh"/>.</param>
/// <param name="ExtremeHeatDays">Days with max temperature ≥ <see cref="WeatherThresholds.ExtremeHeatC"/>.</param>
/// <param name="TotalSnowfallCm">Total snowfall over the period, cm.</param>
/// <param name="TotalPrecipitationMm">Total precipitation over the period, mm.</param>
/// <param name="MaxWindGustKmh">Highest gust observed, km/h; null when no gust data.</param>
/// <param name="MaxTemperatureC">Highest temperature observed, °C; null when no data.</param>
/// <param name="MinTemperatureC">Lowest temperature observed, °C; null when no data.</param>
public sealed record WeatherSummary(
    DateOnly From,
    DateOnly To,
    int DaysObserved,
    int SnowDays,
    int HardFreezeDays,
    int HeavyRainDays,
    int DamagingGustDays,
    int TropicalStormGustDays,
    int ExtremeHeatDays,
    double TotalSnowfallCm,
    double TotalPrecipitationMm,
    double? MaxWindGustKmh,
    double? MaxTemperatureC,
    double? MinTemperatureC)
{
    // Average Gregorian year length, so per-year rates are correct across leap years.
    private const double DaysPerYear = 365.25;

    /// <summary>Number of calendar days in the period.</summary>
    public int DaysInPeriod => To.DayNumber - From.DayNumber + 1;

    /// <summary>Share of the period with data, 0–1. Low coverage lowers scoring confidence.</summary>
    public double Coverage => DaysInPeriod <= 0 ? 0 : Math.Min(1.0, (double)DaysObserved / DaysInPeriod);

    /// <summary>Observed period length in years (based on days with data).</summary>
    public double YearsObserved => DaysObserved / DaysPerYear;

    /// <summary>Converts a day count to an average per year over the observed days.</summary>
    /// <param name="dayCount">A disruption-day count from this summary.</param>
    /// <returns>Average days per year; 0 when nothing was observed.</returns>
    public double PerYear(int dayCount) => DaysObserved == 0 ? 0 : dayCount / YearsObserved;

    /// <summary>Converts a day count to a percentage of observed days.</summary>
    /// <param name="dayCount">A disruption-day count from this summary.</param>
    /// <returns>Percentage 0–100; 0 when nothing was observed.</returns>
    public double PercentOfDays(int dayCount) => DaysObserved == 0 ? 0 : 100.0 * dayCount / DaysObserved;
}
