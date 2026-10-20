using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WeatherRisk.Api.Contracts.System;
using WeatherRisk.Api.Observability;

namespace WeatherRisk.Api.Tests.Observability;

public sealed class DependencyMetricsCollectorTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly DependencyMetricsCollector _collector;

    public DependencyMetricsCollectorTests() =>
        _collector = new DependencyMetricsCollector(Options.Create(new MonitoringSettings()), _time);

    [Fact]
    public void RecordCall_CountsOutcomesLatencyAndLastResult()
    {
        _collector.RecordCall("open-meteo", succeeded: true, TimeSpan.FromMilliseconds(100));
        _time.Advance(TimeSpan.FromMinutes(1));
        _collector.RecordCall("open-meteo", succeeded: false, TimeSpan.FromMilliseconds(300), "HTTP 503");

        DependencyMetrics m = Assert.Single(_collector.GetSnapshot());
        Assert.Equal(("open-meteo", 2L, 1L, 1L), (m.Dependency, m.Calls, m.Succeeded, m.Failed));
        Assert.Equal(200, m.AvgLatencyMs);
        Assert.False(m.LastCallSucceeded);
        Assert.Equal("HTTP 503", m.LastError);
        Assert.Equal(_time.GetUtcNow(), m.LastFailureAt);
        Assert.Equal(_time.GetUtcNow().AddMinutes(-1), m.LastSuccessAt);
    }

    [Fact]
    public void RecordFallback_IsCountedPerDependency()
    {
        _collector.RecordFallback("fema-nri");
        _collector.RecordFallback("fema-nri");

        Assert.Equal(2, _collector.Find("fema-nri")?.FallbacksServed);
        Assert.Null(_collector.Find("fema-nri")?.LastCallSucceeded); // A fallback is not a call.
    }

    [Fact]
    public void Find_UnknownDependency_ReturnsNull()
    {
        Assert.Null(_collector.Find("nope"));
    }

    [Fact]
    public void GetSnapshot_OrdersAlphabetically()
    {
        _collector.RecordFallback("open-meteo");
        _collector.RecordFallback("fema-nri");

        Assert.Equal(["fema-nri", "open-meteo"], _collector.GetSnapshot().Select(d => d.Dependency));
    }
}
