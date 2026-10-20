using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Application;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Ranking;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Tests.Application;

public sealed class HubRiskServiceTests
{
    private static readonly DataWindow Window = new(new DateOnly(2021, 1, 1), new DateOnly(2025, 12, 31));
    private static readonly DateTimeOffset Retrieved = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Hub Minneapolis = TestData.Hub("minneapolis", Region.Midwest);
    private static readonly Hub Chicago = TestData.Hub("chicago", Region.Midwest);
    private static readonly Hub Miami = TestData.Hub("miami", Region.South);

    private readonly FakeProvider _provider = new();
    private readonly HubRiskService _service;

    public HubRiskServiceTests()
    {
        var settings = new ScoringSettings();
        var thresholds = new WeatherThresholds();
        _service = new HubRiskService(
            new FakeHubs(Minneapolis, Chicago, Miami),
            _provider,
            new WeatherStatisticsCalculator(thresholds),
            new RiskScoringEngine(HazardScorers.CreateDefault(settings, thresholds), settings),
            thresholds);

        // Snow every 5th day in Minneapolis, every 10th in Chicago, never in Miami.
        _provider.SnowEvery["minneapolis"] = 5;
        _provider.SnowEvery["chicago"] = 10;
    }

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

        // 1826 days, every 5th a snow day → 366 days / 5 years ≈ 73.2/yr → capped evidence present.
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

    // ---------- Fakes ----------

    private sealed class FakeHubs(params Hub[] hubs) : IHubRepository
    {
        public Task<IReadOnlyList<Hub>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Hub>>(hubs.OrderBy(h => h.Name).ToList());

        public Task<Hub?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(hubs.FirstOrDefault(h => h.Id == Hub.NormalizeId(id)));
    }

    private sealed class FakeProvider : IHubDataProvider
    {
        public Dictionary<string, int> SnowEvery { get; } = [];
        public DataFreshness WeatherFreshness { get; set; } = DataFreshness.Fresh;
        public DataFreshness BaselineFreshness { get; set; } = DataFreshness.Cached;
        public IReadOnlyList<string> LastWeatherRequest { get; private set; } = [];

        public DataWindow Window => HubRiskServiceTests.Window;

        public Task<IReadOnlyDictionary<string, HubWeatherData>> GetWeatherAsync(
            IReadOnlyCollection<Hub> hubs, CancellationToken cancellationToken = default)
        {
            LastWeatherRequest = hubs.Select(h => h.Id).ToList();
            IReadOnlyDictionary<string, HubWeatherData> result = hubs.ToDictionary(h => h.Id, h =>
            {
                if (WeatherFreshness == DataFreshness.Unavailable)
                {
                    return new HubWeatherData(h.Id, Window, [], DataFreshness.Unavailable, null);
                }

                int every = SnowEvery.GetValueOrDefault(h.Id);
                int days = Window.To.DayNumber - Window.From.DayNumber + 1;
                var rows = Enumerable.Range(0, days)
                    .Select(i => Window.From.AddDays(i))
                    .Select(d => new DailyWeather(d, every > 0 && d.DayOfYear % every == 0 ? 3 : 0, 1, 20, 15, 2))
                    .ToList();
                return new HubWeatherData(h.Id, Window, rows, WeatherFreshness, Retrieved);
            });
            return Task.FromResult(result);
        }

        public Task<IReadOnlyDictionary<string, HubBaselineData>> GetBaselinesAsync(
            IReadOnlyCollection<Hub> hubs, CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<string, HubBaselineData> result = hubs
                .Select(h => h.CountyFips)
                .Distinct()
                .ToDictionary(f => f, f => new HubBaselineData(
                    f, new HazardBaseline(50, 50, 50, 50, 50, null, 50, 50, 50, 50), BaselineFreshness, Retrieved));
            return Task.FromResult(result);
        }
    }
}
