namespace WeatherRisk.Api.Observability;

/// <summary>
/// Keeps the most recent N latency samples in a ring buffer and summarizes them (avg / p95 / max).
/// </summary>
/// <remarks>
/// Fixed memory (N doubles). Recording is O(1) and allocation-free; summarizing copies and sorts the
/// samples, which only happens when someone reads the metrics. Not thread-safe — callers synchronize.
/// </remarks>
internal sealed class LatencyWindow
{
    private readonly double[] _samples;
    private int _count;
    private int _next;

    /// <summary>Creates a window holding up to <paramref name="capacity"/> samples.</summary>
    /// <param name="capacity">Maximum number of recent samples kept; must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="capacity"/> is not positive.</exception>
    public LatencyWindow(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _samples = new double[capacity];
    }

    /// <summary>Adds a sample, overwriting the oldest one when full.</summary>
    /// <param name="milliseconds">Request duration in milliseconds.</param>
    public void Add(double milliseconds)
    {
        _samples[_next] = milliseconds;
        _next = (_next + 1) % _samples.Length;
        if (_count < _samples.Length)
        {
            _count++;
        }
    }

    /// <summary>Summarizes the samples currently held.</summary>
    /// <returns>Average, 95th percentile (nearest-rank), max and sample count; zeros when empty.</returns>
    public LatencySummary Summarize()
    {
        if (_count == 0)
        {
            return default;
        }

        var sorted = new double[_count];
        Array.Copy(_samples, sorted, _count);
        Array.Sort(sorted);

        double sum = 0;
        foreach (double sample in sorted)
        {
            sum += sample;
        }

        // Nearest-rank percentile: the smallest sample with at least 95% of samples at or below it.
        int p95Index = (int)Math.Ceiling(0.95 * _count) - 1;
        return new LatencySummary(sum / _count, sorted[p95Index], sorted[^1], _count);
    }
}

/// <summary>Latency statistics over a <see cref="LatencyWindow"/>.</summary>
/// <param name="AverageMs">Mean latency in milliseconds.</param>
/// <param name="P95Ms">95th-percentile latency in milliseconds.</param>
/// <param name="MaxMs">Maximum latency in milliseconds.</param>
/// <param name="Samples">Number of samples the statistics are based on.</param>
internal readonly record struct LatencySummary(double AverageMs, double P95Ms, double MaxMs, int Samples);
