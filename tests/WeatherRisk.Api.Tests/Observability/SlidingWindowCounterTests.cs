using WeatherRisk.Api.Observability;

namespace WeatherRisk.Api.Tests.Observability;

public sealed class SlidingWindowCounterTests
{
    [Fact]
    public void Sum_EventsWithinWindow_CountsAll()
    {
        var counter = new SlidingWindowCounter(windowSeconds: 60);

        counter.Increment(1_000);
        counter.Increment(1_000);
        counter.Increment(1_030);

        Assert.Equal(3, counter.Sum(1_059));
    }

    [Fact]
    public void Sum_EventsOlderThanWindow_AreExcluded()
    {
        var counter = new SlidingWindowCounter(windowSeconds: 5);

        counter.Increment(100);
        counter.Increment(101);

        // Window ending at 105 covers (100, 105] — the event at 100 has just left it.
        Assert.Equal(1, counter.Sum(105));
    }

    [Fact]
    public void Increment_SameBucketAfterWrapAround_ResetsOldCount()
    {
        var counter = new SlidingWindowCounter(windowSeconds: 5);

        counter.Increment(100);
        counter.Increment(100);
        counter.Increment(105); // Same bucket index as 100 — must recycle, not accumulate.

        Assert.Equal(1, counter.Sum(105));
    }

    [Fact]
    public void Sum_NoEvents_ReturnsZero()
    {
        var counter = new SlidingWindowCounter(windowSeconds: 10);

        Assert.Equal(0, counter.Sum(1_000));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveWindow_Throws(int windowSeconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlidingWindowCounter(windowSeconds));
    }
}
