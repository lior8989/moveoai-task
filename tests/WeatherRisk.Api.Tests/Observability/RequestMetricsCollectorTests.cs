using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WeatherRisk.Api.Contracts.System;
using WeatherRisk.Api.Observability;

namespace WeatherRisk.Api.Tests.Observability;

public sealed class RequestMetricsCollectorTests
{
    private const string Health = "GET /api/system/health";
    private const string Hubs = "GET /api/hubs";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly RequestMetricsCollector _collector;

    public RequestMetricsCollectorTests()
    {
        var settings = Options.Create(new MonitoringSettings { WindowSeconds = 60, LatencySampleSize = 10 });
        _collector = new RequestMetricsCollector(settings, _time);
    }

    [Fact]
    public void Record_VariousStatusCodes_ClassifiesIntoOutcomeCounters()
    {
        foreach (int status in new[] { 200, 302, 404, 429, 500 })
        {
            _collector.Record(Hubs, status, TimeSpan.FromMilliseconds(5));
        }

        EndpointMetrics hubs = Single(_collector.GetSnapshot(), Hubs);
        Assert.Equal(5, hubs.Total);
        Assert.Equal(2, hubs.Succeeded);
        Assert.Equal(1, hubs.ClientErrors);
        Assert.Equal(1, hubs.RateLimited);
        Assert.Equal(1, hubs.ServerErrors);
    }

    [Fact]
    public void GetSnapshot_MultipleEndpoints_TracksSeparatelyAndOrdersBusiestFirst()
    {
        _collector.Record(Health, 200, TimeSpan.FromMilliseconds(1));
        _collector.Record(Hubs, 200, TimeSpan.FromMilliseconds(1));
        _collector.Record(Hubs, 200, TimeSpan.FromMilliseconds(1));

        MetricsSnapshot snapshot = _collector.GetSnapshot();

        Assert.Equal([Hubs, Health], snapshot.Endpoints.Select(e => e.Endpoint));
        Assert.Equal(3, snapshot.Overall.Total);
        Assert.Equal(RequestMetricsCollector.OverallEndpoint, snapshot.Overall.Endpoint);
    }

    [Fact]
    public void GetSnapshot_RequestsOlderThanWindow_ExcludedFromWindowButKeptInTotal()
    {
        for (int i = 0; i < 3; i++)
        {
            _collector.Record(Hubs, 200, TimeSpan.FromMilliseconds(1));
        }

        _time.Advance(TimeSpan.FromSeconds(61));
        _collector.Record(Hubs, 200, TimeSpan.FromMilliseconds(1));

        EndpointMetrics hubs = Single(_collector.GetSnapshot(), Hubs);
        Assert.Equal(4, hubs.Total);
        Assert.Equal(1, hubs.RequestsInWindow);
        Assert.Equal(60, hubs.WindowSeconds);
    }

    [Fact]
    public void GetSnapshot_RecordedDurations_ReportsLatencyStatistics()
    {
        foreach (int ms in new[] { 10, 20, 30 })
        {
            _collector.Record(Hubs, 200, TimeSpan.FromMilliseconds(ms));
        }

        EndpointMetrics hubs = Single(_collector.GetSnapshot(), Hubs);
        Assert.Equal(20, hubs.AvgLatencyMs);
        Assert.Equal(30, hubs.P95LatencyMs);
        Assert.Equal(30, hubs.MaxLatencyMs);
        Assert.Equal(3, hubs.LatencySamples);
    }

    [Fact]
    public void GetSnapshot_AfterTimePasses_ReportsUptime()
    {
        _time.Advance(TimeSpan.FromSeconds(90));

        Assert.Equal(90, _collector.GetSnapshot().UptimeSeconds);
    }

    [Fact]
    public void Record_ConcurrentCallers_LosesNoRequests()
    {
        Parallel.For(0, 10_000, i => _collector.Record(i % 2 == 0 ? Hubs : Health, 200, TimeSpan.FromMilliseconds(1)));

        MetricsSnapshot snapshot = _collector.GetSnapshot();
        Assert.Equal(10_000, snapshot.Overall.Total);
        Assert.Equal(5_000, Single(snapshot, Hubs).Total);
        Assert.Equal(5_000, Single(snapshot, Health).Total);
    }

    private static EndpointMetrics Single(MetricsSnapshot snapshot, string endpoint) =>
        Assert.Single(snapshot.Endpoints, e => e.Endpoint == endpoint);
}
