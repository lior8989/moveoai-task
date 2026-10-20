using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WeatherRisk.Eval;

/// <summary>Read access to API ground truth (GET returning JSON).</summary>
internal interface IApiReader
{
    /// <summary>GETs a path and returns the parsed JSON body.</summary>
    /// <param name="path">API path, e.g. <c>/api/hubs/dallas/risk</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response JSON.</returns>
    Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken);
}

/// <summary>Outcome of one chat request.</summary>
/// <param name="Body">Response JSON when successful.</param>
/// <param name="Status">HTTP status code.</param>
/// <param name="Error">Error text when unsuccessful.</param>
/// <param name="ElapsedMs">Round-trip time measured by the client.</param>
internal sealed record ChatOutcome(JsonElement? Body, int Status, string? Error, double ElapsedMs);

/// <summary>
/// HTTP client for the running API. GET responses are cached for the run (ground truth doesn't change
/// mid-run, and repeated checks shouldn't re-query). Chat requests honour 429 <c>Retry-After</c> once.
/// </summary>
internal sealed class ApiClient : IApiReader, IDisposable
{
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, JsonElement> _cache = new(StringComparer.Ordinal);

    /// <summary>Creates the client.</summary>
    /// <param name="baseUrl">API base URL, e.g. <c>http://localhost:5050</c>.</param>
    public ApiClient(Uri baseUrl) =>
        _http = new HttpClient { BaseAddress = baseUrl, Timeout = TimeSpan.FromSeconds(150) };

    /// <inheritdoc />
    public async Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(path, out JsonElement cached))
        {
            return cached;
        }

        using HttpResponseMessage response = await _http.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        _cache[path] = body;
        return body;
    }

    /// <summary>GETs without caching (for before/after metrics snapshots).</summary>
    /// <param name="path">API path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response JSON.</returns>
    public async Task<JsonElement> GetFreshAsync(string path, CancellationToken cancellationToken) =>
        await _http.GetFromJsonAsync<JsonElement>(path, cancellationToken);

    /// <summary>Asks the agent one question; on 429 waits for <c>Retry-After</c> and retries once.</summary>
    /// <param name="message">Question.</param>
    /// <param name="conversationId">Conversation to continue, or null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome (never throws for HTTP errors).</returns>
    public async Task<ChatOutcome> AskAsync(string message, string? conversationId, CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                using HttpResponseMessage response = await _http.PostAsJsonAsync(
                    "/api/chat", new { message, conversationId }, cancellationToken);
                double ms = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;

                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt == 0)
                {
                    TimeSpan wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(15);
                    await Task.Delay(wait + TimeSpan.FromSeconds(1), cancellationToken);
                    continue;
                }

                string text = await response.Content.ReadAsStringAsync(cancellationToken);
                return response.IsSuccessStatusCode
                    ? new ChatOutcome(JsonDocument.Parse(text).RootElement.Clone(), (int)response.StatusCode, null, ms)
                    : new ChatOutcome(null, (int)response.StatusCode, text.Length > 400 ? text[..400] : text, ms);
            }
            catch (HttpRequestException ex)
            {
                return new ChatOutcome(null, 0, ex.Message, System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();
}
