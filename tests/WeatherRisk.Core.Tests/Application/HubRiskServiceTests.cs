using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Application;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Ranking;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Tests.Application;

public sealed class HubRiskServiceTests
{
    private static readonly DataWindow Window = FakeHubDataProvider.FixedWindow;
    private static readonly DateTimeOffset Retrieved = FakeHubDataProvider.Retrieved;

    // Snow every 5th day in Minneapolis, every 10th in Chicago, never in Miami.
    private readonly FakeHubDataProvider _provider = ServiceFactory.DefaultProvider();
    private readonly HubRiskService _service;

    public HubRiskServiceTests() => _service = ServiceFactory.Create(_provider);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---------- Assess ----------

    [Fact]
    public async Task AssessAsync_FreshData_ScoresWindowWithProvenanceAndHighConfidence()
    {
        HubAssessment a = await _service.AssessAsync("Minneapolis", Ct);

        Assert.Equal("minneapolis", a.Score.Hub.Id);
        Assert.Equal(ScoreConfidence.High, a.Score.Confidence);
        Assert.Empty(a.Score.Notes);
        Assert.Equal(new HubDataProvenance(Window, DataFreshness.Fresh, Retrieved, DataFreshness.Cached, Retrieved), a.Provenance);

        // 1826 days, every 5th a snow day â†’ 366 days / 5 years â‰ˆ 73.2/yr â†’ capped evidence present.
        Assert.Contains(a.Score.For(HazardType.Winter).Detail.Factors, f => f.Name == "snowDaysPerYear" && f.Value > 70);
    }

    [Fact]
    public async Task AssessAsync_StaleWeatherAndBaseline_AddsNotesWithRetrievalDates()
    {
        _provider.WeatherFreshness = DataFreshness.Stale;
        _provider.BaselineFreshness = DataFreshness.Stale;

        HubAssessment a = await _service.AssessAsync("chicago", Ct);

        Assert.Contains(a.Score.Notes, n => n.StartsWith("Weather source unavailable") && n.Contains("2026-10-01"));
        Assert.Contains(a.Score.Notes, n => n.StartsWith("FEMA source unavailable"));
    }

    [Fact]
    public async Task AssessAsync_WeatherUnavailable_ScoresOnBaselineWithMediumConfidence()
    {
        _provider.WeatherFreshness = DataFreshness.Unavailable;

        HubAssessment a = await _service.AssessAsync("chicago", Ct);

        Assert.Equal(ScoreConfidence.Medium, a.Score.Confidence);
        Assert.Contains(a.Score.Notes, n => n.Contains("Observed weather unavailable"));
        Assert.Null(a.Score.WeatherPeriodFrom);
    }

    [Fact]
    public async Task AssessAsync_UnknownHub_ThrowsHubNotFound()
    {
        var ex = await Assert.ThrowsAsync<HubNotFoundException>(() => _service.AssessAsync("atlantis", Ct));

        Assert.Equal(["atlantis"], ex.HubIds);
    }

    // ---------- Rank ----------

    [Fact]
    public async Task RankAsync_MidwestWinter_LoadsOnlyRegionHubsAndOrdersBySnow()
    {
        RankingResult result = await _service.RankAsync(new RankingCriteria(Region.Midwest, HazardType.Winter), Ct);

        Assert.Equal(["minneapolis", "chicago"], result.Entries.Select(e => e.Details.Hub.Id));
        Assert.Equal(["chicago", "minneapolis"], _provider.LastWeatherRequest.Order());
        Assert.Equal(DataFreshness.Fresh, result.Provenance["chicago"].WeatherFreshness);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task RankAsync_NonPositiveTop_ThrowsValidation(int top)
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => _service.RankAsync(new RankingCriteria(Top: top), Ct));
    }

    // ---------- Compare ----------

    [Fact]
    public async Task CompareAsync_ValidHubs_ReturnsRowsInRequestOrder()
    {
        ComparisonResult result = await _service.CompareAsync(["Miami", "minneapolis"], [HazardType.Winter], Ct);

        Assert.Equal(["miami", "minneapolis"], result.Comparison.HubIds);
        ComparisonRow winter = result.Comparison.Rows.Single(r => r.Hazard == HazardType.Winter);
        Assert.Equal("minneapolis", winter.MostExposedHubId);
        Assert.Equal(2, result.Provenance.Count);
    }

    [Fact]
    public async Task CompareAsync_UnknownHubs_ThrowsWithAllUnknownIds()
    {
        var ex = await Assert.ThrowsAsync<HubNotFoundException>(
            () => _service.CompareAsync(["miami", "gotham", "atlantis"], null, Ct));

        Assert.Equal(["gotham", "atlantis"], ex.HubIds);
    }

    [Theory]
    [InlineData("miami")]
    [InlineData("miami", "MIAMI")]
    [InlineData("miami", " ")]
    public async Task CompareAsync_FewerThanTwoDistinctHubs_ThrowsValidation(params string[] ids)
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => _service.CompareAsync(ids, null, Ct));
    }

    // ---------- Weather statistics ----------

    [Fact]
    public async Task GetWeatherStatistics_NoPeriod_DefaultsToLastFullYear()
    {
        WeatherStatisticsResult result = await _service.GetWeatherStatisticsAsync("minneapolis", cancellationToken: Ct);

        Assert.Equal((new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)), (result.Summary.From, result.Summary.To));
        Assert.Equal(365, result.Summary.DaysObserved);
        Assert.Equal(73, result.Summary.SnowDays);
        Assert.Equal(20.0, result.Summary.PercentOfDays(result.Summary.SnowDays), precision: 6);
        Assert.Equal(DataFreshness.Fresh, result.Freshness);
    }

    [Fact]
    public async Task GetWeatherStatistics_CustomPeriodInsideWindow_IsSummarized()
    {
        WeatherStatisticsResult result = await _service.GetWeatherStatisticsAsync(
            "minneapolis", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31), Ct);

        Assert.Equal(31, result.Summary.DaysObserved);
    }

    [Fact]
    public async Task GetWeatherStatistics_PeriodOutsideWindow_ThrowsValidationNamingTheWindow()
    {
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.GetWeatherStatisticsAsync("minneapolis", new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31), Ct));

        Assert.Contains("2021-01-01", ex.Message);
    }

    [Fact]
    public async Task GetWeatherStatistics_ReversedPeriod_ThrowsValidation()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.GetWeatherStatisticsAsync("minneapolis", new DateOnly(2024, 2, 1), new DateOnly(2024, 1, 1), Ct));
    }
}
