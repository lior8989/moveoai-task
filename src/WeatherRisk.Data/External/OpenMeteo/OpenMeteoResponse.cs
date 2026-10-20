using System.Text.Json.Serialization;

namespace WeatherRisk.Data.External.OpenMeteo;

/// <summary>Subset of the Open-Meteo archive response we read (column-oriented daily arrays).</summary>
internal sealed class OpenMeteoResponse
{
    /// <summary>Daily series; each array is index-aligned with <see cref="OpenMeteoDaily.Time"/>.</summary>
    [JsonPropertyName("daily")]
    public OpenMeteoDaily? Daily { get; init; }
}

/// <summary>Daily arrays as returned by Open-Meteo. Values may be null for missing observations.</summary>
internal sealed class OpenMeteoDaily
{
    /// <summary>ISO dates (yyyy-MM-dd), hub-local because the request uses <c>timezone=auto</c>.</summary>
    [JsonPropertyName("time")]
    public string[]? Time { get; init; }

    /// <summary>Snowfall sum, cm.</summary>
    [JsonPropertyName("snowfall_sum")]
    public double?[]? SnowfallSum { get; init; }

    /// <summary>Precipitation sum, mm.</summary>
    [JsonPropertyName("precipitation_sum")]
    public double?[]? PrecipitationSum { get; init; }

    /// <summary>Max wind gust at 10 m, km/h.</summary>
    [JsonPropertyName("wind_gusts_10m_max")]
    public double?[]? WindGustsMax { get; init; }

    /// <summary>Max temperature at 2 m, °C.</summary>
    [JsonPropertyName("temperature_2m_max")]
    public double?[]? TemperatureMax { get; init; }

    /// <summary>Min temperature at 2 m, °C.</summary>
    [JsonPropertyName("temperature_2m_min")]
    public double?[]? TemperatureMin { get; init; }
}

/// <summary>Source-generated serializer metadata: no reflection at runtime, faster deserialization.</summary>
[JsonSerializable(typeof(OpenMeteoResponse))]
internal sealed partial class OpenMeteoJsonContext : JsonSerializerContext;
