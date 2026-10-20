using System.Text.Json;
using WeatherRisk.Eval;

namespace WeatherRisk.Eval.Tests;

public sealed class JsonPathTests
{
    private static readonly JsonElement Doc = JsonDocument.Parse("""
        {
          "entries": [ { "hubId": "minneapolis", "rank": 1, "confidence": "High" }, { "hubId": "chicago", "rank": 2, "confidence": "High" } ],
          "rows": [ { "metric": "Hurricane", "values": [ { "hubId": "miami", "score": 98.6 }, { "hubId": "houston", "score": 87.6 } ] } ],
          "topDrivers": [ "Flood", "SevereStorm" ],
          "rating": "High"
        }
        """).RootElement;

    [Theory]
    [InlineData("entries[0].hubId", "minneapolis")]
    [InlineData("entries[hubId=chicago].rank", "2")]
    [InlineData("ENTRIES[HUBID=CHICAGO].RANK", "2")]
    [InlineData("rows[metric=Hurricane].values[hubId=houston].score", "87.6")]
    [InlineData("topDrivers[1]", "SevereStorm")]
    [InlineData("rating", "High")]
    public void Single_ResolvesIndexFilterAndNestedPaths(string path, string expected)
    {
        Assert.Equal(expected, JsonPath.Text(JsonPath.Single(Doc, path)));
    }

    [Fact]
    public void Resolve_Wildcard_ReturnsAllItems()
    {
        Assert.Equal(["High", "High"], JsonPath.Resolve(Doc, "entries[*].confidence").Select(JsonPath.Text));
    }

    [Theory]
    [InlineData("entries[5].hubId")]
    [InlineData("entries[hubId=gotham].rank")]
    [InlineData("missing.path")]
    public void Resolve_MissingPath_ReturnsEmpty(string path)
    {
        Assert.Empty(JsonPath.Resolve(Doc, path));
    }

    [Fact]
    public void Single_NoOrManyMatches_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => JsonPath.Single(Doc, "entries[9]"));
        Assert.Throws<KeyNotFoundException>(() => JsonPath.Single(Doc, "entries[*].hubId"));
    }

    [Fact]
    public void Resolve_InvalidSegment_ThrowsFormat()
    {
        Assert.Throws<FormatException>(() => JsonPath.Resolve(Doc, "entries[0"));
    }
}
