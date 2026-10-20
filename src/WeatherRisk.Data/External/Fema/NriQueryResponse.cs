using System.Text.Json.Serialization;

namespace WeatherRisk.Data.External.Fema;

/// <summary>ArcGIS feature-service query response (subset). ArcGIS reports errors as HTTP 200 with an <c>error</c> body.</summary>
internal sealed class NriQueryResponse
{
    /// <summary>Returned rows.</summary>
    [JsonPropertyName("features")]
    public NriFeature[]? Features { get; init; }

    /// <summary>True when more rows exist beyond this page.</summary>
    [JsonPropertyName("exceededTransferLimit")]
    public bool ExceededTransferLimit { get; init; }

    /// <summary>Set when the query failed.</summary>
    [JsonPropertyName("error")]
    public ArcGisError? Error { get; init; }
}

/// <summary>One feature (county row).</summary>
internal sealed class NriFeature
{
    /// <summary>Requested attribute values.</summary>
    [JsonPropertyName("attributes")]
    public NriCountyAttributes? Attributes { get; init; }
}

/// <summary>
/// Annualized frequency (<c>*_AFREQ</c>) attributes per county: expected events (or event-days) per year,
/// independent of population and property exposure. Null = hazard not applicable / not assessed.
/// </summary>
internal sealed class NriCountyAttributes
{
    /// <summary>5-digit state+county FIPS code.</summary>
    [JsonPropertyName("STCOFIPS")] public string? StateCountyFips { get; init; }

    /// <summary>Winter Weather frequency.</summary>
    [JsonPropertyName("WNTW_AFREQ")] public double? WinterWeather { get; init; }

    /// <summary>Ice Storm frequency.</summary>
    [JsonPropertyName("ISTM_AFREQ")] public double? IceStorm { get; init; }

    /// <summary>Cold Wave frequency.</summary>
    [JsonPropertyName("CWAV_AFREQ")] public double? ColdWave { get; init; }

    /// <summary>Hurricane frequency.</summary>
    [JsonPropertyName("HRCN_AFREQ")] public double? Hurricane { get; init; }

    /// <summary>Inland Flooding frequency (formerly "Riverine Flooding").</summary>
    [JsonPropertyName("IFLD_AFREQ")] public double? InlandFlooding { get; init; }

    /// <summary>Coastal Flooding frequency.</summary>
    [JsonPropertyName("CFLD_AFREQ")] public double? CoastalFlooding { get; init; }

    /// <summary>Tornado frequency.</summary>
    [JsonPropertyName("TRND_AFREQ")] public double? Tornado { get; init; }

    /// <summary>Hail frequency.</summary>
    [JsonPropertyName("HAIL_AFREQ")] public double? Hail { get; init; }

    /// <summary>Strong Wind frequency.</summary>
    [JsonPropertyName("SWND_AFREQ")] public double? StrongWind { get; init; }

    /// <summary>Heat Wave frequency.</summary>
    [JsonPropertyName("HWAV_AFREQ")] public double? HeatWave { get; init; }

    /// <summary>
    /// Values in <see cref="Core.Scoring.HazardBaseline"/> parameter order — the single place that defines
    /// the mapping from NRI fields to baseline components.
    /// </summary>
    /// <returns>Ten frequencies.</returns>
    public double?[] ToArray() =>
        [WinterWeather, IceStorm, ColdWave, Hurricane, InlandFlooding, CoastalFlooding, Tornado, Hail, StrongWind, HeatWave];
}

/// <summary>ArcGIS error payload.</summary>
internal sealed class ArcGisError
{
    /// <summary>Error code.</summary>
    [JsonPropertyName("code")] public int Code { get; init; }

    /// <summary>Error message.</summary>
    [JsonPropertyName("message")] public string? Message { get; init; }
}

/// <summary>Source-generated serializer metadata.</summary>
[JsonSerializable(typeof(NriQueryResponse))]
internal sealed partial class NriJsonContext : JsonSerializerContext;
