using WeatherRisk.Core.Domain;

namespace WeatherRisk.Core.Tests.Domain;

public sealed class HubTests
{
    [Theory]
    [InlineData("dallas", "dallas")]
    [InlineData("Dallas", "dallas")]
    [InlineData("  NEW-YORK ", "new-york")]
    public void NormalizeId_TrimsAndLowerCases(string raw, string expected)
    {
        Assert.Equal(expected, Hub.NormalizeId(raw));
    }

    [Fact]
    public void NormalizeId_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Hub.NormalizeId(null!));
    }
}
