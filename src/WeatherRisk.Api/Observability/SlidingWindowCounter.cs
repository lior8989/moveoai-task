namespace WeatherRisk.Api.Observability;

/// <summary>
/// Counts events over a sliding time window made of one-second buckets
/// (e.g. "requests in the last 60 seconds").
/// </summary>
/// <remarks>
/// Fixed memory: one bucket per second of the window, reused circularly. O(1) increment, O(window) sum.
/// Not thread-safe — callers synchronize (see <see cref="EndpointStats"/>).
/// </remarks>
internal sealed class SlidingWindowCounter
{
    private readonly long[] _counts;

    // Unix second each bucket currently represents; a bucket is stale once its second leaves the window.
    private readonly long[] _bucketSeconds;

    /// <summary>Creates a counter covering the given number of seconds.</summary>
    /// <param name="windowSeconds">Window length in seconds; must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="windowSeconds"/> is not positive.</exception>
    public SlidingWindowCounter(int windowSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSeconds);
        _counts = new long[windowSeconds];
        _bucketSeconds = new long[windowSeconds];
        // MinValue marks "never used" so empty buckets are never counted as inside the window.
        Array.Fill(_bucketSeconds, long.MinValue);
    }

    /// <summary>Window length in seconds.</summary>
    public int WindowSeconds => _counts.Length;

    /// <summary>Records one event at the given time.</summary>
    /// <param name="unixSecond">Event time as Unix seconds.</param>
    public void Increment(long unixSecond)
    {
        int index = IndexOf(unixSecond);
        if (_bucketSeconds[index] != unixSecond)
        {
            // Bucket held an older second — recycle it.
            _bucketSeconds[index] = unixSecond;
            _counts[index] = 0;
        }

        _counts[index]++;
    }

    /// <summary>Returns the number of events inside the window ending at <paramref name="unixSecond"/>.</summary>
    /// <param name="unixSecond">End of the window (inclusive) as Unix seconds — usually "now".</param>
    /// <returns>Event count within <c>(unixSecond - WindowSeconds, unixSecond]</c>.</returns>
    public long Sum(long unixSecond)
    {
        long oldestIncluded = unixSecond - _counts.Length;
        long sum = 0;
        for (int i = 0; i < _counts.Length; i++)
        {
            long second = _bucketSeconds[i];
            if (second > oldestIncluded && second <= unixSecond)
            {
                sum += _counts[i];
            }
        }

        return sum;
    }

    private int IndexOf(long unixSecond) => (int)(((unixSecond % _counts.Length) + _counts.Length) % _counts.Length);
}
