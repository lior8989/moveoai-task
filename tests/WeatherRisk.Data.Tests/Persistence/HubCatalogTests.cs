using System.Text.RegularExpressions;
using WeatherRisk.Core.Domain;
using WeatherRisk.Data.Persistence.Seed;

namespace WeatherRisk.Data.Tests.Persistence;

public sealed partial class HubCatalogTests
{
    [Fact]
    public void All_ContainsFifteenUniqueNormalizedIds()
    {
        Assert.Equal(15, HubCatalog.All.Count);
        Assert.Equal(15, HubCatalog.All.Select(h => h.Id).Distinct().Count());
        Assert.All(HubCatalog.All, h => Assert.Equal(Hub.NormalizeId(h.Id), h.Id));
    }

    [Theory]
    [InlineData("miami")]
    [InlineData("houston")]
    [InlineData("denver")]
    [InlineData("dallas")]
    public void All_IncludesEveryCityNamedInTheBrief(string id)
    {
        Assert.Contains(HubCatalog.All, h => h.Id == id);
    }

    [Fact]
    public void All_HasFiveMidwestHubs_ForRegionalRankings()
    {
        Assert.Equal(5, HubCatalog.All.Count(h => h.Region == Region.Midwest));
    }

    [Fact]
    public void All_HasValidCountyFipsAndContinentalUsCoordinates()
    {
        Assert.All(HubCatalog.All, h =>
        {
            Assert.Matches(FiveDigits(), h.CountyFips);
            Assert.InRange(h.Latitude, 24.0, 50.0);
            Assert.InRange(h.Longitude, -125.0, -66.0);
            Assert.Equal(2, h.State.Length);
        });
    }

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex FiveDigits();
}
