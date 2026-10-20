using System.Diagnostics;

namespace WeatherRisk.Api.Observability;

/// <summary>
/// Measures every <c>/api</c> request (status + duration) and records it in <see cref="RequestMetricsCollector"/>.
/// </summary>
/// <remarks>
/// Placed after routing (to know the matched route template) and outside the exception handler and rate
/// limiter, so it records the final status of every request — including handled exceptions (404/400/500)
/// and 429 rejections. Allocation-light: <see cref="Stopwatch.GetTimestamp"/> timing.
/// </remarks>
public sealed class RequestMetricsMiddleware
{
    private static readonly PathString ApiPrefix = "/api";

    private readonly RequestDelegate _next;
    private readonly RequestMetricsCollector _collector;

    /// <summary>Creates the middleware.</summary>
    /// <param name="next">Next middleware in the pipeline.</param>
    /// <param name="collector">Metrics store.</param>
    public RequestMetricsMiddleware(RequestDelegate next, RequestMetricsCollector collector)
    {
        _next = next;
        _collector = collector;
    }

    /// <summary>Times the request and records its outcome; non-API paths pass through untouched.</summary>
    /// <param name="context">Current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(ApiPrefix))
        {
            await _next(context);
            return;
        }

        // Resolve the label first: the exception handler (inside this middleware) clears the endpoint when it
        // handles an exception, so reading it afterwards would mislabel failed requests as "(unmatched)".
        string label = ResolveEndpointLabel(context);
        long start = Stopwatch.GetTimestamp();
        bool failed = false;
        try
        {
            await _next(context);
        }
        catch
        {
            // Only reached if the exception escaped the exception handler (e.g. response already started).
            failed = true;
            throw;
        }
        finally
        {
            int status = failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
            _collector.Record(label, status, Stopwatch.GetElapsedTime(start));
        }
    }

    /// <summary>
    /// Labels a request by HTTP method + route template (e.g. <c>GET /api/hubs/{id}</c>), never by raw path,
    /// so the number of metric keys stays bounded.
    /// </summary>
    private static string ResolveEndpointLabel(HttpContext context) =>
        context.GetEndpoint() is RouteEndpoint { RoutePattern.RawText: { } template }
            ? $"{context.Request.Method} /{template.TrimStart('/')}"
            : RequestMetricsCollector.UnmatchedEndpoint;
}
