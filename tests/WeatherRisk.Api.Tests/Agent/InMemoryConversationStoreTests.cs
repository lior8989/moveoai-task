using Microsoft.Extensions.Options;
using WeatherRisk.Api.Agent;
using WeatherRisk.Core.Agent;

namespace WeatherRisk.Api.Tests.Agent;

public sealed class InMemoryConversationStoreTests : IDisposable
{
    private readonly InMemoryConversationStore _store = new(Options.Create(new ConversationSettings { MaxTurns = 3 }));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ConversationTurn Turn(int i) => new($"q{i}", $"a{i}", DateTimeOffset.UnixEpoch.AddMinutes(i));

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task Get_UnknownConversation_ReturnsEmpty()
    {
        Assert.Empty(await _store.GetAsync("nope", Ct));
    }

    [Fact]
    public async Task Append_ThenGet_ReturnsTurnsOldestFirst()
    {
        await _store.AppendAsync("c", Turn(1), Ct);
        await _store.AppendAsync("c", Turn(2), Ct);

        Assert.Equal(["q1", "q2"], (await _store.GetAsync("c", Ct)).Select(t => t.UserMessage));
    }

    [Fact]
    public async Task Append_BeyondMaxTurns_DropsOldest()
    {
        for (int i = 1; i <= 5; i++)
        {
            await _store.AppendAsync("c", Turn(i), Ct);
        }

        Assert.Equal(["q3", "q4", "q5"], (await _store.GetAsync("c", Ct)).Select(t => t.UserMessage));
    }

    [Fact]
    public async Task Conversations_AreIsolated()
    {
        await _store.AppendAsync("a", Turn(1), Ct);
        await _store.AppendAsync("b", Turn(2), Ct);

        Assert.Equal("q1", (await _store.GetAsync("a", Ct)).Single().UserMessage);
        Assert.Equal("q2", (await _store.GetAsync("b", Ct)).Single().UserMessage);
    }

    [Fact]
    public async Task Get_ReturnsSnapshotUnaffectedByLaterAppends()
    {
        await _store.AppendAsync("c", Turn(1), Ct);
        IReadOnlyList<ConversationTurn> snapshot = await _store.GetAsync("c", Ct);

        await _store.AppendAsync("c", Turn(2), Ct);

        Assert.Single(snapshot);
    }
}
