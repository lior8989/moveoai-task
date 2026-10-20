using System.Net;
using Microsoft.Extensions.Options;
using WeatherRisk.Api.Contracts.System;
using WeatherRisk.Api.Observability;

namespace WeatherRisk.Api.Tests.Observability;

public sealed class DependencyMetricsHandlerTests
{
    private readonly DependencyMetricsCollector _collector =
        new(Options.Create(new MonitoringSettings()), TimeProvider.System);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private HttpClient ClientReturning(Func<HttpResponseMessage> respond) =>
        new(new DependencyMetricsHandler("dep", _collector) { InnerHandler = new LambdaHandler(respond) })
        {
            BaseAddress = new Uri("https://example.test/"),
        };

    [Fact]
    public async Task SuccessStatus_RecordsSuccess()
    {
        await ClientReturning(() => new HttpResponseMessage(HttpStatusCode.OK)).GetAsync("x", Ct);

        DependencyMetrics? m = _collector.Find("dep");
        Assert.Equal((1L, 1L, 0L, true), (m?.Calls, m?.Succeeded, m?.Failed, m?.LastCallSucceeded));
    }

    [Fact]
    public async Task ErrorStatus_RecordsFailureWithStatusCode()
    {
        await ClientReturning(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)).GetAsync("x", Ct);

        DependencyMetrics? m = _collector.Find("dep");
        Assert.Equal(1, m?.Failed);
        Assert.Equal("HTTP 503", m?.LastError);
    }

    [Fact]
    public async Task Exception_RecordsFailureAndRethrows()
    {
        HttpClient client = ClientReturning(() => throw new HttpRequestException("dns"));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("x", Ct));

        Assert.Equal("HttpRequestException", _collector.Find("dep")?.LastError);
    }

    [Fact]
    public async Task CallerCancellation_IsNotRecordedAsDependencyFailure()
    {
        using var cts = new CancellationTokenSource();
        HttpClient client = ClientReturning(() =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("x", cts.Token));

        Assert.Null(_collector.Find("dep"));
    }

    private sealed class LambdaHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond());
    }
}
