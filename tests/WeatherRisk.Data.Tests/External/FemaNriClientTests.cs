using System.Globalization;
using System.Text;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Data.External.Fema;

namespace WeatherRisk.Data.Tests.External;

public sealed class FemaNriClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Builds an ArcGIS page where every hazard frequency of a county equals the given value.</summary>
    private static string Page(bool more, params (string Fips, double? Value)[] counties)
    {
        var sb = new StringBuilder("{\"features\":[");
        sb.AppendJoin(',', counties.Select(c =>
        {
            string v = c.Value is { } x ? x.ToString(CultureInfo.InvariantCulture) : "null";
            return $"{{\"attributes\":{{\"STCOFIPS\":\"{c.Fips}\",\"WNTW_AFREQ\":{v},\"ISTM_AFREQ\":{v},\"CWAV_AFREQ\":{v}," +
                   $"\"HRCN_AFREQ\":{v},\"IFLD_AFREQ\":{v},\"CFLD_AFREQ\":{v},\"TRND_AFREQ\":{v},\"HAIL_AFREQ\":{v}," +
                   $"\"SWND_AFREQ\":{v},\"HWAV_AFREQ\":{v}}}}}";
        }));
        sb.Append("],\"exceededTransferLimit\":").Append(more ? "true" : "false").Append('}');
        return sb.ToString();
    }

    [Fact]
    public async Task FetchAsync_PagesUntilNoMoreRows_AndRanksAgainstAllCounties()
    {
        var stub = new StubHttpMessageHandler()
            .RespondJson(Page(more: true, ("00001", 1), ("00002", 2)))
            .RespondJson(Page(more: false, ("00003", 3), ("00004", 4)));
        var client = new FemaNriClient(stub.CreateClient(FemaNriClient.BaseAddress));

        IReadOnlyDictionary<string, HazardBaseline> result = await client.FetchAsync(["00003"], Ct);

        Assert.Equal(2, stub.Requests.Count);
        Assert.Contains("resultOffset=0&", stub.Requests[0].RequestUri!.Query);
        Assert.Contains($"resultOffset={FemaNriClient.PageSize}&", stub.Requests[1].RequestUri!.Query);
        Assert.Contains("orderByFields=STCOFIPS", stub.Requests[0].RequestUri!.Query);

        // Value 3 among [1,2,3,4]: 2 below + 0.5 × 1 equal → 2.5 / 4 = 62.5th percentile, for every hazard.
        HazardBaseline baseline = Assert.Single(result).Value;
        Assert.Equal(62.5, baseline.WinterWeather);
        Assert.Equal(62.5, baseline.RiverineFlooding);
        Assert.Equal(62.5, baseline.HeatWave);
    }

    [Fact]
    public async Task FetchAsync_NullFrequency_StaysNull()
    {
        var stub = new StubHttpMessageHandler().RespondJson(Page(more: false, ("00001", null), ("00002", 5)));
        var client = new FemaNriClient(stub.CreateClient(FemaNriClient.BaseAddress));

        IReadOnlyDictionary<string, HazardBaseline> result = await client.FetchAsync(["00001"], Ct);

        Assert.Null(result["00001"].Hurricane);
        Assert.Null(result["00001"].CoastalFlooding);
    }

    [Fact]
    public async Task FetchAsync_ArcGisErrorBody_ThrowsHttpRequestException()
    {
        var stub = new StubHttpMessageHandler().RespondJson("""{"error":{"code":400,"message":"Invalid query"}}""");
        var client = new FemaNriClient(stub.CreateClient(FemaNriClient.BaseAddress));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchAsync(["00001"], Ct));
        Assert.Contains("Invalid query", ex.Message);
    }

    [Fact]
    public async Task FetchAsync_UnknownCounty_IsOmitted()
    {
        var stub = new StubHttpMessageHandler().RespondJson(Page(more: false, ("00001", 1)));
        var client = new FemaNriClient(stub.CreateClient(FemaNriClient.BaseAddress));

        Assert.Empty(await client.FetchAsync(["99999"], Ct));
    }

    [Fact]
    public async Task FetchAsync_NoCountiesRequested_MakesNoCall()
    {
        var stub = new StubHttpMessageHandler();
        var client = new FemaNriClient(stub.CreateClient(FemaNriClient.BaseAddress));

        Assert.Empty(await client.FetchAsync([], Ct));
        Assert.Empty(stub.Requests);
    }
}
