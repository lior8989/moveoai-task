using System.Globalization;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Application;

/// <summary>
/// Describes the configured risk model in plain terms (weights, thresholds, signals, bands, assumptions).
/// Built from the live settings, so the description can't drift from what the engine actually does.
/// Used by the REST API (<c>/api/risk/model</c>) and by the agent's <c>describe_risk_model</c> tool.
/// </summary>
/// <remarks>Singleton; building the description is a handful of string operations, so it is done per call.</remarks>
public sealed class RiskModelDescriber
{
    private readonly ScoringSettings _scoring;
    private readonly WeatherThresholds _thresholds;

    /// <summary>Creates the describer.</summary>
    /// <param name="scoring">Scoring settings.</param>
    /// <param name="thresholds">Weather thresholds.</param>
    public RiskModelDescriber(ScoringSettings scoring, WeatherThresholds thresholds)
    {
        _scoring = scoring;
        _thresholds = thresholds;
    }

    /// <summary>Builds the description.</summary>
    /// <param name="window">Current observed-weather window.</param>
    /// <returns>Model description.</returns>
    public RiskModelDescription Describe(DataWindow window)
    {
        double totalWeight = Enum.GetValues<HazardType>().Sum(h => _scoring.For(h).CompositeWeight);
        double Weight(HazardType h) => Math.Round(_scoring.For(h).CompositeWeight / totalWeight, 3);
        string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        HazardModelDescription[] hazards =
        [
            new(HazardType.Winter, Weight(HazardType.Winter), _scoring.Winter.ObservedWeight,
                $"{F(_scoring.Winter.SnowShare * 100)}% snow days (snowfall ≥ {F(_thresholds.SnowDayCm)} cm; {F(_scoring.Winter.SnowDaysPerYearCap)}/yr = 100) + " +
                $"{F((1 - _scoring.Winter.SnowShare) * 100)}% hard-freeze days (min ≤ {F(_thresholds.HardFreezeC)} °C; {F(_scoring.Winter.HardFreezeDaysPerYearCap)}/yr = 100)",
                "FEMA NRI Winter Weather, Ice Storm, Cold Wave frequency percentiles"),
            new(HazardType.Hurricane, Weight(HazardType.Hurricane), _scoring.Hurricane.ObservedWeight,
                $"Gust ≥ {F(_thresholds.TropicalStormGustKmh)} km/h days ({F(_scoring.Hurricane.DaysPerYearCap)}/yr = 100) — evidence only; used if FEMA data is unavailable",
                "FEMA NRI Hurricane frequency percentile"),
            new(HazardType.Flood, Weight(HazardType.Flood), _scoring.Flood.ObservedWeight,
                $"Precipitation ≥ {F(_thresholds.HeavyRainMm)} mm days ({F(_scoring.Flood.DaysPerYearCap)}/yr = 100)",
                "FEMA NRI Inland and Coastal Flooding frequency percentiles"),
            new(HazardType.SevereStorm, Weight(HazardType.SevereStorm), _scoring.SevereStorm.ObservedWeight,
                $"Gust ≥ {F(_thresholds.DamagingGustKmh)} km/h days ({F(_scoring.SevereStorm.DaysPerYearCap)}/yr = 100)",
                "FEMA NRI Tornado, Hail, Strong Wind frequency percentiles"),
            new(HazardType.Heat, Weight(HazardType.Heat), _scoring.Heat.ObservedWeight,
                $"Max temperature ≥ {F(_thresholds.ExtremeHeatC)} °C days ({F(_scoring.Heat.DaysPerYearCap)}/yr = 100)",
                "FEMA NRI Heat Wave frequency percentile"),
        ];

        return new RiskModelDescription(
            "Deterministic, rule-based index (not machine learning). Each hazard score (0–100) blends observed " +
            "disruption days at the hub (Open-Meteo, last full years) with the county's long-term FEMA hazard " +
            "frequency percentile. Hazards FEMA rates not applicable score 0. Composite = " +
            $"{F(_scoring.WorstHazardBlend * 100)}% worst hazard + {F((1 - _scoring.WorstHazardBlend) * 100)}% weighted average, " +
            "so one extreme hazard is not diluted by benign ones. Contributions per hazard sum to the composite.",
            window.From,
            window.To,
            _scoring.WorstHazardBlend,
            _scoring.MinimumCoverage,
            hazards,
            [
                new(RiskRating.Low, 0, RiskRatings.ModerateFrom),
                new(RiskRating.Moderate, RiskRatings.ModerateFrom, RiskRatings.HighFrom),
                new(RiskRating.High, RiskRatings.HighFrom, RiskRatings.VeryHighFrom),
                new(RiskRating.VeryHigh, RiskRatings.VeryHighFrom, null),
            ],
            [
                "Open-Meteo Historical Weather API (daily reanalysis at hub coordinates)",
                "FEMA National Risk Index (county annualized hazard frequency, national percentile)",
            ],
            [
                "Weights, thresholds and caps are expert assumptions, not calibrated on company disruption data.",
                "Scores rank relative exposure; they are not predictions of closure days or cost.",
                "Daily gusts are a coarse proxy for tornadoes/hail; FEMA frequency dominates those hazards.",
                "Hub locations are city centres; county-level FEMA data may differ from the exact site.",
            ]);
    }
}

/// <summary>The deterministic risk model as currently configured.</summary>
/// <param name="Summary">One-paragraph description of how scores are computed.</param>
/// <param name="WindowFrom">First day of the observed-weather window.</param>
/// <param name="WindowTo">Last day of the observed-weather window.</param>
/// <param name="WorstHazardBlend">Share of the composite taken from the worst hazard.</param>
/// <param name="MinimumCoverage">Weather coverage required for full confidence (0–1).</param>
/// <param name="Hazards">Per-hazard signals and weights.</param>
/// <param name="RatingBands">Score bands.</param>
/// <param name="DataSources">External data sources.</param>
/// <param name="Assumptions">Key assumptions and limitations.</param>
public sealed record RiskModelDescription(
    string Summary,
    DateOnly WindowFrom,
    DateOnly WindowTo,
    double WorstHazardBlend,
    double MinimumCoverage,
    IReadOnlyList<HazardModelDescription> Hazards,
    IReadOnlyList<RatingBand> RatingBands,
    IReadOnlyList<string> DataSources,
    IReadOnlyList<string> Assumptions);

/// <summary>How one hazard is scored.</summary>
/// <param name="Hazard">Hazard.</param>
/// <param name="CompositeWeight">Normalized weight in the weighted average.</param>
/// <param name="ObservedWeight">Share of the hazard score from observed weather.</param>
/// <param name="ObservedSignal">Observed disruption-day definition and cap.</param>
/// <param name="BaselineSignal">FEMA NRI components averaged into the baseline.</param>
public sealed record HazardModelDescription(
    HazardType Hazard, double CompositeWeight, double ObservedWeight, string ObservedSignal, string BaselineSignal);

/// <summary>A rating band.</summary>
/// <param name="Rating">Rating.</param>
/// <param name="FromInclusive">Lower bound (inclusive).</param>
/// <param name="ToExclusive">Upper bound (exclusive); null for the top band.</param>
public sealed record RatingBand(RiskRating Rating, double FromInclusive, double? ToExclusive);
