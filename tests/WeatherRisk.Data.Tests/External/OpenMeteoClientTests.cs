using System.Globalization;
using System.Net;
using WeatherRisk.Core.Weather;
using WeatherRisk.Data.External.OpenMeteo;
using WeatherRisk.Data.Persistence.Seed;

namespace WeatherRisk.Data.Tests.External;

public sealed class OpenMeteoClientTests
{
    private const string TwoDays = """
        {"latitude":39.75,"longitude":-105.02,"daily_units":{"time":"iso8601","snowfall_sum":"cm"},
         "daily":{"time":["2025-01-03","2025-01-04"],
                  "snowfall_sum":[0.00,0.42],"precipitation_sum":[0.00,0.60],
                  "wind_gusts_10m_max":[17.6,31.0],"temperature_2m_max":[8.1,-0.2],"temperature_2m_min":[-4.4,null]}}
        """;

    private static readonly DateOnly From = new(2025, 1, 3);
    private static readonly DateOnly To = new(2025, 1, 4);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FetchDailyAsync_ValidResponse_MapsColumnsToRows()
    {
        var stub = new StubHttpMessageHandler().RespondJson(TwoDays);
        var client = new OpenMeteoClient(stub.CreateClient(OpenMeteoClient.BaseAddress));

        IReadOnlyList<DailyWeather> days = await client.FetchDailyAsync(HubCatalog.All[0], From, To, Ct);

        Assert.Equal(
            [new DailyWeather(From, 0, 0, 17.6, 8.1, -4.4), new DailyWeather(To, 0.42, 0.60, 31.0, -0.2, null)],
            days);
    }

    [Fact]
    public async Task FetchDailyAsync_BuildsQueryWithInvariantCoordinatesDatesAndVariables()
    {
        var stub = new StubHttpMessageHandler().RespondJson(TwoDays);
        var client = new OpenMeteoClient(stub.CreateClient(OpenMeteoClient.BaseAddress));
        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // Comma decimal separator.
        try
        {
            await client.FetchDailyAsync(HubCatalog.All.Single(h => h.Id == "denver"), From, To, Ct);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        string url = stub.Requests.Single().RequestUri!.ToString();
        Assert.StartsWith("https://archive-api.open-meteo.com/v1/archive?", url);
        Assert.Contains("latitude=39.7392&longitude=-104.9903", url);
        Assert.Contains("start_date=2025-01-03&end_date=2025-01-04", url);
        Assert.Contains("daily=snowfall_sum,precipitation_sum,wind_gusts_10m_max,temperature_2m_max,temperature_2m_min", url);
        Assert.Contains("timezone=auto", url);
    }

    [Fact]
    public async Task FetchDailyAsync_MisalignedArrays_ThrowsInvalidData()
    {
        const string misaligned = """
            {"daily":{"time":["2025-01-03","2025-01-04"],"snowfall_sum":[0.0],"precipitation_sum":[0,0],
                      "wind_gusts_10m_max":[1,1],"temperature_2m_max":[1,1],"temperature_2m_min":[1,1]}}
            """;
        var client = new OpenMeteoClient(new StubHttpMessageHandler().RespondJson(misaligned).CreateClient(OpenMeteoClient.BaseAddress));

        await Assert.ThrowsAsync<InvalidDataException>(() => client.FetchDailyAsync(HubCatalog.All[0], From, To, Ct));
    }

    [Fact]
    public async Task FetchDailyAsync_MissingDailyBlock_ThrowsInvalidData()
    {
        var client = new OpenMeteoClient(new StubHttpMessageHandler().RespondJson("{}").CreateClient(OpenMeteoClient.BaseAddress));

        await Assert.ThrowsAsync<InvalidDataException>(() => client.FetchDailyAsync(HubCatalog.All[0], From, To, Ct));
    }

    [Fact]
    public async Task FetchDailyAsync_ErrorStatus_ThrowsHttpRequestException()
    {
        var client = new OpenMeteoClient(
            new StubHttpMessageHandler().RespondStatus(HttpStatusCode.BadRequest).CreateClient(OpenMeteoClient.BaseAddress));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchDailyAsync(HubCatalog.All[0], From, To, Ct));
    }
}
