using System.Globalization;
using System.Net.Http.Json;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Data.External.OpenMeteo;

/// <summary>
/// Typed HTTP client for the Open-Meteo Historical Weather API (free, no key):
/// https://open-meteo.com/en/docs/historical-weather-api. Resilience (timeout/retry/circuit breaker) is added
/// by the HTTP pipeline at registration, so this class only maps request and response.
/// </summary>
internal sealed class OpenMeteoClient : IWeatherDataSource
{
    /// <summary>Dependency name for metrics/health/logs.</summary>
    public const string DependencyName = DependencyNames.OpenMeteo;

    /// <summary>Base address of the archive API.</summary>
    public static readonly Uri BaseAddress = new("https://archive-api.open-meteo.com/");

    // Units are Open-Meteo defaults (cm, mm, km/h, °C) — exactly what DailyWeather expects.
    private const string DailyVariables =
        "snowfall_sum,precipitation_sum,wind_gusts_10m_max,temperature_2m_max,temperature_2m_min";

    private readonly HttpClient _http;

    /// <summary>Creates the client.</summary>
    /// <param name="http">HttpClient configured by <c>IHttpClientFactory</c> (base address + resilience).</param>
    public OpenMeteoClient(HttpClient http) => _http = http;

    /// <inheritdoc />
    public string Name => DependencyName;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DailyWeather>> FetchDailyAsync(
        Hub hub, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hub);

        // Invariant culture: a comma decimal separator would silently query the wrong location.
        string url = string.Create(CultureInfo.InvariantCulture,
            $"v1/archive?latitude={hub.Latitude}&longitude={hub.Longitude}" +
            $"&start_date={from:yyyy-MM-dd}&end_date={to:yyyy-MM-dd}&daily={DailyVariables}&timezone=auto");

        using HttpResponseMessage response = await _http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        OpenMeteoResponse? body = await response.Content.ReadFromJsonAsync(
            OpenMeteoJsonContext.Default.OpenMeteoResponse, cancellationToken);
        return Map(body);
    }

    /// <summary>Converts the column-oriented arrays into rows, validating that all arrays line up.</summary>
    /// <exception cref="InvalidDataException">When the daily block is missing or arrays differ in length.</exception>
    private static List<DailyWeather> Map(OpenMeteoResponse? body)
    {
        OpenMeteoDaily daily = body?.Daily ?? throw new InvalidDataException("Open-Meteo response has no 'daily' block.");
        string[] time = daily.Time ?? throw new InvalidDataException("Open-Meteo response has no 'daily.time'.");

        double?[] snow = Require(daily.SnowfallSum, time.Length, "snowfall_sum");
        double?[] precip = Require(daily.PrecipitationSum, time.Length, "precipitation_sum");
        double?[] gust = Require(daily.WindGustsMax, time.Length, "wind_gusts_10m_max");
        double?[] high = Require(daily.TemperatureMax, time.Length, "temperature_2m_max");
        double?[] low = Require(daily.TemperatureMin, time.Length, "temperature_2m_min");

        var rows = new List<DailyWeather>(time.Length);
        for (int i = 0; i < time.Length; i++)
        {
            if (!DateOnly.TryParseExact(time[i], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
            {
                throw new InvalidDataException($"Open-Meteo returned an invalid date '{time[i]}'.");
            }

            rows.Add(new DailyWeather(date, snow[i], precip[i], gust[i], high[i], low[i]));
        }

        return rows;
    }

    private static double?[] Require(double?[]? values, int expectedLength, string name) =>
        values is { } v && v.Length == expectedLength
            ? v
            : throw new InvalidDataException($"Open-Meteo series '{name}' is missing or misaligned with 'time'.");
}
