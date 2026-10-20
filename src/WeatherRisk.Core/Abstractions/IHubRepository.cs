using WeatherRisk.Core.Domain;

namespace WeatherRisk.Core.Abstractions;

/// <summary>Read access to the hub catalog.</summary>
public interface IHubRepository
{
    /// <summary>Returns all hubs, ordered by name.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All hubs.</returns>
    Task<IReadOnlyList<Hub>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns one hub by id (case-insensitive), or null when it doesn't exist.</summary>
    /// <param name="id">Hub id, e.g. <c>dallas</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hub or null.</returns>
    Task<Hub?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
}
