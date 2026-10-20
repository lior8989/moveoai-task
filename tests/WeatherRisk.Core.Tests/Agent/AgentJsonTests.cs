using System.Text.Json;
using WeatherRisk.Core.Agent;

namespace WeatherRisk.Core.Tests.Agent;

public sealed class AgentJsonTests
{
    private static readonly JsonElement Schema = AgentJson.SchemaFor<AgentAnswer>();

    [Fact]
    public void SchemaFor_AgentAnswer_RequiresEveryFieldAndForbidsExtras()
    {
        Assert.Equal("object", Schema.GetProperty("type").GetString());
        Assert.False(Schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["answer", "intent", "findings", "keyFactors", "assumptions", "uncertainty", "followUpQuestions"],
            Schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void SchemaFor_AgentAnswer_ListsIntentValuesAndCarriesDescriptions()
    {
        JsonElement intent = Schema.GetProperty("properties").GetProperty("intent");

        Assert.Contains("OutOfScope", intent.GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
        Assert.Contains("Markdown", Schema.GetProperty("properties").GetProperty("answer").GetProperty("description").GetString());
    }

    [Fact]
    public void SchemaFor_NestedFinding_IsStrictObjectWithRequiredFields()
    {
        JsonElement finding = Schema.GetProperty("properties").GetProperty("findings").GetProperty("items");

        Assert.False(finding.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["hubId", "metric", "value", "unit"], finding.GetProperty("required").EnumerateArray().Select(e => e.GetString()));
    }

    private const string ValidAnswer = """
        {"answer":"A","intent":"Ranking","findings":[],"keyFactors":[],"assumptions":[],"uncertainty":"U","followUpQuestions":[]}
        """;

    [Fact]
    public void Strict_ValidJson_Deserializes()
    {
        AgentAnswer? answer = JsonSerializer.Deserialize<AgentAnswer>(ValidAnswer, AgentJson.Strict);

        Assert.Equal(AnswerIntent.Ranking, answer?.Intent);
    }

    [Theory]
    [InlineData("""{"answer":"A","intent":"Ranking","findings":[],"keyFactors":[],"assumptions":[],"uncertainty":"U","followUpQuestions":[],"extra":1}""")]
    [InlineData("""{"answer":"A","intent":"Ranking","findings":[],"keyFactors":[],"assumptions":[],"followUpQuestions":[]}""")]
    [InlineData("""{"answer":"A","intent":0,"findings":[],"keyFactors":[],"assumptions":[],"uncertainty":"U","followUpQuestions":[]}""")]
    [InlineData("""{"answer":null,"intent":"Ranking","findings":[],"keyFactors":[],"assumptions":[],"uncertainty":"U","followUpQuestions":[]}""")]
    [InlineData("""{"answer":"A","intent":"Guess","findings":[],"keyFactors":[],"assumptions":[],"uncertainty":"U","followUpQuestions":[]}""")]
    public void Strict_SchemaViolations_AreRejected(string json)
    {
        // Unknown property, missing required, numeric enum, null in non-nullable, unknown enum value.
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentAnswer>(json, AgentJson.Strict));
    }
}
