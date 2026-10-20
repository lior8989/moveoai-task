using System.Net;
using System.Text;

namespace WeatherRisk.Data.Tests;

/// <summary>Returns queued responses (or throws queued exceptions) and records every request — no network.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public StubHttpMessageHandler RespondJson(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses.Enqueue(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
        return this;
    }

    public StubHttpMessageHandler RespondStatus(HttpStatusCode status)
    {
        _responses.Enqueue(_ => new HttpResponseMessage(status));
        return this;
    }

    public HttpClient CreateClient(Uri baseAddress) => new(this) { BaseAddress = baseAddress };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException($"No stubbed response left for {request.RequestUri}.");
        }

        return Task.FromResult(_responses.Dequeue()(request));
    }
}
