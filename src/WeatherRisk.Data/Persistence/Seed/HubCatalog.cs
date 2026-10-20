using WeatherRisk.Core.Domain;

namespace WeatherRisk.Data.Persistence.Seed;

/// <summary>
/// The 15 hubs seeded into the database. Real US cities (the company's actual hub list was not provided);
/// includes every city named in the brief (Miami, Houston, Denver, Dallas) and five Midwest hubs so
/// regional rankings are meaningful.
/// </summary>
/// <remarks>
/// Coordinates are city centres (WGS84). County FIPS codes identify the county the city centre lies in —
/// used to look up FEMA National Risk Index baselines.
/// </remarks>
public static class HubCatalog
{
    /// <summary>All seeded hubs.</summary>
    public static IReadOnlyList<Hub> All { get; } =
    [
        // Midwest
        new("chicago", "Chicago Hub", "Chicago", "IL", Region.Midwest, 41.8781, -87.6298, "17031"),            // Cook County
        new("minneapolis", "Minneapolis Hub", "Minneapolis", "MN", Region.Midwest, 44.9778, -93.2650, "27053"), // Hennepin County
        new("detroit", "Detroit Hub", "Detroit", "MI", Region.Midwest, 42.3314, -83.0458, "26163"),            // Wayne County
        new("indianapolis", "Indianapolis Hub", "Indianapolis", "IN", Region.Midwest, 39.7684, -86.1581, "18097"), // Marion County
        new("kansas-city", "Kansas City Hub", "Kansas City", "MO", Region.Midwest, 39.0997, -94.5786, "29095"), // Jackson County

        // South
        new("miami", "Miami Hub", "Miami", "FL", Region.South, 25.7617, -80.1918, "12086"),                     // Miami-Dade County
        new("houston", "Houston Hub", "Houston", "TX", Region.South, 29.7604, -95.3698, "48201"),               // Harris County
        new("dallas", "Dallas Hub", "Dallas", "TX", Region.South, 32.7767, -96.7970, "48113"),                  // Dallas County
        new("new-orleans", "New Orleans Hub", "New Orleans", "LA", Region.South, 29.9511, -90.0715, "22071"),   // Orleans Parish
        new("atlanta", "Atlanta Hub", "Atlanta", "GA", Region.South, 33.7490, -84.3880, "13121"),               // Fulton County

        // West
        new("denver", "Denver Hub", "Denver", "CO", Region.West, 39.7392, -104.9903, "08031"),                  // Denver County
        new("phoenix", "Phoenix Hub", "Phoenix", "AZ", Region.West, 33.4484, -112.0740, "04013"),               // Maricopa County
        new("seattle", "Seattle Hub", "Seattle", "WA", Region.West, 47.6062, -122.3321, "53033"),               // King County
        new("los-angeles", "Los Angeles Hub", "Los Angeles", "CA", Region.West, 34.0522, -118.2437, "06037"),   // Los Angeles County

        // Northeast
        new("new-york", "New York Hub", "New York", "NY", Region.Northeast, 40.7128, -74.0060, "36061"),        // New York County
    ];
}
