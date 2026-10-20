namespace WeatherRisk.Core.Weather;

/// <summary>
/// Thresholds that turn a day of weather into a "disruption day" for a hazard. All comparisons are inclusive (≥ / ≤).
/// </summary>
/// <remarks>
/// Defaults are informed by common operational and meteorological conventions; they are assumptions, not
/// calibrated against company disruption records (see docs/ARCHITECTURE.md → Assumptions).
/// </remarks>
public sealed class WeatherThresholds
{
    /// <summary>Snowfall (cm) at or above which a day counts as a snow day. Default 1 cm: enough to require plowing/treatment.</summary>
    public double SnowDayCm { get; init; } = 1.0;

    /// <summary>Minimum temperature (°C) at or below which a day counts as a hard freeze. Default −12 °C (~10 °F): equipment and road-salt effectiveness degrade.</summary>
    public double HardFreezeC { get; init; } = -12.0;

    /// <summary>Daily precipitation (mm) at or above which a day counts as heavy rain. Default 50 mm (~2 in): flash-flood guidance range for many US areas.</summary>
    public double HeavyRainMm { get; init; } = 50.0;

    /// <summary>Wind gust (km/h) at or above which a day counts as a damaging-wind day. Default 75 km/h (~47 mph): close to the US severe-thunderstorm wind criterion (58 mph) at daily-max resolution.</summary>
    public double DamagingGustKmh { get; init; } = 75.0;

    /// <summary>Wind gust (km/h) at or above which a day counts as tropical-storm-force. Default 90 km/h (~56 mph).</summary>
    public double TropicalStormGustKmh { get; init; } = 90.0;

    /// <summary>Maximum temperature (°C) at or above which a day counts as extreme heat. Default 38 °C (100 °F).</summary>
    public double ExtremeHeatC { get; init; } = 38.0;
}
