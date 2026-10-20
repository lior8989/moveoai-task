using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WeatherRisk.Core.Agent;

namespace WeatherRisk.Api.Agent;

/// <summary>
/// Bounded in-process conversation memory: each conversation keeps its most recent turns and expires after a
/// period of inactivity; the number of conversations is capped. Singleton.
/// </summary>
/// <remarks>
/// Uses its own <see cref="MemoryCache"/> (with a size limit) rather than the shared one, so the cap applies
/// only here. In-memory by design for a single-instance demo; a SQLite/Redis implementation of
/// <see cref="IConversationStore"/> would make conversations survive restarts and scale out.
/// </remarks>
public sealed class InMemoryConversationStore : IConversationStore, IDisposable
{
    private readonly MemoryCache _cache;
    private readonly ConversationSettings _settings;

    /// <summary>Creates the store.</summary>
    /// <param name="settings">Capacity and expiry.</param>
    public InMemoryConversationStore(IOptions<ConversationSettings> settings)
    {
        _settings = settings.Value;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = _settings.MaxConversations });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ConversationTurn>> GetAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ConversationTurn> turns = _cache.TryGetValue(conversationId, out Conversation? conversation)
            ? conversation!.Snapshot()
            : [];
        return Task.FromResult(turns);
    }

    /// <inheritdoc />
    public Task AppendAsync(string conversationId, ConversationTurn turn, CancellationToken cancellationToken = default)
    {
        Conversation conversation = _cache.GetOrCreate(conversationId, entry =>
        {
            entry.Size = 1; // One unit per conversation → SizeLimit = max conversations.
            entry.SlidingExpiration = TimeSpan.FromMinutes(_settings.IdleMinutes);
            return new Conversation(_settings.MaxTurns);
        })!;
        conversation.Add(turn);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose() => _cache.Dispose();

    /// <summary>Thread-safe bounded list of turns (oldest dropped first).</summary>
    private sealed class Conversation(int maxTurns)
    {
        private readonly Lock _gate = new();
        private readonly Queue<ConversationTurn> _turns = new();

        public void Add(ConversationTurn turn)
        {
            lock (_gate)
            {
                _turns.Enqueue(turn);
                while (_turns.Count > maxTurns)
                {
                    _turns.Dequeue();
                }
            }
        }

        public IReadOnlyList<ConversationTurn> Snapshot()
        {
            lock (_gate)
            {
                return [.. _turns];
            }
        }
    }
}

/// <summary>Conversation memory limits, bound from <c>Conversations</c>.</summary>
public sealed class ConversationSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Conversations";

    /// <summary>Maximum concurrent conversations kept (default 1000); the least valuable are evicted beyond it.</summary>
    [System.ComponentModel.DataAnnotations.Range(1, 1_000_000)]
    public int MaxConversations { get; init; } = 1000;

    /// <summary>Turns kept per conversation (default 10).</summary>
    [System.ComponentModel.DataAnnotations.Range(1, 100)]
    public int MaxTurns { get; init; } = 10;

    /// <summary>Minutes of inactivity before a conversation is forgotten (default 60).</summary>
    [System.ComponentModel.DataAnnotations.Range(1, 24 * 60)]
    public int IdleMinutes { get; init; } = 60;
}
