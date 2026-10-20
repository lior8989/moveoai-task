namespace WeatherRisk.Core.Scoring;

/// <summary>
/// Long-term hazard baseline for a hub's county, derived from the FEMA National Risk Index (NRI).
/// Each value is the county's <b>national percentile (0–100) of annualized hazard frequency</b> — how often the
/// hazard occurs there relative to all US counties. Null when the hazard does not apply to the county.
/// </summary>
/// <remarks>
/// Frequency is used instead of NRI's "risk score" because the risk score scales with the county's population
/// and property value, so every large metro county scores near 100 on almost every hazard; frequency measures
/// the hazard itself, which is what a single facility is exposed to.
/// NRI captures decades of events (including rare hurricanes/tornadoes) that a few years of daily weather can
/// miss — hence it serves as the "baseline" signal in scoring.
/// </remarks>
/// <param name="WinterWeather">Winter Weather frequency percentile.</param>
/// <param name="IceStorm">Ice Storm frequency percentile.</param>
/// <param name="ColdWave">Cold Wave frequency percentile.</param>
/// <param name="Hurricane">Hurricane frequency percentile (null for counties outside hurricane reach).</param>
/// <param name="RiverineFlooding">Inland (riverine) flooding frequency percentile — NRI field <c>IFLD</c>, formerly "Riverine Flooding".</param>
/// <param name="CoastalFlooding">Coastal Flooding frequency percentile (null for inland counties).</param>
/// <param name="Tornado">Tornado frequency percentile.</param>
/// <param name="Hail">Hail frequency percentile.</param>
/// <param name="StrongWind">Strong Wind frequency percentile.</param>
/// <param name="HeatWave">Heat Wave frequency percentile.</param>
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
