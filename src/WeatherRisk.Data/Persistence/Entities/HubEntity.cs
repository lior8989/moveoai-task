using WeatherRisk.Core.Domain;

namespace WeatherRisk.Data.Persistence.Entities;

/// <summary>Row of the <c>Hubs</c> table. Mapped to <see cref="Hub"/> by repositories; never exposed over the API.</summary>
public sealed class HubEntity
{
    /// <summary>Primary key: lower-case hub id, e.g. <c>dallas</c>.</summary>
    public required string Id { get; set; }

    /// <summary>Display name.</summary>
    public required string Name { get; set; }

    /// <summary>City.</summary>
    public required string City { get; set; }

    /// <summary>Two-letter state code.</summary>
    public required string State { get; set; }

    /// <summary>US Census region (stored as text).</summary>
    public Region Region { get; set; }

    /// <summary>Latitude, decimal degrees.</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude, decimal degrees.</summary>
    public double Longitude { get; set; }

    /// <summary>5-digit county FIPS code.</summary>
    public required string CountyFips { get; set; }

    /// <summary>Maps to the domain model.</summary>
    /// <returns>The domain hub.</returns>
    public Hub ToDomain() => new(Id, Name, City, State, Region, Latitude, Longitude, CountyFips);

    /// <summary>Maps from the domain model.</summary>
    /// <param name="hub">Domain hub.</param>
    /// <returns>The entity.</returns>
    public static HubEntity FromDomain(Hub hub) => new()
    {
        Id = hub.Id,
        Name = hub.Name,
        City = hub.City,
        State = hub.State,
        Region = hub.Region,
        Latitude = hub.Latitude,
        Longitude = hub.Longitude,
        CountyFips = hub.CountyFips,
    };
}
