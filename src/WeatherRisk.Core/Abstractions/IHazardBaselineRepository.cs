using WeatherRisk.Core.Scoring;

namespace WeatherRisk.Core.Abstractions;

/// <summary>Local store of FEMA NRI baselines per county — a cache of the external source.</summary>
public interface IHazardBaselineRepository
{
    /// <summary>Returns the stored baseline for a county, or null when not cached.</summary>
    /// <param name="countyFips">5-digit county FIPS code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The baseline with its fetch time, or null.</returns>
    Task<StoredBaseline?> GetAsync(string countyFips, CancellationToken cancellationToken = default);

    /// <summary>Inserts or replaces the baseline for a county.</summary>
    /// <param name="countyFips">5-digit county FIPS code.</param>
    /// <param name="baseline">NRI hazard scores.</param>
    /// <param name="fetchedAt">When the data was fetched from the source.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(
        string countyFips, HazardBaseline baseline, DateTimeOffset fetchedAt, CancellationToken cancellationToken = default);
}

/// <summary>A cached baseline and when it was fetched.</summary>
/// <param name="CountyFips">5-digit county FIPS code.</param>
/// <param name="Baseline">NRI hazard scores.</param>
/// <param name="FetchedAt">When the data was fetched from the source.</param>
public sealed record StoredBaseline(string CountyFips, HazardBaseline Baseline, DateTimeOffset FetchedAt);
