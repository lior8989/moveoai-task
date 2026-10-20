using WeatherRisk.Core.Scoring;

namespace WeatherRisk.Core.Abstractions;

/// <summary>External source of long-term hazard baselines per county (e.g. FEMA National Risk Index).</summary>
public interface IHazardBaselineSource
{
    /// <summary>Dependency name used in metrics, health checks and logs, e.g. <c>fema-nri</c>.</summary>
    string Name { get; }

    /// <summary>Fetches baselines for the requested counties in one logical operation.</summary>
    /// <param name="countyFips">5-digit county FIPS codes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Baselines keyed by FIPS; counties unknown to the source are omitted.</returns>
    /// <exception cref="HttpRequestException">When the source is unreachable or returns an error.</exception>
    /// <exception cref="InvalidDataException">When the response is malformed.</exception>
    Task<IReadOnlyDictionary<string, HazardBaseline>> FetchAsync(
        IReadOnlyCollection<string> countyFips, CancellationToken cancellationToken = default);
}
