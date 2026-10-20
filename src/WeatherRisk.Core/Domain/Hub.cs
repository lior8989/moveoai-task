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
    string CountyFips);
