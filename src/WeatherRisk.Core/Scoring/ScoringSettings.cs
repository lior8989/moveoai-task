using WeatherRisk.Core.Domain;

namespace WeatherRisk.Core.Scoring;

/// <summary>
/// All tunable numbers of the risk model: per-hazard caps and blends, composite weights, and the worst-hazard blend.
/// Defaults are the documented model; override via configuration without code changes.
/// </summary>
/// <remarks>These values are expert assumptions, not calibrated weights (see docs/ARCHITECTURE.md §6).</remarks>
public sealed class ScoringSettings
{
    /// <summary>
    /// Share of the composite taken from the hub's single worst hazard (0–1); the rest is the weighted average.
    /// Default 0.5 — stops one extreme hazard (e.g. Miami hurricanes) from being diluted by benign ones.
    /// </summary>
    public double WorstHazardBlend { get; init; } = 0.5;

    /// <summary>
    /// Minimum weather-data coverage (0–1) of the look-back window to treat the observed signal as complete.
    /// Default 0.9.
    /// </summary>
    public double MinimumCoverage { get; init; } = 0.9;

    /// <summary>Winter scoring parameters.</summary>
    public WinterScoringSettings Winter { get; init; } = new();

    /// <summary>
    /// Hurricane scoring parameters. Observed weight is 0: real data showed daily gusts ≥ 90 km/h also come from
    /// nor'easters (New York) and downslope winds (Denver), so they can't identify hurricanes. The gust-day rate
    /// is still reported as evidence and is used alone only if the FEMA baseline is unavailable.
    /// </summary>
    public SingleMetricScoringSettings Hurricane { get; init; } = new()
    {
        CompositeWeight = 0.20, ObservedWeight = 0.0, DaysPerYearCap = 3,
    };

    /// <summary>Flood scoring parameters.</summary>
    public SingleMetricScoringSettings Flood { get; init; } = new()
    {
        CompositeWeight = 0.20, ObservedWeight = 0.50, DaysPerYearCap = 6,
    };

    /// <summary>Severe-storm scoring parameters. Tornado/hail are poorly captured by daily gusts, so NRI dominates.</summary>
    public SingleMetricScoringSettings SevereStorm { get; init; } = new()
    {
        CompositeWeight = 0.20, ObservedWeight = 0.40, DaysPerYearCap = 12,
    };

    /// <summary>Heat scoring parameters.</summary>
    public SingleMetricScoringSettings Heat { get; init; } = new()
    {
        CompositeWeight = 0.15, ObservedWeight = 0.60, DaysPerYearCap = 45,
    };

    /// <summary>Returns the parameters for a hazard.</summary>
    /// <param name="hazard">Hazard type.</param>
    /// <returns>That hazard's settings.</returns>
    public HazardScoringSettings For(HazardType hazard) => hazard switch
    {
        HazardType.Winter => Winter,
        HazardType.Hurricane => Hurricane,
        HazardType.Flood => Flood,
        HazardType.SevereStorm => SevereStorm,
        HazardType.Heat => Heat,
        _ => throw new ArgumentOutOfRangeException(nameof(hazard), hazard, "Unknown hazard."),
    };

    /// <summary>Checks the settings are internally consistent.</summary>
    /// <exception cref="ArgumentException">When a weight/blend is out of range or all composite weights are zero.</exception>
    public void Validate()
    {
        RequireUnitInterval(WorstHazardBlend, nameof(WorstHazardBlend));
        RequireUnitInterval(MinimumCoverage, nameof(MinimumCoverage));
        RequireUnitInterval(Winter.SnowShare, $"{nameof(Winter)}.{nameof(Winter.SnowShare)}");

        double totalWeight = 0;
        foreach (HazardType hazard in Enum.GetValues<HazardType>())
        {
            HazardScoringSettings settings = For(hazard);
            RequireUnitInterval(settings.ObservedWeight, $"{hazard}.{nameof(settings.ObservedWeight)}");
            if (settings.CompositeWeight < 0)
            {
                throw new ArgumentException($"{hazard}.{nameof(settings.CompositeWeight)} must be ≥ 0.");
            }

            totalWeight += settings.CompositeWeight;
        }

        if (totalWeight <= 0)
        {
            throw new ArgumentException("At least one hazard must have a positive composite weight.");
        }

        if (Winter.SnowDaysPerYearCap <= 0 || Winter.HardFreezeDaysPerYearCap <= 0 ||
            Hurricane.DaysPerYearCap <= 0 || Flood.DaysPerYearCap <= 0 ||
            SevereStorm.DaysPerYearCap <= 0 || Heat.DaysPerYearCap <= 0)
        {
            throw new ArgumentException("All days-per-year caps must be positive.");
        }
    }

    private static void RequireUnitInterval(double value, string name)
    {
        if (value is < 0 or > 1 || double.IsNaN(value))
        {
            throw new ArgumentException($"{name} must be between 0 and 1 (was {value}).");
        }
    }
}

/// <summary>Parameters shared by every hazard.</summary>
public class HazardScoringSettings
{
    /// <summary>Relative weight of this hazard in the composite's weighted-average part (weights are normalized).</summary>
    public double CompositeWeight { get; init; }

    /// <summary>Share (0–1) of the hazard score taken from observed weather; the rest comes from the FEMA NRI baseline.</summary>
    public double ObservedWeight { get; init; }
}

/// <summary>Parameters for hazards measured by a single disruption-day metric.</summary>
public sealed class SingleMetricScoringSettings : HazardScoringSettings
{
    /// <summary>Disruption days per year at which the observed score reaches 100 (linear below).</summary>
    public double DaysPerYearCap { get; init; }
}

/// <summary>Winter parameters: combines snow days and hard-freeze days.</summary>
public sealed class WinterScoringSettings : HazardScoringSettings
{
    /// <summary>Creates winter settings with the documented defaults.</summary>
    public WinterScoringSettings()
    {
        CompositeWeight = 0.25;
        ObservedWeight = 0.60;
    }

    /// <summary>Snow days per year at which the snow component reaches 100. Default 40.</summary>
    public double SnowDaysPerYearCap { get; init; } = 40;

    /// <summary>Hard-freeze days per year at which the freeze component reaches 100. Default 30.</summary>
    public double HardFreezeDaysPerYearCap { get; init; } = 30;

    /// <summary>Share (0–1) of the observed winter score from snow; the rest from hard freezes. Default 0.6.</summary>
    public double SnowShare { get; init; } = 0.6;
}
