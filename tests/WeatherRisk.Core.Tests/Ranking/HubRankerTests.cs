using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Ranking;
using WeatherRisk.Core.Scoring;

namespace WeatherRisk.Core.Tests.Ranking;

public sealed class HubRankerTests
{
    private static readonly HubRiskScore[] Hubs =
    [
        TestData.Score("miami", 70, Region.South, hurricane: 95, flood: 70),
        TestData.Score("houston", 66, Region.South, hurricane: 80, flood: 85),
        TestData.Score("chicago", 48, Region.Midwest, winter: 60),
        TestData.Score("minneapolis", 52, Region.Midwest, winter: 85),
        TestData.Score("detroit", 45, Region.Midwest, winter: 60),
    ];

    [Fact]
    public void Rank_ByComposite_OrdersDescendingWithSequentialRanks()
    {
        IReadOnlyList<RankedHub> ranked = HubRanker.Rank(Hubs, new RankingCriteria());

        Assert.Equal(["miami", "houston", "minneapolis", "chicago", "detroit"], ranked.Select(r => r.Details.Hub.Id));
        Assert.Equal([1, 2, 3, 4, 5], ranked.Select(r => r.Rank));
        Assert.Equal(70, ranked[0].Score);
    }

    [Fact]
    public void Rank_MidwestWinter_FiltersRegionAndSortsByHazard()
    {
        // "Which hubs in the Midwest are most exposed to winter disruption?"
        IReadOnlyList<RankedHub> ranked = HubRanker.Rank(
            Hubs, new RankingCriteria(Region.Midwest, HazardType.Winter));

        Assert.Equal(["minneapolis", "chicago", "detroit"], ranked.Select(r => r.Details.Hub.Id));
        Assert.Equal(85, ranked[0].Score);
        Assert.Equal(RiskRating.VeryHigh, ranked[0].Rating);
    }

    [Fact]
    public void Rank_TiedHazardScore_BreaksTieByComposite()
    {
        // Chicago and Detroit both have winter 60; Chicago has the higher composite.
        IReadOnlyList<RankedHub> ranked = HubRanker.Rank(
            Hubs, new RankingCriteria(Region.Midwest, HazardType.Winter));

        Assert.True(ranked.Single(r => r.Details.Hub.Id == "chicago").Rank <
                    ranked.Single(r => r.Details.Hub.Id == "detroit").Rank);
    }

    [Fact]
    public void Rank_FullTie_BreaksByName()
    {
        HubRiskScore[] tied = [TestData.Score("b", 50), TestData.Score("a", 50)];

        IReadOnlyList<RankedHub> ranked = HubRanker.Rank(tied, new RankingCriteria());

        Assert.Equal(["a", "b"], ranked.Select(r => r.Details.Hub.Id));
    }

    [Fact]
    public void Rank_Top_LimitsResults()
    {
        IReadOnlyList<RankedHub> ranked = HubRanker.Rank(Hubs, new RankingCriteria(Top: 2));

        Assert.Equal(2, ranked.Count);
    }

    [Fact]
    public void Rank_RegionWithNoHubs_ReturnsEmpty()
    {
        Assert.Empty(HubRanker.Rank(Hubs, new RankingCriteria(Region.Northeast)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rank_NonPositiveTop_Throws(int top)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HubRanker.Rank(Hubs, new RankingCriteria(Top: top)));
    }

    [Fact]
    public void Compare_MiamiHoustonHurricaneFlood_ReportsLeaderAndSpreadPerMetric()
    {
        // "Compare Miami and Houston in terms of hurricane and flood exposure."
        HubComparison comparison = HubRanker.Compare(
            [Hubs[0], Hubs[1]], [HazardType.Flood, HazardType.Hurricane]);

        Assert.Equal(["miami", "houston"], comparison.HubIds);
        Assert.Equal([HubRanker.CompositeMetric, "Hurricane", "Flood"], comparison.Rows.Select(r => r.Metric));

        ComparisonRow hurricane = comparison.Rows[1];
        Assert.Equal("miami", hurricane.MostExposedHubId);
        Assert.Equal(15, hurricane.Spread);

        ComparisonRow flood = comparison.Rows[2];
        Assert.Equal("houston", flood.MostExposedHubId);
        Assert.Equal([70.0, 85.0], flood.Values.Select(v => v.Score));
    }

    [Fact]
    public void Compare_NoHazardsGiven_IncludesAllHazards()
    {
        HubComparison comparison = HubRanker.Compare([Hubs[0], Hubs[1]]);

        Assert.Equal(1 + Enum.GetValues<HazardType>().Length, comparison.Rows.Count);
        Assert.Null(comparison.Rows[0].Hazard);
    }

    [Fact]
    public void Compare_FewerThanTwoHubs_Throws()
    {
        Assert.Throws<ArgumentException>(() => HubRanker.Compare([Hubs[0]]));
    }

    [Fact]
    public void Compare_DuplicateHub_Throws()
    {
        Assert.Throws<ArgumentException>(() => HubRanker.Compare([Hubs[0], Hubs[0]]));
    }
}
