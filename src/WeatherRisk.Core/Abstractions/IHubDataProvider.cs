using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Abstractions;

/// <summary>
/// Cache-first access to the data the risk model needs. Serves valid cached data, refreshes stale data from
/// the external sources, and falls back to older cached data when a source fails — every result says which.
/// </summary>
public interface IHubDataProvider
{
    /// <summary>The observed-weather look-back window currently in use (last N full calendar years).</summary>
    DataWindow Window { get; }

    /// <summary>Returns daily weather for the current <see cref="Window"/> for each hub.</summary>
    /// <param name="hubs">Hubs to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One entry per hub, keyed by hub id.</returns>
    Task<IReadOnlyDictionary<string, HubWeatherData>> GetWeatherAsync(
        IReadOnlyCollection<Hub> hubs, CancellationToken cancellationToken = default);

    /// <summary>Returns the hazard baseline for each hub's county.</summary>
    /// <param name="hubs">Hubs to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One entry per distinct county, keyed by county FIPS.</returns>
    Task<IReadOnlyDictionary<string, HubBaselineData>> GetBaselinesAsync(
        IReadOnlyCollection<Hub> hubs, CancellationToken cancellationToken = default);
}

/// <summary>An inclusive date range.</summary>
/// <param name="From">First day (inclusive).</param>
/// <param name="To">Last day (inclusive).</param>
public readonly record struct DataWindow(DateOnly From, DateOnly To);

/// <summary>Where a piece of data came from — surfaced to users as part of communicating uncertainty.</summary>
public enum DataFreshness
{
    /// <summary>Fetched from the external source during this request.</summary>
    Fresh,

    /// <summary>Served from a cache entry that is still within its time-to-live.</summary>
    Cached,

    /// <summary>The source failed; an expired (or partial) cache entry was served instead.</summary>
    Stale,

    /// <summary>The source failed and nothing was cached.</summary>
    Unavailable,
}

/// <summary>Daily weather for one hub plus its provenance.</summary>
/// <param name="HubId">Hub id.</param>
/// <param name="Window">Requested window.</param>
/// <param name="Days">Rows within the window (may be fewer than the days in it, or empty when unavailable).</param>
/// <param name="Freshness">Provenance of <paramref name="Days"/>.</param>
/// <param name="RetrievedAt">When the data was fetched from the source; null when unavailable.</param>
public sealed record HubWeatherData(
    string HubId, DataWindow Window, IReadOnlyList<DailyWeather> Days, DataFreshness Freshness, DateTimeOffset? RetrievedAt);

/// <summary>Hazard baseline for one county plus its provenance.</summary>
/// <param name="CountyFips">County FIPS code.</param>
/// <param name="Baseline">Baseline, or null when unavailable.</param>
/// <param name="Freshness">Provenance of <paramref name="Baseline"/>.</param>
/// <param name="RetrievedAt">When the data was fetched from the source; null when unavailable.</param>
public sealed record HubBaselineData(
    string CountyFips, HazardBaseline? Baseline, DataFreshness Freshness, DateTimeOffset? RetrievedAt);
