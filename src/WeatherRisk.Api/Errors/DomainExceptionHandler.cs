using Microsoft.AspNetCore.Diagnostics;
using WeatherRisk.Core.Application;

namespace WeatherRisk.Api.Errors;

/// <summary>
/// Translates domain exceptions into RFC 7807 responses: <see cref="HubNotFoundException"/> → 404,
/// <see cref="RequestValidationException"/> → 400. Anything else falls through to the default 500 handler
/// (Chain of Responsibility via <see cref="IExceptionHandler"/>).
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problems;

    /// <summary>Creates the handler.</summary>
    /// <param name="problems">ProblemDetails writer.</param>
    public DomainExceptionHandler(IProblemDetailsService problems) => _problems = problems;

    /// <summary>Whether an exception is an expected domain outcome (not a server fault).</summary>
    /// <param name="exception">Exception.</param>
    /// <returns>True for exceptions this handler translates.</returns>
    public static bool IsDomainException(Exception exception) =>
        exception is HubNotFoundException or RequestValidationException;

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int status, string title, IDictionary<string, object?> extensions) = exception switch
        {
            HubNotFoundException notFound => (StatusCodes.Status404NotFound, "Hub not found",
                new Dictionary<string, object?> { ["unknownHubs"] = notFound.HubIds }),
            RequestValidationException => (StatusCodes.Status400BadRequest, "Invalid request",
                new Dictionary<string, object?>()),
            _ => (0, string.Empty, (IDictionary<string, object?>)new Dictionary<string, object?>()),
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        return await _problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = status, Title = title, Detail = exception.Message, Extensions = extensions },
        });
    }
}
