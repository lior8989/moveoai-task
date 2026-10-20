using Microsoft.AspNetCore.Diagnostics;
using WeatherRisk.Core.Agent;
using WeatherRisk.Core.Agent.Llm;
using WeatherRisk.Core.Application;

namespace WeatherRisk.Api.Errors;

/// <summary>
/// Translates expected exceptions into RFC 7807 responses: <see cref="HubNotFoundException"/> → 404,
/// <see cref="RequestValidationException"/> → 400, <see cref="AgentFailedException"/> → 502,
/// <see cref="LlmUnavailableException"/> → 503, <see cref="BadHttpRequestException"/> → its status (400).
/// Anything else falls through to the default 500 handler
/// (Chain of Responsibility via <see cref="IExceptionHandler"/>).
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problems;

    /// <summary>Creates the handler.</summary>
    /// <param name="problems">ProblemDetails writer.</param>
    public DomainExceptionHandler(IProblemDetailsService problems) => _problems = problems;

    /// <summary>Whether an exception is an expected outcome (not a server fault) — such exceptions aren't logged as errors.</summary>
    /// <param name="exception">Exception.</param>
    /// <returns>True for exceptions this handler translates.</returns>
    public static bool IsDomainException(Exception exception) =>
        exception is HubNotFoundException or RequestValidationException or AgentFailedException or LlmUnavailableException
            or BadHttpRequestException;

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int status, string title) = exception switch
        {
            HubNotFoundException => (StatusCodes.Status404NotFound, "Hub not found"),
            RequestValidationException => (StatusCodes.Status400BadRequest, "Invalid request"),
            AgentFailedException => (StatusCodes.Status502BadGateway, "The assistant could not produce a valid answer"),
            LlmUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Assistant unavailable"),

            // Malformed request body/parameters. Minimal APIs throw this in Development (ThrowOnBadRequest);
            // without this mapping the client would get a 500 for its own mistake.
            BadHttpRequestException bad => (bad.StatusCode, "Bad request"),
            _ => (0, string.Empty),
        };

        if (status == 0)
        {
            return false;
        }

        var extensions = new Dictionary<string, object?>();
        if (exception is HubNotFoundException notFound)
        {
            extensions["unknownHubs"] = notFound.HubIds;
        }

        if (exception is LlmUnavailableException or AgentFailedException)
        {
            // Point the analyst to what still works without the LLM.
            extensions["hint"] = "Deterministic endpoints remain available: /api/risk/rankings, /api/risk/compare, /api/hubs/{id}/risk.";
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
