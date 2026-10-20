using System.Text.Json;
using WeatherRisk.Eval;

namespace WeatherRisk.Eval.Tests;

/// <summary>Ground truth from canned JSON keyed by URL.</summary>
internal sealed class FakeApi(Dictionary<string, string> responses) : IApiReader
{
    public Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken) =>
        responses.TryGetValue(path, out string? json)
            ? Task.FromResult(JsonDocument.Parse(json).RootElement)
            : throw new HttpRequestException($"404 {path}");
}
