using WeatherRisk.Core.Scoring;

namespace WeatherRisk.Data.External.Fema;

/// <summary>
/// Converts raw NRI annualized frequencies into national percentiles (0–100) per hazard, so every hazard
/// is on the same scale regardless of its unit (events/yr vs event-days/yr).
/// </summary>
/// <remarks>
/// Percentile = mid-rank: <c>100 × (countBelow + 0.5 × countEqual) / countNonNull</c>. Mid-rank treats ties
/// fairly (e.g. the many counties with zero hurricane frequency all get the same, low percentile).
/// Each hazard column is sorted once (O(n log n)); each lookup is two binary searches.
/// </remarks>
internal static class NriPercentileCalculator
{
    private const int HazardCount = 10;

    /// <summary>Computes baselines for the requested counties against the national distribution.</summary>
    /// <param name="allCounties">Frequencies for every US county (FIPS, ten values in baseline order).</param>
    /// <param name="requestedFips">Counties to return.</param>
    /// <returns>Baselines keyed by FIPS; requested counties absent from <paramref name="allCounties"/> are omitted.</returns>
    public static Dictionary<string, HazardBaseline> Compute(
        IReadOnlyList<(string Fips, double?[] Values)> allCounties, IReadOnlyCollection<string> requestedFips)
    {
        double[][] sortedColumns = new double[HazardCount][];
        for (int hazard = 0; hazard < HazardCount; hazard++)
        {
            var column = new List<double>(allCounties.Count);
            foreach ((_, double?[] values) in allCounties)
            {
                if (values[hazard] is { } v)
                {
                    column.Add(v);
                }
            }

            sortedColumns[hazard] = [.. column];
            Array.Sort(sortedColumns[hazard]);
        }

        var wanted = new HashSet<string>(requestedFips, StringComparer.Ordinal);
        var result = new Dictionary<string, HazardBaseline>(wanted.Count, StringComparer.Ordinal);
        foreach ((string fips, double?[] values) in allCounties)
        {
            if (!wanted.Contains(fips))
            {
                continue;
            }

            var p = new double?[HazardCount];
            for (int hazard = 0; hazard < HazardCount; hazard++)
            {
                // Null = hazard not applicable to the county; keep it null rather than ranking it as zero.
                p[hazard] = values[hazard] is { } v ? Percentile(sortedColumns[hazard], v) : null;
            }

            result[fips] = new HazardBaseline(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9]);
        }

        return result;
    }

    /// <summary>Mid-rank percentile of <paramref name="value"/> within an ascending array.</summary>
    /// <param name="sorted">Ascending values (non-empty when called with a value taken from it).</param>
    /// <param name="value">Value to rank.</param>
    /// <returns>Percentile 0–100, rounded to 2 decimals.</returns>
    internal static double Percentile(double[] sorted, double value)
    {
        if (sorted.Length == 0)
        {
            return 0;
        }

        int below = LowerBound(sorted, value);

        // LowerBound of the next representable double = index of the first element strictly greater than value.
        int equal = LowerBound(sorted, Math.BitIncrement(value)) - below;
        return Math.Round(100.0 * (below + (0.5 * equal)) / sorted.Length, 2);
    }

    /// <summary>Index of the first element ≥ <paramref name="value"/>.</summary>
    private static int LowerBound(double[] sorted, double value)
    {
        int lo = 0, hi = sorted.Length;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (sorted[mid] < value) lo = mid + 1;
            else hi = mid;
        }

        return lo;
    }
}
