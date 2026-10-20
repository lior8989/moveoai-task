using System.ComponentModel.DataAnnotations;

namespace WeatherRisk.Api.Observability;

/// <summary>Configuration for request monitoring, bound from the <c>Monitoring</c> configuration section.</summary>
public sealed class MonitoringSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Monitoring";

    /// <summary>Length of the "recent traffic" window in seconds (default 60 = requests per minute).</summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; init; } = 60;

    /// <summary>Number of most recent latency samples kept per endpoint for avg/p95/max (default 512).</summary>
    [Range(1, 100_000)]
    public int LatencySampleSize { get; init; } = 512;
}
