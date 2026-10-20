using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using WeatherRisk.Api.Observability;

namespace WeatherRisk.Api.Tests.Observability;

public sealed class DependencyHealthCheckTests
{
    private readonly DependencyMetricsCollector _collector = new(Options.Create(new MonitoringSettings()), TimeProvider.System);

    private Task<HealthCheckResult> CheckAsync() =>
        new DependencyHealthCheck(_collector, "open-meteo").CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task NeverCalled_IsHealthy()
    {
        Assert.Equal(HealthStatus.Healthy, (await CheckAsync()).Status);
    }

    [Fact]
    public async Task LastCallSucceeded_IsHealthy()
    {
        _collector.RecordCall("open-meteo", succeeded: false, TimeSpan.Zero, "HTTP 500");
        _collector.RecordCall("open-meteo", succeeded: true, TimeSpan.Zero);

        Assert.Equal(HealthStatus.Healthy, (await CheckAsync()).Status);
    }

    [Fact]
    public async Task LastCallFailed_IsDegradedWithReason()
    {
        _collector.RecordCall("open-meteo", succeeded: false, TimeSpan.Zero, "TimeoutRejectedException");

        HealthCheckResult result = await CheckAsync();

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("TimeoutRejectedException", result.Description);
    }
}
