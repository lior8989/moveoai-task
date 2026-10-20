namespace WeatherRisk.Core.Abstractions;

/// <summary>
/// Receives resilience events from the data layer (which must not depend on the API's monitoring code).
/// Call outcomes and latency are recorded separately by an HTTP handler; this covers what only the data
/// layer knows: that a fallback was used.
/// </summary>
public interface IDependencyMetrics
{
    /// <summary>Records that a dependency failed and cached (or no) data was served instead.</summary>
    /// <param name="dependency">Dependency name, e.g. <c>open-meteo</c>.</param>
    void RecordFallback(string dependency);
}
