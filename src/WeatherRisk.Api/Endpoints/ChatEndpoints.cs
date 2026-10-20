using Microsoft.AspNetCore.Http.HttpResults;
using WeatherRisk.Api.Contracts.Chat;
using WeatherRisk.Api.RateLimiting;
using WeatherRisk.Core.Agent;

namespace WeatherRisk.Api.Endpoints;

/// <summary>The conversational agent under <c>/api/chat</c>.</summary>
public static class ChatEndpoints
{
    /// <summary>Maps the chat endpoint.</summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chat", AskAsync)
            .WithTags("Chat")
            .WithName("Chat")
            .WithSummary("Ask the weather-risk assistant")
            .WithDescription(
                "Natural-language questions about hub weather-disruption risk. The LLM calls the deterministic tools " +
                "(rankings, comparisons, risk breakdowns, weather statistics, model description) and must return a " +
                "JSON-schema-validated answer; every cited number is checked against the tool results (`findings[].verified`). " +
                "Send `conversationId` from a previous response to ask follow-ups. Example: " +
                "`{\"message\":\"Which hubs in the Midwest are most exposed to winter disruption?\"}`.")
            .RequireRateLimiting(RateLimitingExtensions.ChatPolicy)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    /// <summary>Answers one question.</summary>
    /// <param name="request">Question and optional conversation id.</param>
    /// <param name="agent">The agent.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>200 with the structured answer (400/502/503 via the domain exception handler).</returns>
    internal static async Task<Ok<ChatResponse>> AskAsync(ChatRequest request, ChatAgent agent, CancellationToken cancellationToken)
    {
        ChatResult result = await agent.AskAsync(request.Message, request.ConversationId, cancellationToken);
        return TypedResults.Ok(result.ToResponse());
    }
}
