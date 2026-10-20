using WeatherRisk.Core.Scoring;

namespace WeatherRisk.Data.Persistence.Entities;

/// <summary>Row of the <c>HazardBaselines</c> table: cached FEMA NRI risk scores for one county.</summary>
public sealed class HazardBaselineEntity
{
    /// <summary>5-digit county FIPS code (primary key).</summary>
    public required string CountyFips { get; set; }

    /// <summary>NRI Winter Weather risk score.</summary>
    public double? WinterWeather { get; set; }

    /// <summary>NRI Ice Storm risk score.</summary>
    public double? IceStorm { get; set; }

    /// <summary>NRI Cold Wave risk score.</summary>
    public double? ColdWave { get; set; }

    /// <summary>NRI Hurricane risk score.</summary>
    public double? Hurricane { get; set; }

    /// <summary>NRI Riverine Flooding risk score.</summary>
    public double? RiverineFlooding { get; set; }

    /// <summary>NRI Coastal Flooding risk score.</summary>
    public double? CoastalFlooding { get; set; }

    /// <summary>NRI Tornado risk score.</summary>
    public double? Tornado { get; set; }

    /// <summary>NRI Hail risk score.</summary>
    public double? Hail { get; set; }

    /// <summary>NRI Strong Wind risk score.</summary>
    public double? StrongWind { get; set; }

    /// <summary>NRI Heat Wave risk score.</summary>
    public double? HeatWave { get; set; }

    /// <summary>When the data was fetched from FEMA.</summary>
    public DateTimeOffset FetchedAt { get; set; }

    /// <summary>Maps to the domain model.</summary>
    /// <returns>The domain baseline.</returns>
    public HazardBaseline ToDomain() => new(
        WinterWeather, IceStorm, ColdWave, Hurricane, RiverineFlooding, CoastalFlooding, Tornado, Hail, StrongWind, HeatWave);

    /// <summary>Copies domain values into this entity (used for inserts and updates).</summary>
    /// <param name="baseline">Domain baseline.</param>
    /// <param name="fetchedAt">Fetch time.</param>
    public void Apply(HazardBaseline baseline, DateTimeOffset fetchedAt)
    {
        WinterWeather = baseline.WinterWeather;
        IceStorm = baseline.IceStorm;
        ColdWave = baseline.ColdWave;
        Hurricane = baseline.Hurricane;
        RiverineFlooding = baseline.RiverineFlooding;
        CoastalFlooding = baseline.CoastalFlooding;
        Tornado = baseline.Tornado;
        Hail = baseline.Hail;
        StrongWind = baseline.StrongWind;
        HeatWave = baseline.HeatWave;
        FetchedAt = fetchedAt;
    }
}
