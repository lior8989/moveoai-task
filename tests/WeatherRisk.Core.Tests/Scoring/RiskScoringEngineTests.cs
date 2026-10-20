using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Tests.Scoring;

public sealed class RiskScoringEngineTests
{
    private static IHazardScorer[] Fixed(double winter, double hurricane, double flood, double storm, double heat) =>
    [
        new FixedScorer(HazardType.Winter, winter),
        new FixedScorer(HazardType.Hurricane, hurricane),
        new FixedScorer(HazardType.Flood, flood),
        new FixedScorer(HazardType.SevereStorm, storm),
        new FixedScorer(HazardType.Heat, heat),
    ];

    private static HubHazardInputs CompleteInputs() =>
        new(TestData.Hub(), TestData.Weather(), TestData.Baseline());

    [Fact]
    public void Score_DefaultSettings_ReturnsGoldenComposite()
    {
        var engine = new RiskScoringEngine(Fixed(80, 20, 40, 60, 0), new ScoringSettings());

        // Weighted avg = .25×80 + .2×20 + .2×40 + .2×60 + .15×0 = 44.  Worst = 80 (Winter).
        // Composite = .5×80 + .5×44 = 62 → High.
        HubRiskScore score = engine.Score(CompleteInputs());

        Assert.Equal(62.0, score.CompositeScore);
        Assert.Equal(RiskRating.High, score.Rating);
    }

    [Fact]
    public void Score_Contributions_AreAttributedAndSumToComposite()
    {
        var engine = new RiskScoringEngine(Fixed(80, 20, 40, 60, 0), new ScoringSettings());

        HubRiskScore score = engine.Score(CompleteInputs());

        // Winter: .5×.25×80 + .5×80 = 50; Hurricane .5×.2×20 = 2; Flood 4; Storm 6; Heat 0.
        Assert.Equal([50.0, 2.0, 4.0, 6.0, 0.0], score.Hazards.Select(h => h.Contribution));
        Assert.Equal(score.CompositeScore, score.Hazards.Sum(h => h.Contribution), precision: 6);
        Assert.True(score.For(HazardType.Winter).IsWorst);
        Assert.Single(score.Hazards, h => h.IsWorst);
    }

    [Fact]
    public void Score_TopDrivers_AreLargestTwoContributors()
    {
        var engine = new RiskScoringEngine(Fixed(80, 20, 40, 60, 0), new ScoringSettings());

        HubRiskScore score = engine.Score(CompleteInputs());

        Assert.Equal([HazardType.Winter, HazardType.SevereStorm], score.TopDrivers);
    }

    [Theory]
    [InlineData(0.0, 44.0)]  // pure weighted average
    [InlineData(1.0, 80.0)]  // pure worst hazard
    public void Score_WorstHazardBlendExtremes_BehaveAsDocumented(double blend, double expected)
    {
        var engine = new RiskScoringEngine(Fixed(80, 20, 40, 60, 0), new ScoringSettings { WorstHazardBlend = blend });

        Assert.Equal(expected, engine.Score(CompleteInputs()).CompositeScore);
    }

    [Fact]
    public void Score_WeightsNotSummingToOne_AreNormalized()
    {
        var settings = new ScoringSettings
        {
            WorstHazardBlend = 0,
            Winter = new WinterScoringSettings { CompositeWeight = 1 },
            Hurricane = new SingleMetricScoringSettings { CompositeWeight = 1, DaysPerYearCap = 3 },
            Flood = new SingleMetricScoringSettings { CompositeWeight = 1, DaysPerYearCap = 6 },
            SevereStorm = new SingleMetricScoringSettings { CompositeWeight = 1, DaysPerYearCap = 12 },
            Heat = new SingleMetricScoringSettings { CompositeWeight = 1, DaysPerYearCap = 45 },
        };
        var engine = new RiskScoringEngine(Fixed(80, 20, 40, 60, 0), settings);

        HubRiskScore score = engine.Score(CompleteInputs());

        Assert.Equal(40.0, score.CompositeScore); // (80+20+40+60+0)/5
        Assert.All(score.Hazards, h => Assert.Equal(0.2, h.Weight));
    }

    [Fact]
    public void Score_RealScorersEndToEnd_ReturnsGoldenComposite()
    {
        var settings = new ScoringSettings();
        var engine = new RiskScoringEngine(HazardScorers.CreateDefault(settings, new WeatherThresholds()), settings);
        var inputs = new HubHazardInputs(
            TestData.Hub(),
            TestData.Weather(snow: 80, freeze: 24, tropicalGust: 4, heavyRain: 12, damagingGust: 24, heat: 90),
            TestData.Baseline(70, 50, 60, hurricane: 90, riverine: 40, coastal: 80, tornado: 90, hail: 60,
                strongWind: 30, heatWave: 20));

        // Hazards (see HazardScorerTests): Winter 46.8, Hurricane 90 (baseline only), Flood 55, Storm 56, Heat 38.
        // Avg = .25×46.8 + .2×90 + .2×55 + .2×56 + .15×38 = 57.6.  Composite = .5×90 + .5×57.6 = 73.8.
        HubRiskScore score = engine.Score(inputs);

        Assert.Equal([46.8, 90.0, 55.0, 56.0, 38.0], score.Hazards.Select(h => h.Detail.Score));
        Assert.Equal(73.8, score.CompositeScore);
        Assert.Equal(HazardType.Hurricane, score.TopDrivers[0]);
        Assert.Equal(ScoreConfidence.High, score.Confidence);
        Assert.Empty(score.Notes);
        Assert.Equal(TestData.From, score.WeatherPeriodFrom);
    }

    // ---------- Confidence ----------

    [Fact]
    public void Score_WeatherMissing_MediumConfidenceWithNote()
    {
        var engine = new RiskScoringEngine(Fixed(10, 10, 10, 10, 10), new ScoringSettings());

        HubRiskScore score = engine.Score(new HubHazardInputs(TestData.Hub(), null, TestData.Baseline()));

        Assert.Equal(ScoreConfidence.Medium, score.Confidence);
        Assert.Contains(score.Notes, n => n.Contains("Observed weather unavailable"));
        Assert.Null(score.WeatherPeriodFrom);
    }

    [Fact]
    public void Score_LowWeatherCoverage_MediumConfidenceWithNote()
    {
        var engine = new RiskScoringEngine(Fixed(10, 10, 10, 10, 10), new ScoringSettings());

        HubRiskScore score = engine.Score(
            new HubHazardInputs(TestData.Hub(), TestData.Weather(daysObserved: 700), TestData.Baseline()));

        Assert.Equal(ScoreConfidence.Medium, score.Confidence);
        Assert.Contains(score.Notes, n => n.Contains("covers only"));
    }

    [Fact]
    public void Score_BaselineMissing_MediumConfidenceWithNote()
    {
        var engine = new RiskScoringEngine(Fixed(10, 10, 10, 10, 10), new ScoringSettings());

        HubRiskScore score = engine.Score(new HubHazardInputs(TestData.Hub(), TestData.Weather(), null));

        Assert.Equal(ScoreConfidence.Medium, score.Confidence);
        Assert.Contains(score.Notes, n => n.Contains("FEMA NRI baseline unavailable"));
    }

    [Fact]
    public void Score_NoData_LowConfidenceWithBothNotes()
    {
        var engine = new RiskScoringEngine(Fixed(0, 0, 0, 0, 0), new ScoringSettings());

        HubRiskScore score = engine.Score(new HubHazardInputs(TestData.Hub(), null, null));

        Assert.Equal(ScoreConfidence.Low, score.Confidence);
        Assert.Equal(2, score.Notes.Count);
        Assert.Empty(score.TopDrivers);
    }

    // ---------- Construction guards ----------

    [Fact]
    public void Constructor_DuplicateScorer_Throws()
    {
        IHazardScorer[] scorers = [.. Fixed(1, 1, 1, 1, 1), new FixedScorer(HazardType.Heat, 2)];

        Assert.Throws<ArgumentException>(() => new RiskScoringEngine(scorers, new ScoringSettings()));
    }

    [Fact]
    public void Constructor_MissingScorer_Throws()
    {
        IHazardScorer[] scorers = Fixed(1, 1, 1, 1, 1)[..4];

        Assert.Throws<ArgumentException>(() => new RiskScoringEngine(scorers, new ScoringSettings()));
    }

    [Fact]
    public void Constructor_BlendOutOfRange_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new RiskScoringEngine(Fixed(1, 1, 1, 1, 1), new ScoringSettings { WorstHazardBlend = 1.5 }));
    }

    [Fact]
    public void Constructor_AllWeightsZero_Throws()
    {
        var settings = new ScoringSettings
        {
            Winter = new WinterScoringSettings { CompositeWeight = 0 },
            Hurricane = new SingleMetricScoringSettings { CompositeWeight = 0, DaysPerYearCap = 3 },
            Flood = new SingleMetricScoringSettings { CompositeWeight = 0, DaysPerYearCap = 6 },
            SevereStorm = new SingleMetricScoringSettings { CompositeWeight = 0, DaysPerYearCap = 12 },
            Heat = new SingleMetricScoringSettings { CompositeWeight = 0, DaysPerYearCap = 45 },
        };

        Assert.Throws<ArgumentException>(() => new RiskScoringEngine(Fixed(1, 1, 1, 1, 1), settings));
    }
}
