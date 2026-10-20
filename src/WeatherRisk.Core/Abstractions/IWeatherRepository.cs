using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Abstractions;

/// <summary>Local store of daily weather per hub — a cache of the external historical-weather source.</summary>
public interface IWeatherRepository
{
    /// <summary>Returns stored daily rows for a hub within a period, ordered by date.</summary>
    /// <param name="hubId">Hub id.</param>
    /// <param name="from">First day (inclusive).</param>
    /// <param name="to">Last day (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stored rows (possibly fewer than the days in the period).</returns>
    Task<IReadOnlyList<DailyWeather>> GetDailyAsync(
        string hubId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically replaces a hub's rows within a period and records the sync state —
    /// so a failed refresh never leaves a half-written period behind.
    /// </summary>
    /// <param name="hubId">Hub id.</param>
    /// <param name="from">First day of the replaced period (inclusive).</param>
    /// <param name="to">Last day of the replaced period (inclusive).</param>
    /// <param name="days">New rows; rows outside the period are ignored.</param>
    /// <param name="syncedAt">When the data was fetched from the source.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReplaceDailyAsync(
        string hubId, DateOnly from, DateOnly to, IReadOnlyCollection<DailyWeather> days, DateTimeOffset syncedAt,
        CancellationToken cancellationToken = default);

    /// <summary>Returns which period is cached for a hub and when it was fetched; null when nothing is cached.</summary>
    /// <param name="hubId">Hub id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sync state or null.</returns>
    Task<WeatherSyncState?> GetSyncStateAsync(string hubId, CancellationToken cancellationToken = default);
}

/// <summary>What weather period is cached for a hub and when it was fetched.</summary>
/// <param name="HubId">Hub id.</param>
/// <param name="CoveredFrom">First cached day (inclusive).</param>
/// <param name="CoveredTo">Last cached day (inclusive).</param>
/// <param name="SyncedAt">When the data was fetched from the source.</param>
public sealed record WeatherSyncState(string HubId, DateOnly CoveredFrom, DateOnly CoveredTo, DateTimeOffset SyncedAt);
