namespace WeatherRisk.Core.Weather;

/// <summary>One day of observed weather at a hub location (as returned by a historical weather source).</summary>
/// <remarks>
/// A value type: tens of thousands of these are aggregated per request, so avoiding heap allocations matters.
/// Values are nullable because sources occasionally have gaps; a missing value never counts as a disruption day.
/// </remarks>
/// <param name="Date">Calendar date (local to the hub).</param>
/// <param name="SnowfallCm">Total snowfall in centimetres.</param>
/// <param name="PrecipitationMm">Total precipitation (rain + melted snow) in millimetres.</param>
/// <param name="MaxWindGustKmh">Maximum wind gust in km/h.</param>
/// <param name="MaxTemperatureC">Maximum air temperature at 2 m, °C.</param>
/// <param name="MinTemperatureC">Minimum air temperature at 2 m, °C.</param>
public readonly record struct DailyWeather(
    DateOnly Date,
    double? SnowfallCm,
    double? PrecipitationMm,
    double? MaxWindGustKmh,
    double? MaxTemperatureC,
    double? MinTemperatureC);
