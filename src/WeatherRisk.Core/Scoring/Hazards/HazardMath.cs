using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Scoring.Hazards;

/// <summary>Shared arithmetic for hazard scorers: normalization, blending, and the single-metric scoring recipe.</summary>
internal static class HazardMath
{
    /// <summary>Linearly maps a value onto 0–100, reaching 100 at <paramref name="cap"/> and clamping above/below.</summary>
    /// <param name="value">Raw value, e.g. disruption days per year.</param>
    /// <param name="cap">Value that maps to 100; must be positive.</param>
    /// <returns>Normalized score 0–100.</returns>
    public static double Normalize(double value, double cap) => Math.Clamp(value / cap * 100.0, 0, 100);

    /// <summary>
    /// Blends observed and baseline scores. When only one signal exists it is used alone; when neither exists, 0.
    /// </summary>
    /// <param name="observed">Observed score or null.</param>
    /// <param name="baseline">Baseline score or null.</param>
    /// <param name="observedWeight">Weight (0–1) of the observed score when both exist.</param>
    /// <returns>Blended score 0–100.</returns>
    public static double Blend(double? observed, double? baseline, double observedWeight) => (observed, baseline) switch
    {
        ({ } o, { } b) => (observedWeight * o) + ((1 - observedWeight) * b),
        ({ } o, null) => o,
        (null, { } b) => b,
        _ => 0,
    };

    /// <summary>Averages the non-null values; null when all are null (FEMA has no rating for that hazard).</summary>
    /// <param name="values">Candidate values.</param>
    /// <returns>Mean of available values, or null.</returns>
    public static double? AverageOfAvailable(params ReadOnlySpan<double?> values)
    {
        double sum = 0;
        int count = 0;
        foreach (double? value in values)
        {
            if (value is { } v)
            {
                sum += v;
                count++;
            }
        }

        return count == 0 ? null : sum / count;
    }

    /// <summary>Rounds for presentation (scores to 1 decimal keep outputs stable and readable).</summary>
    /// <param name="value">Value to round.</param>
    /// <param name="decimals">Decimal places.</param>
    /// <returns>Rounded value (midpoints away from zero).</returns>
    public static double Round(double value, int decimals = 1) => Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Score for a hazard FEMA rates as not applicable to the county (baseline present, all its components null) —
    /// e.g. hurricanes in Denver. Scores 0: weather proxies must not override FEMA's applicability assessment.
    /// </summary>
    /// <param name="hazard">Hazard scored.</param>
    /// <param name="factors">Evidence gathered so far (kept for transparency).</param>
    /// <returns>A zero score with a "not applicable" factor.</returns>
    public static HazardScore NotApplicable(HazardType hazard, List<ScoreFactor> factors)
    {
        factors.Add(new ScoreFactor("notApplicable", 0, "flag",
            "FEMA National Risk Index rates this hazard as not applicable to the county"));
        return new HazardScore(hazard, 0, RiskRating.Low, null, 0, factors);
    }

    /// <summary>Builds the final <see cref="HazardScore"/> from optional observed/baseline scores and evidence.</summary>
    /// <param name="hazard">Hazard scored.</param>
    /// <param name="observed">Observed score or null.</param>
    /// <param name="baseline">Baseline score or null.</param>
    /// <param name="observedWeight">Observed share when both exist.</param>
    /// <param name="factors">Evidence.</param>
    /// <returns>The hazard score.</returns>
    public static HazardScore Build(
        HazardType hazard, double? observed, double? baseline, double observedWeight, IReadOnlyList<ScoreFactor> factors)
    {
        double score = Round(Blend(observed, baseline, observedWeight));
        return new HazardScore(
            hazard,
            score,
            RiskRatings.FromScore(score),
            observed is { } o ? Round(o) : null,
            baseline is { } b ? Round(b) : null,
            factors);
    }

    /// <summary>
    /// Scoring recipe for hazards measured by one disruption-day metric and one or more NRI components:
    /// observed = normalize(days/yr, cap); baseline = mean of available NRI components; score = blend.
    /// If the baseline exists but rates every component not applicable, the hazard scores 0.
    /// </summary>
    /// <param name="hazard">Hazard scored.</param>
    /// <param name="settings">Cap and weights.</param>
    /// <param name="weather">Weather summary or null.</param>
    /// <param name="baselineAvailable">Whether a baseline exists for the county (distinguishes "unavailable" from "not applicable").</param>
    /// <param name="dayCount">Selects the disruption-day count from the summary.</param>
    /// <param name="metricName">Evidence name, e.g. <c>heavyRainDaysPerYear</c>.</param>
    /// <param name="metricDescription">Evidence description including the threshold.</param>
    /// <param name="baselineComponents">NRI components (name, value) averaged into the baseline.</param>
    /// <returns>The hazard score.</returns>
    public static HazardScore ScoreSingleMetric(
        HazardType hazard,
        SingleMetricScoringSettings settings,
        WeatherSummary? weather,
        bool baselineAvailable,
        Func<WeatherSummary, int> dayCount,
        string metricName,
        string metricDescription,
        params ReadOnlySpan<(string Name, double? Value)> baselineComponents)
    {
        var factors = new List<ScoreFactor>(1 + baselineComponents.Length);
        double? observed = null;
        if (weather is { DaysObserved: > 0 })
        {
            double perYear = weather.PerYear(dayCount(weather));
            observed = Normalize(perYear, settings.DaysPerYearCap);
            factors.Add(new ScoreFactor(metricName, Round(perYear, 2), "days/yr", metricDescription));
        }

        double sum = 0;
        int count = 0;
        foreach ((string name, double? value) in baselineComponents)
        {
            if (value is not { } v)
            {
                continue;
            }

            sum += v;
            count++;
            factors.Add(NriFactor(name, v));
        }

        if (baselineAvailable && count == 0)
        {
            return NotApplicable(hazard, factors);
        }

        double? baseline = count == 0 ? null : sum / count;
        return Build(hazard, observed, baseline, settings.ObservedWeight, factors);
    }

    /// <summary>Creates evidence for one FEMA NRI component.</summary>
    /// <param name="name">NRI hazard name, e.g. <c>Hurricane</c>.</param>
    /// <param name="value">National frequency percentile 0–100.</param>
    /// <returns>The factor.</returns>
    public static ScoreFactor NriFactor(string name, double value) =>
        new($"nri{name.Replace(" ", string.Empty, StringComparison.Ordinal)}", Round(value, 2), "percentile",
            $"FEMA National Risk Index — {name} annualized frequency, national percentile among US counties (0–100)");
}
