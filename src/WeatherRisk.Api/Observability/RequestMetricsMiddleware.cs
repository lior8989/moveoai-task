using System.Diagnostics;

namespace WeatherRisk.Api.Observability;

/// <summary>
/// Measures every <c>/api</c> request (status + duration) and records it in <see cref="RequestMetricsCollector"/>.
/// </summary>
/// <remarks>
/// Placed after routing (to know the matched route template) and before rate limiting
/// (so 429 rejections are counted). Allocation-light: <see cref="Stopwatch.GetTimestamp"/> timing.
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

        long start = Stopwatch.GetTimestamp();
        bool failed = false;
        try
        {
            await _next(context);
        }
        catch
        {
            // The exception handler upstream turns this into a 500; record it as such.
            failed = true;
            throw;
        }
        finally
        {
            int status = failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
            _collector.Record(ResolveEndpointLabel(context), status, Stopwatch.GetElapsedTime(start));
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
