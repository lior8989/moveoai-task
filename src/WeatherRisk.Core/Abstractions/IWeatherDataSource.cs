using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Abstractions;

/// <summary>External source of historical daily weather (e.g. Open-Meteo). Always live — caching is the provider's job.</summary>
public interface IWeatherDataSource
{
    /// <summary>Dependency name used in metrics, health checks and logs, e.g. <c>open-meteo</c>.</summary>
    string Name { get; }

    /// <summary>Fetches daily weather at the hub's coordinates.</summary>
    /// <param name="hub">Hub whose coordinates are queried.</param>
    /// <param name="from">First day (inclusive).</param>
    /// <param name="to">Last day (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One row per day returned by the source.</returns>
    /// <exception cref="HttpRequestException">When the source is unreachable or returns an error.</exception>
    /// <exception cref="InvalidDataException">When the response is malformed.</exception>
    Task<IReadOnlyList<DailyWeather>> FetchDailyAsync(
        Hub hub, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
