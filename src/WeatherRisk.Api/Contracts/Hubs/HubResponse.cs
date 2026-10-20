using WeatherRisk.Core.Domain;

namespace WeatherRisk.Api.Contracts.Hubs;

/// <summary>A distribution hub.</summary>
/// <param name="Id">Hub id used in all other endpoints, e.g. <c>dallas</c>.</param>
/// <param name="Name">Display name.</param>
/// <param name="City">City.</param>
/// <param name="State">Two-letter US state code.</param>
/// <param name="Region">US Census region.</param>
/// <param name="Latitude">Latitude, decimal degrees.</param>
/// <param name="Longitude">Longitude, decimal degrees.</param>
/// <param name="CountyFips">5-digit county FIPS code (FEMA NRI lookup).</param>
public sealed record HubResponse(
    string Id, string Name, string City, string State, Region Region, double Latitude, double Longitude, string CountyFips);
