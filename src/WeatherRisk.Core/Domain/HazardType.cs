namespace WeatherRisk.Core.Domain;

/// <summary>Weather hazard categories that can disrupt hub operations.</summary>
public enum HazardType
{
    /// <summary>Snow, ice and extreme cold — road closures, yard and dock shutdowns.</summary>
    Winter,

    /// <summary>Hurricanes and tropical storms — wind damage, evacuations, multi-day closures.</summary>
    Hurricane,

    /// <summary>Riverine and coastal flooding from heavy rain or storm surge — access cut off, inventory damage.</summary>
    Flood,

    /// <summary>Tornadoes, hail and damaging straight-line winds — facility damage, power loss.</summary>
    SevereStorm,

    /// <summary>Extreme heat — worker safety limits, equipment failure, perishable losses.</summary>
    Heat,
}
