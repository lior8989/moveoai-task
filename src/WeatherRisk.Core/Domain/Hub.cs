namespace WeatherRisk.Core.Domain;

/// <summary>A regional distribution hub whose weather exposure is assessed.</summary>
/// <param name="Id">Stable identifier used in APIs and tool calls, e.g. <c>dallas</c>.</param>
/// <param name="Name">Display name, e.g. <c>Dallas Hub</c>.</param>
/// <param name="City">City the hub is located in.</param>
/// <param name="State">Two-letter US state code.</param>
/// <param name="Region">US Census region, used for filters such as "hubs in the Midwest".</param>
/// <param name="Latitude">Latitude in decimal degrees (WGS84) — used to query weather data.</param>
/// <param name="Longitude">Longitude in decimal degrees (WGS84).</param>
/// <param name="CountyFips">5-digit county FIPS code — used to look up the FEMA National Risk Index baseline.</param>
public sealed record Hub(
    string Id,
    string Name,
    string City,
    string State,
    Region Region,
    double Latitude,
    double Longitude,
    string CountyFips)
{
    /// <summary>Normalizes a user- or LLM-supplied hub id to the stored form (trimmed, lower-case).</summary>
    /// <param name="id">Raw id, e.g. <c>" Dallas "</c>.</param>
    /// <returns>Normalized id, e.g. <c>dallas</c>.</returns>
    public static string NormalizeId(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.Trim().ToLowerInvariant();
    }
}
