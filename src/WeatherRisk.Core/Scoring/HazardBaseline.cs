namespace WeatherRisk.Core.Scoring;

/// <summary>
/// FEMA National Risk Index (NRI) hazard risk scores for a hub's county. Each value is a 0–100 national
/// percentile (higher = more risk relative to other US counties); null when FEMA has no rating for that hazard.
/// </summary>
/// <remarks>
/// NRI captures long-term hazard history (decades of events, including rare hurricanes/tornadoes) that a
/// few years of daily weather can miss — hence it serves as the "baseline" signal in scoring.
/// </remarks>
/// <param name="WinterWeather">NRI Winter Weather risk score.</param>
/// <param name="IceStorm">NRI Ice Storm risk score.</param>
/// <param name="ColdWave">NRI Cold Wave risk score.</param>
/// <param name="Hurricane">NRI Hurricane risk score.</param>
/// <param name="RiverineFlooding">NRI Riverine Flooding risk score.</param>
/// <param name="CoastalFlooding">NRI Coastal Flooding risk score (null/0 for inland counties).</param>
/// <param name="Tornado">NRI Tornado risk score.</param>
/// <param name="Hail">NRI Hail risk score.</param>
/// <param name="StrongWind">NRI Strong Wind risk score.</param>
/// <param name="HeatWave">NRI Heat Wave risk score.</param>
public sealed record HazardBaseline(
    double? WinterWeather,
    double? IceStorm,
    double? ColdWave,
    double? Hurricane,
    double? RiverineFlooding,
    double? CoastalFlooding,
    double? Tornado,
    double? Hail,
    double? StrongWind,
    double? HeatWave);
