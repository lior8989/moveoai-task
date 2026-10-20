using WeatherRisk.Core.Weather;

namespace WeatherRisk.Data.Persistence.Entities;

/// <summary>Row of the <c>DailyWeather</c> table: one day of cached weather for one hub.</summary>
public sealed class DailyWeatherEntity
{
    /// <summary>Hub id (part of the composite primary key, FK to <c>Hubs</c>).</summary>
    public required string HubId { get; set; }

    /// <summary>Calendar date (part of the composite primary key).</summary>
    public DateOnly Date { get; set; }

    /// <summary>Snowfall, cm.</summary>
    public double? SnowfallCm { get; set; }

    /// <summary>Precipitation, mm.</summary>
    public double? PrecipitationMm { get; set; }

    /// <summary>Maximum wind gust, km/h.</summary>
    public double? MaxWindGustKmh { get; set; }

    /// <summary>Maximum temperature, °C.</summary>
    public double? MaxTemperatureC { get; set; }

    /// <summary>Minimum temperature, °C.</summary>
    public double? MinTemperatureC { get; set; }

    /// <summary>Maps from the domain model.</summary>
    /// <param name="hubId">Owning hub id.</param>
    /// <param name="day">Domain value.</param>
    /// <returns>The entity.</returns>
    public static DailyWeatherEntity FromDomain(string hubId, DailyWeather day) => new()
    {
        HubId = hubId,
        Date = day.Date,
        SnowfallCm = day.SnowfallCm,
        PrecipitationMm = day.PrecipitationMm,
        MaxWindGustKmh = day.MaxWindGustKmh,
        MaxTemperatureC = day.MaxTemperatureC,
        MinTemperatureC = day.MinTemperatureC,
    };
}
