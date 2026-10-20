using System.Diagnostics;

namespace WeatherRisk.Api.Observability;

/// <summary>
/// Outermost handler on each external <see cref="HttpClient"/>: measures every logical call — including all
/// retries done by the resilience pipeline inside it — and records the outcome in <see cref="DependencyMetricsCollector"/>.
/// </summary>
/// <remarks>Lives in the Api so the data layer stays free of monitoring code.</remarks>
public sealed class DependencyMetricsHandler : DelegatingHandler
{
    private readonly string _dependency;
    private readonly DependencyMetricsCollector _collector;

    /// <summary>Creates the handler for one dependency.</summary>
    /// <param name="dependency">Dependency name, e.g. <c>open-meteo</c>.</param>
    /// <param name="collector">Metrics store.</param>
    public DependencyMetricsHandler(string dependency, DependencyMetricsCollector collector)
    {
        _dependency = dependency;
        _collector = collector;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        long start = Stopwatch.GetTimestamp();
        try
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            _collector.RecordCall(
                _dependency,
                response.IsSuccessStatusCode,
                Stopwatch.GetElapsedTime(start),
                response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}");
            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Timeouts, open circuit, DNS/TLS errors. Caller-initiated cancellation is not a dependency failure.
            _collector.RecordCall(_dependency, succeeded: false, Stopwatch.GetElapsedTime(start), ex.GetType().Name);
            throw;
        }
    }
}
