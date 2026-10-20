using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace WeatherRisk.Api.RateLimiting;

/// <summary>Registers the API rate limiter (built-in ASP.NET Core middleware) from configuration.</summary>
public static class RateLimitingExtensions
{
    // Operational endpoints stay reachable under load — that is exactly when metrics/health are needed.
    private static readonly PathString ApiPrefix = "/api";
    private static readonly PathString ExemptPrefix = "/api/system";

    /// <summary>
    /// Adds a global per-client sliding-window limiter for <c>/api</c> traffic. Rejections return
    /// <c>429</c> with a <c>Retry-After</c> header and an RFC 7807 problem body.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">App configuration containing the <c>RateLimiting</c> section.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingSettings>()
            .Bind(configuration.GetSection(RateLimitingSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter();

        // Configured through options (not inline) so settings come from DI and tests can override them.
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingSettings>>((options, settings) =>
            {
                SlidingWindowSettings global = settings.Value.Global;
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    context => CreatePartition(context, global));
                options.OnRejected = (context, cancellationToken) =>
                    WriteRejectionAsync(context, global.SegmentDuration, cancellationToken);
            });

        return services;
    }

    /// <summary>One sliding-window bucket per client IP for limited paths; no limiter for everything else.</summary>
    private static RateLimitPartition<string> CreatePartition(HttpContext context, SlidingWindowSettings settings)
    {
        PathString path = context.Request.Path;
        if (!path.StartsWithSegments(ApiPrefix) || path.StartsWithSegments(ExemptPrefix))
        {
            return RateLimitPartition.GetNoLimiter("unlimited");
        }

        // Assumption: clients connect directly. Behind a proxy, enable forwarded headers first.
        string clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(clientKey, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = settings.PermitLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            SegmentsPerWindow = settings.SegmentsPerWindow,
            QueueLimit = 0, // Reject immediately rather than holding connections open.
        });
    }

    /// <summary>Writes the 429 response: <c>Retry-After</c> header + <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> body.</summary>
    private static async ValueTask WriteRejectionAsync(
        OnRejectedContext context, TimeSpan fallbackRetryAfter, CancellationToken cancellationToken)
    {
        HttpContext http = context.HttpContext;
        TimeSpan retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan fromLease)
            ? fromLease
            : fallbackRetryAfter;
        int retrySeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        http.Response.Headers.RetryAfter = retrySeconds.ToString(CultureInfo.InvariantCulture);

        var problems = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests",
                Detail = $"Rate limit exceeded. Retry after {retrySeconds} second(s).",
            },
        });
    }
}
