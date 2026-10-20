namespace WeatherRisk.Core.Agent;

/// <summary>Short-term memory of conversations, enabling follow-up questions ("and what about Chicago?").</summary>
public interface IConversationStore
{
    /// <summary>Returns the most recent turns of a conversation, oldest first; empty for unknown ids.</summary>
    /// <param name="conversationId">Conversation id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Turns, oldest first.</returns>
    Task<IReadOnlyList<ConversationTurn>> GetAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>Appends a completed turn.</summary>
    /// <param name="conversationId">Conversation id.</param>
    /// <param name="turn">The turn.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AppendAsync(string conversationId, ConversationTurn turn, CancellationToken cancellationToken = default);
}

/// <summary>One question and the assistant's final answer text (tool payloads are not kept — they're re-fetched cheaply).</summary>
/// <param name="UserMessage">The analyst's question.</param>
/// <param name="AssistantAnswer">The assistant's answer text.</param>
/// <param name="At">When the turn completed.</param>
public sealed record ConversationTurn(string UserMessage, string AssistantAnswer, DateTimeOffset At);
