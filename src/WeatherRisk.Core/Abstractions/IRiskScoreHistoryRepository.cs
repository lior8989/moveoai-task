using WeatherRisk.Core.Scoring;

namespace WeatherRisk.Core.Abstractions;

/// <summary>Append-only history of computed hub risk scores — the basis for trend views and change alerts.</summary>
public interface IRiskScoreHistoryRepository
{
    /// <summary>Appends snapshots in one batch.</summary>
    /// <param name="snapshots">Snapshots to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(IReadOnlyCollection<RiskScoreSnapshot> snapshots, CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent snapshot for a hub, or null when none exists.</summary>
    /// <param name="hubId">Hub id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest snapshot or null.</returns>
    Task<RiskScoreSnapshot?> GetLatestAsync(string hubId, CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent snapshots for a hub, newest first.</summary>
    /// <param name="hubId">Hub id.</param>
    /// <param name="limit">Maximum number of snapshots (must be positive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Up to <paramref name="limit"/> snapshots, newest first.</returns>
    Task<IReadOnlyList<RiskScoreSnapshot>> GetHistoryAsync(
        string hubId, int limit, CancellationToken cancellationToken = default);
}
