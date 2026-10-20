namespace WeatherRisk.Core.Domain;

/// <summary>US Census Bureau regions — a standard, unambiguous way to group hubs geographically.</summary>
public enum Region
{
    /// <summary>Midwest (e.g. Chicago, Minneapolis, Detroit, Indianapolis, Kansas City).</summary>
    Midwest,

    /// <summary>South, including the Gulf Coast and Texas (e.g. Miami, Houston, Dallas, New Orleans, Atlanta).</summary>
    South,

    /// <summary>West, including the Mountain states (e.g. Denver, Phoenix, Seattle, Los Angeles).</summary>
    West,

    /// <summary>Northeast (e.g. New York).</summary>
    Northeast,
}
