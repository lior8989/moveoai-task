using System.Net;
using System.Text.Json;
using WeatherRisk.Core.Agent.Llm;
using WeatherRisk.Data.External.Llm;

namespace WeatherRisk.Data.Tests.External.Llm;

public sealed class OpenAiCompatibleChatClientTests
{
    private static readonly LlmProviderSettings Settings = new()
    {
        BaseUrl = "https://llm.test/openai/v1/",
        Models = ["model-a"],
        ApiKey = "secret-key",
        Temperature = 0.2,
        MaxTokens = 1500,
        ReasoningEffort = "medium",
    };

    private static readonly LlmRequest Request = new(
        [
            LlmMessage.System("sys"),
            LlmMessage.User("Rank Midwest"),
            LlmMessage.Assistant(null, [new LlmToolCall("c1", "rank_hubs", """{"region":"Midwest"}""")]),
            LlmMessage.Tool("c1", """{"entries":[]}"""),
        ],
        [new LlmToolDefinition("rank_hubs", "Rank hubs", JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement)],
        RequireToolCall: true);

    private const string ToolCallResponse = """
        {"choices":[{"message":{"role":"assistant","content":null,
          "tool_calls":[{"id":"call_9","type":"function","function":{"name":"submit_answer","arguments":"{\"answer\":\"x\"}"}}]}}],
         "usage":{"prompt_tokens":321,"completion_tokens":45}}
        """;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (OpenAiCompatibleChatClient Client, StubHttpMessageHandler Stub) Create(LlmProviderSettings? settings = null)
    {
        var stub = new StubHttpMessageHandler();
        return (new OpenAiCompatibleChatClient(new HttpClient(stub), "Groq", "model-a", settings ?? Settings), stub);
    }

    [Fact]
    public async Task CompleteAsync_SendsOpenAiCompatibleRequestWithBearerKey()
    {
        (OpenAiCompatibleChatClient client, StubHttpMessageHandler stub) = Create();
        stub.RespondJson(ToolCallResponse);

        await client.CompleteAsync(Request, Ct);

        HttpRequestMessage sent = stub.Requests.Single();
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("https://llm.test/openai/v1/chat/completions", sent.RequestUri!.ToString());
        Assert.Equal(("Bearer", "secret-key"), (sent.Headers.Authorization?.Scheme, sent.Headers.Authorization?.Parameter));

        JsonElement body = JsonDocument.Parse(stub.RequestBodies.Single()!).RootElement;
        Assert.Equal("model-a", body.GetProperty("model").GetString());
        Assert.Equal("required", body.GetProperty("tool_choice").GetString());
        Assert.Equal("medium", body.GetProperty("reasoning_effort").GetString());
        Assert.Equal(1500, body.GetProperty("max_tokens").GetInt32());
        Assert.Equal("rank_hubs", body.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString());
        Assert.Equal(["system", "user", "assistant", "tool"], body.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("role").GetString()));
        JsonElement assistant = body.GetProperty("messages")[2];
        Assert.Equal("c1", assistant.GetProperty("tool_calls")[0].GetProperty("id").GetString());
        Assert.False(assistant.TryGetProperty("content", out _)); // Nulls are omitted.
        Assert.Equal("c1", body.GetProperty("messages")[3].GetProperty("tool_call_id").GetString());
    }

    [Fact]
    public async Task CompleteAsync_ParsesToolCallsAndUsage()
    {
        (OpenAiCompatibleChatClient client, StubHttpMessageHandler stub) = Create();
        stub.RespondJson(ToolCallResponse);

        LlmCompletion completion = await client.CompleteAsync(Request, Ct);

        Assert.Equal(new LlmToolCall("call_9", "submit_answer", """{"answer":"x"}"""), completion.ToolCalls.Single());
        Assert.Equal(("Groq", "model-a", 321, 45), (completion.Provider, completion.Model, completion.PromptTokens, completion.CompletionTokens));
    }

    [Fact]
    public async Task CompleteAsync_NoApiKey_SendsNoAuthorizationHeader()
    {
        (OpenAiCompatibleChatClient client, StubHttpMessageHandler stub) = Create(new LlmProviderSettings
        {
            BaseUrl = "http://localhost:11434/v1/", Models = ["llama"], RequiresApiKey = false,
        });
        stub.RespondJson("""{"choices":[{"message":{"role":"assistant","content":"hi"}}]}""");

        LlmCompletion completion = await client.CompleteAsync(Request, Ct);

        Assert.Null(stub.Requests.Single().Headers.Authorization);
        Assert.Equal("hi", completion.Content);
        Assert.Empty(completion.ToolCalls);
    }

    [Fact]
    public async Task CompleteAsync_ToolUseFailedOnce_RetriesSameModel()
    {
        (OpenAiCompatibleChatClient client, StubHttpMessageHandler stub) = Create();
        stub.RespondJson("""{"error":{"code":"tool_use_failed","message":"attempted to call tool 'json'"}}""", HttpStatusCode.BadRequest)
            .RespondJson(ToolCallResponse);

        LlmCompletion completion = await client.CompleteAsync(Request, Ct);

        Assert.Equal(2, stub.Requests.Count);
        Assert.Equal("submit_answer", completion.ToolCalls.Single().Name);
    }

    [Fact]
    public async Task CompleteAsync_ToolUseFailedTwice_Throws()
    {
        (OpenAiCompatibleChatClient client, StubHttpMessageHandler stub) = Create();
        const string failed = """{"error":{"code":"tool_use_failed"}}""";
        stub.RespondJson(failed, HttpStatusCode.BadRequest).RespondJson(failed, HttpStatusCode.BadRequest);

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => client.CompleteAsync(Request, Ct));
        Assert.Equal(2, stub.Requests.Count);
    }

    [Fact]
    public async Task CompleteAsync_RateLimited_ThrowsWithoutRetrying()
    {
        (OpenAiCompatibleChatClient client, StubHttpMessageHandler stub) = Create();
        stub.RespondJson("""{"error":{"message":"Rate limit reached"}}""", HttpStatusCode.TooManyRequests);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.CompleteAsync(Request, Ct));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.Contains("Groq/model-a returned 429", ex.Message);
        Assert.DoesNotContain("secret-key", ex.Message);
        Assert.Single(stub.Requests);
    }

    [Fact]
    public async Task CompleteAsync_NoChoices_ThrowsInvalidData()
    {
        (OpenAiCompatibleChatClient client, StubHttpMessageHandler stub) = Create();
        stub.RespondJson("""{"choices":[]}""");

        await Assert.ThrowsAsync<InvalidDataException>(() => client.CompleteAsync(Request, Ct));
    }
}
