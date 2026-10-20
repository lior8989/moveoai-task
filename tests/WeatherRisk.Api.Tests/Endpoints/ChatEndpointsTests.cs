using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using WeatherRisk.Api.Contracts.Chat;
using WeatherRisk.Api.Contracts.System;
using WeatherRisk.Api.Tests.Infrastructure;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Agent;
using WeatherRisk.Core.Agent.Llm;

namespace WeatherRisk.Api.Tests.Endpoints;

/// <summary>Full-pipeline tests for POST /api/chat with a fake LLM and deterministic stub data.</summary>
public sealed class ChatEndpointsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (WeatherRiskApiFactory Factory, FakeLlmClient Llm) Create(IReadOnlyDictionary<string, string?>? settings = null)
    {
        var llm = new FakeLlmClient();
        var factory = new WeatherRiskApiFactory
        {
            Settings = settings ?? new Dictionary<string, string?>(),
            TestServices = services =>
            {
                services.AddScoped<IHubDataProvider, StubHubDataProvider>();
                services.AddSingleton<ILlmClient>(llm);
            },
        };
        return (factory, llm);
    }

    private static Task<HttpResponseMessage> AskAsync(HttpClient client, string message, string? conversationId = null) =>
        client.PostAsJsonAsync("/api/chat", new ChatRequest(message, conversationId), TestJson.Options, Ct);

    [Fact]
    public async Task Ask_ReturnsStructuredGroundedAnswerWithToolTrace()
    {
        (WeatherRiskApiFactory factory, _) = Create();
        await using (factory)
        {
            HttpResponseMessage response = await AskAsync(factory.CreateClient(), "Which Midwest hubs are most exposed to winter disruption?");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            ChatResponse? chat = await response.Content.ReadFromJsonAsync<ChatResponse>(TestJson.Options, Ct);
            Assert.NotNull(chat);
            Assert.False(string.IsNullOrWhiteSpace(chat.ConversationId));
            Assert.Equal(AnswerIntent.Ranking, chat.Intent);
            Assert.Contains("minneapolis", chat.Answer);
            FindingResponse finding = Assert.Single(chat.Findings);
            Assert.True(finding.Verified);
            Assert.True(chat.Verification.AllFindingsGrounded);
            ToolCallResponse tool = Assert.Single(chat.ToolCalls);
            Assert.Equal("rank_hubs", tool.Tool);
            Assert.Equal("Midwest", tool.Arguments.GetProperty("region").GetString());
            Assert.Equal(("Fake", "fake-model", 2), (chat.Llm.Provider, chat.Llm.Model, chat.Llm.Steps));
            Assert.NotEmpty(chat.Assumptions);
            Assert.False(string.IsNullOrWhiteSpace(chat.Uncertainty));
        }
    }

    [Fact]
    public async Task Ask_FollowUpWithConversationId_SendsHistoryToTheModel()
    {
        (WeatherRiskApiFactory factory, FakeLlmClient llm) = Create();
        await using (factory)
        {
            HttpClient client = factory.CreateClient();
            ChatResponse? first = await (await AskAsync(client, "First question")).Content.ReadFromJsonAsync<ChatResponse>(TestJson.Options, Ct);

            HttpResponseMessage second = await AskAsync(client, "And what about Chicago?", first!.ConversationId);

            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Contains(llm.LastMessages, m => m is { Role: LlmRole.User, Content: "First question" });
            Assert.Contains(llm.LastMessages, m => m.Role == LlmRole.Assistant && m.Content == first.Answer);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Ask_EmptyMessage_Returns400(string message)
    {
        (WeatherRiskApiFactory factory, FakeLlmClient llm) = Create();
        await using (factory)
        {
            HttpResponseMessage response = await AskAsync(factory.CreateClient(), message);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(0, llm.Requests); // Rejected before any LLM cost.
        }
    }

    [Fact]
    public async Task Ask_MalformedJson_Returns400NotServerError()
    {
        (WeatherRiskApiFactory factory, _) = Create();
        await using (factory)
        {
            HttpResponseMessage response = await factory.CreateClient().PostAsync(
                "/api/chat", new StringContent("""{"message": 5, "conversationId": [1]}""", Encoding.UTF8, "application/json"), Ct);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task Ask_AllProvidersDown_Returns503WithHintToDeterministicEndpoints()
    {
        (WeatherRiskApiFactory factory, FakeLlmClient llm) = Create();
        llm.Mode = FakeLlmMode.Unavailable;
        await using (factory)
        {
            HttpResponseMessage response = await AskAsync(factory.CreateClient(), "Rank hubs");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
            Assert.Equal("Assistant unavailable", problem!.Title);
            Assert.Contains("/api/risk/rankings", problem.Extensions["hint"]!.ToString());
        }
    }

    [Fact]
    public async Task Ask_ModelNeverProducesAnswer_Returns502()
    {
        (WeatherRiskApiFactory factory, FakeLlmClient llm) = Create();
        llm.Mode = FakeLlmMode.ProseOnly;
        await using (factory)
        {
            HttpResponseMessage response = await AskAsync(factory.CreateClient(), "Rank hubs");

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            Assert.Equal(8, llm.Requests); // Bounded by Agent:MaxSteps.
        }
    }

    [Fact]
    public async Task Ask_ExceedingChatRateLimit_Returns429_WhileOtherEndpointsStillWork()
    {
        (WeatherRiskApiFactory factory, _) = Create(new Dictionary<string, string?>
        {
            ["RateLimiting:Chat:PermitLimit"] = "1",
            ["RateLimiting:Chat:SegmentsPerWindow"] = "1",
        });
        await using (factory)
        {
            HttpClient client = factory.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await AskAsync(client, "one")).StatusCode);

            HttpResponseMessage limited = await AskAsync(client, "two");

            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
            Assert.True(limited.Headers.RetryAfter is not null);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/hubs", Ct)).StatusCode);
        }
    }

    [Fact]
    public async Task OpenApiAndHealth_IncludeChatEndpointAndLlmDependencies()
    {
        (WeatherRiskApiFactory factory, _) = Create();
        await using (factory)
        {
            HttpClient client = factory.CreateClient();
            string document = await client.GetStringAsync("/openapi/v1.json", Ct);
            HealthResponse? health = await client.GetFromJsonAsync<HealthResponse>("/api/system/health", Ct);

            Assert.Contains("\"/api/chat\"", document);
            Assert.Contains("\"503\"", document);
            Assert.Contains(health!.Checks, c => c.Name == "llm-groq");
        }
    }
}
