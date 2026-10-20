using WeatherRisk.Api.Observability;

namespace WeatherRisk.Api.Tests.Observability;

public sealed class LatencyWindowTests
{
    [Fact]
    public void Summarize_Empty_ReturnsZeros()
    {
        var window = new LatencyWindow(capacity: 10);

        Assert.Equal(default, window.Summarize());
    }

    [Fact]
    public void Summarize_OneToHundred_ReturnsExactStatistics()
    {
        var window = new LatencyWindow(capacity: 100);
        for (int ms = 100; ms >= 1; ms--) // Inserted out of order on purpose.
        {
            window.Add(ms);
        }

        LatencySummary summary = window.Summarize();

        Assert.Equal(50.5, summary.AverageMs);
        Assert.Equal(95, summary.P95Ms);
        Assert.Equal(100, summary.MaxMs);
        Assert.Equal(100, summary.Samples);
    }

    [Fact]
    public void Add_WhenFull_OverwritesOldestSample()
    {
        var window = new LatencyWindow(capacity: 3);

        window.Add(1_000); // Oldest — should be evicted.
        window.Add(1);
        window.Add(2);
        window.Add(3);

        LatencySummary summary = window.Summarize();
        Assert.Equal(3, summary.Samples);
        Assert.Equal(2, summary.AverageMs);
        Assert.Equal(3, summary.MaxMs);
    }

    [Fact]
    public void Summarize_SingleSample_AllStatisticsEqualIt()
    {
        var window = new LatencyWindow(capacity: 5);
        window.Add(42);

        Assert.Equal(new LatencySummary(42, 42, 42, 1), window.Summarize());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_NonPositiveCapacity_Throws(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LatencyWindow(capacity));
    }
}
