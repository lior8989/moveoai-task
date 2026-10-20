using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Scoring.Hazards;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Tests.Scoring;

/// <summary>
/// Golden-value tests for each hazard scorer with default settings. TestData.Weather spans exactly 4 years,
/// so "N days" means N/4 days per year. Expected values are worked out in the comments.
/// </summary>
public sealed class HazardScorerTests
{
    private static readonly ScoringSettings Settings = new();
    private static readonly WeatherThresholds Thresholds = new();

    // ---------- Winter ----------

    [Fact]
    public void Winter_BothSignals_ReturnsGoldenScore()
    {
        var scorer = new WinterScorer(Settings, Thresholds);

        // Snow: 80 days → 20/yr → 20/40 = 50.   Freeze: 24 days → 6/yr → 6/30 = 20.
        // Observed = 0.6×50 + 0.4×20 = 38.      Baseline = mean(70, 50, 60) = 60.
        // Score = 0.6×38 + 0.4×60 = 46.8 → Moderate.
        HazardScore s = scorer.Score(
            TestData.Weather(snow: 80, freeze: 24),
            TestData.Baseline(winterWeather: 70, iceStorm: 50, coldWave: 60));

        Assert.Equal(HazardType.Winter, s.Hazard);
        Assert.Equal(38.0, s.ObservedScore);
        Assert.Equal(60.0, s.BaselineScore);
        Assert.Equal(46.8, s.Score);
        Assert.Equal(RiskRating.Moderate, s.Rating);
        Assert.Contains(s.Factors, f => f is { Name: "snowDaysPerYear", Value: 20.0, Unit: "days/yr" });
        Assert.Contains(s.Factors, f => f is { Name: "nriIceStorm", Value: 50.0 });
    }

    [Fact]
    public void Winter_ExtremeSnow_ObservedComponentIsCapped()
    {
        var scorer = new WinterScorer(Settings, Thresholds);

        // 400 snow days → 100/yr → capped at 100; no freezes → observed = 0.6×100 = 60.
        HazardScore s = scorer.Score(TestData.Weather(snow: 400), baseline: null);

        Assert.Equal(60.0, s.Score);
    }

    [Fact]
    public void Winter_PartialBaseline_AveragesOnlyAvailableComponents()
    {
        var scorer = new WinterScorer(Settings, Thresholds);

        HazardScore s = scorer.Score(weather: null, TestData.Baseline(winterWeather: 80));

        Assert.Equal(80.0, s.BaselineScore);
        Assert.Equal(80.0, s.Score);
        Assert.Null(s.ObservedScore);
    }

    // ---------- Hurricane ----------

    [Fact]
    public void Hurricane_BothSignals_UsesBaselineOnlyButKeepsGustEvidence()
    {
        var scorer = new HurricaneScorer(Settings, Thresholds);

        // Observed weight 0 → score = baseline 90. Gust days (1/yr → 33.3) remain visible as evidence.
        HazardScore s = scorer.Score(TestData.Weather(tropicalGust: 4), TestData.Baseline(hurricane: 90));

        Assert.Equal(33.3, s.ObservedScore);
        Assert.Equal(90.0, s.Score);
        Assert.Equal(RiskRating.VeryHigh, s.Rating);
        Assert.Contains(s.Factors, f => f is { Name: "tropicalStormGustDaysPerYear", Value: 1.0 });
        Assert.Contains(s.Factors, f => f is { Name: "nriHurricane", Value: 90.0 });
    }

    [Fact]
    public void Hurricane_BaselineUnavailable_FallsBackToObservedProxy()
    {
        var scorer = new HurricaneScorer(Settings, Thresholds);

        HazardScore s = scorer.Score(TestData.Weather(tropicalGust: 4), baseline: null);

        Assert.Equal(33.3, s.Score);
    }

    [Fact]
    public void Hurricane_FemaRatesNotApplicable_ScoresZeroDespiteGustyWeather()
    {
        // Denver: frequent ≥90 km/h downslope gusts, but FEMA rates hurricanes not applicable.
        var scorer = new HurricaneScorer(Settings, Thresholds);

        HazardScore s = scorer.Score(TestData.Weather(tropicalGust: 40), TestData.Baseline(winterWeather: 80));

        Assert.Equal(0, s.Score);
        Assert.Equal(RiskRating.Low, s.Rating);
        Assert.Contains(s.Factors, f => f.Name == "notApplicable");
        Assert.Contains(s.Factors, f => f.Name == "tropicalStormGustDaysPerYear"); // Evidence kept.
    }

    [Fact]
    public void Winter_FemaRatesAllComponentsNotApplicable_ScoresZero()
    {
        var scorer = new WinterScorer(Settings, Thresholds);

        HazardScore s = scorer.Score(TestData.Weather(snow: 80), TestData.Baseline(hurricane: 50));

        Assert.Equal(0, s.Score);
        Assert.Contains(s.Factors, f => f.Name == "notApplicable");
    }

    // ---------- Flood ----------

    [Fact]
    public void Flood_CoastalHub_AveragesRiverineAndCoastal()
    {
        var scorer = new FloodScorer(Settings, Thresholds);

        // 12 days → 3/yr → 3/6 = 50.  Baseline = mean(40, 80) = 60.  Score = 0.5×50 + 0.5×60 = 55.
        HazardScore s = scorer.Score(TestData.Weather(heavyRain: 12), TestData.Baseline(riverine: 40, coastal: 80));

        Assert.Equal(55.0, s.Score);
    }

    [Fact]
    public void Flood_InlandHub_IgnoresMissingCoastalComponent()
    {
        var scorer = new FloodScorer(Settings, Thresholds);

        // Baseline = riverine only = 40.  Score = 0.5×50 + 0.5×40 = 45.
        HazardScore s = scorer.Score(TestData.Weather(heavyRain: 12), TestData.Baseline(riverine: 40));

        Assert.Equal(40.0, s.BaselineScore);
        Assert.Equal(45.0, s.Score);
    }

    // ---------- Severe storm ----------

    [Fact]
    public void SevereStorm_BothSignals_ReturnsGoldenScore()
    {
        var scorer = new SevereStormScorer(Settings, Thresholds);

        // 24 days → 6/yr → 6/12 = 50.  Baseline = mean(90, 60, 30) = 60.  Score = 0.4×50 + 0.6×60 = 56.
        HazardScore s = scorer.Score(
            TestData.Weather(damagingGust: 24), TestData.Baseline(tornado: 90, hail: 60, strongWind: 30));

        Assert.Equal(56.0, s.Score);
        Assert.Equal(RiskRating.High, s.Rating);
    }

    // ---------- Heat ----------

    [Fact]
    public void Heat_BothSignals_ReturnsGoldenScore()
    {
        var scorer = new HeatScorer(Settings, Thresholds);

        // 90 days → 22.5/yr → 22.5/45 = 50.  Score = 0.6×50 + 0.4×20 = 38.
        HazardScore s = scorer.Score(TestData.Weather(heat: 90), TestData.Baseline(heatWave: 20));

        Assert.Equal(38.0, s.Score);
    }

    // ---------- Shared behaviour ----------

    public static TheoryData<IHazardScorer> AllScorers() => new(HazardScorers.CreateDefault(Settings, Thresholds));

    [Theory]
    [MemberData(nameof(AllScorers))]
    public void AnyScorer_NoDataAtAll_ScoresZeroWithoutEvidence(IHazardScorer scorer)
    {
        HazardScore s = scorer.Score(weather: null, baseline: null);

        Assert.Equal(0, s.Score);
        Assert.Null(s.ObservedScore);
        Assert.Null(s.BaselineScore);
        Assert.Empty(s.Factors);
    }

    [Theory]
    [MemberData(nameof(AllScorers))]
    public void AnyScorer_ZeroDaysObserved_TreatsWeatherAsMissing(IHazardScorer scorer)
    {
        HazardScore s = scorer.Score(TestData.Weather(daysObserved: 0), baseline: null);

        Assert.Null(s.ObservedScore);
    }

    [Fact]
    public void CreateDefault_ReturnsOneScorerPerHazard()
    {
        IReadOnlyList<IHazardScorer> scorers = HazardScorers.CreateDefault(Settings, Thresholds);

        Assert.Equal(Enum.GetValues<HazardType>(), scorers.Select(s => s.Hazard));
    }
}
