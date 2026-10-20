namespace WeatherRisk.Core.Weather;

/// <summary>
/// Aggregates daily weather into a <see cref="WeatherSummary"/> using configurable disruption thresholds.
/// </summary>
/// <remarks>Stateless and thread-safe (register as a singleton). Single pass, no intermediate collections.</remarks>
public sealed class WeatherStatisticsCalculator
{
    private readonly WeatherThresholds _thresholds;

    /// <summary>Creates a calculator.</summary>
    /// <param name="thresholds">Disruption-day thresholds.</param>
    public WeatherStatisticsCalculator(WeatherThresholds thresholds) => _thresholds = thresholds;

    /// <summary>Summarizes the days that fall within <paramref name="from"/>..<paramref name="to"/>.</summary>
    /// <param name="days">Daily observations (any order; days outside the period are ignored).</param>
    /// <param name="from">First day of the period (inclusive).</param>
    /// <param name="to">Last day of the period (inclusive).</param>
    /// <returns>Counts and extremes for the period.</returns>
    /// <exception cref="ArgumentException">When <paramref name="to"/> is before <paramref name="from"/>.</exception>
    public WeatherSummary Summarize(IEnumerable<DailyWeather> days, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(days);
        if (to < from)
        {
            throw new ArgumentException($"Period end {to} is before start {from}.", nameof(to));
        }

        int observed = 0, snow = 0, freeze = 0, heavyRain = 0, damagingGust = 0, tropicalGust = 0, heat = 0;
        double totalSnow = 0, totalPrecip = 0;
        double? maxGust = null, maxTemp = null, minTemp = null;

        foreach (DailyWeather day in days)
        {
            if (day.Date < from || day.Date > to)
            {
                continue;
            }

            observed++;

            // A null value is "unknown", so it never counts towards a disruption day.
            if (day.SnowfallCm is { } snowCm)
            {
                totalSnow += snowCm;
                if (snowCm >= _thresholds.SnowDayCm) snow++;
            }

            if (day.PrecipitationMm is { } precipMm)
            {
                totalPrecip += precipMm;
                if (precipMm >= _thresholds.HeavyRainMm) heavyRain++;
            }

            if (day.MaxWindGustKmh is { } gust)
            {
                maxGust = maxGust is null ? gust : Math.Max(maxGust.Value, gust);
                if (gust >= _thresholds.DamagingGustKmh) damagingGust++;
                if (gust >= _thresholds.TropicalStormGustKmh) tropicalGust++;
            }

            if (day.MaxTemperatureC is { } high)
            {
                maxTemp = maxTemp is null ? high : Math.Max(maxTemp.Value, high);
                if (high >= _thresholds.ExtremeHeatC) heat++;
            }

            if (day.MinTemperatureC is { } low)
            {
                minTemp = minTemp is null ? low : Math.Min(minTemp.Value, low);
                if (low <= _thresholds.HardFreezeC) freeze++;
            }
        }

        return new WeatherSummary(
            from, to, observed, snow, freeze, heavyRain, damagingGust, tropicalGust, heat,
            totalSnow, totalPrecip, maxGust, maxTemp, minTemp);
    }
}
