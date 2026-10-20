namespace WeatherRisk.Core.Application;

/// <summary>One or more requested hubs do not exist. Mapped to HTTP 404 by the API.</summary>
public sealed class HubNotFoundException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="hubIds">Ids that were not found.</param>
    public HubNotFoundException(IReadOnlyList<string> hubIds)
        : base($"Unknown hub(s): {string.Join(", ", hubIds)}.") => HubIds = hubIds;

    /// <summary>Ids that were not found.</summary>
    public IReadOnlyList<string> HubIds { get; }
}

/// <summary>A request is well-formed but invalid for the domain (e.g. period outside the data window). Mapped to HTTP 400.</summary>
public sealed class RequestValidationException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">Human-readable explanation of what is wrong and what is allowed.</param>
    public RequestValidationException(string message)
        : base(message)
    {
    }
}
