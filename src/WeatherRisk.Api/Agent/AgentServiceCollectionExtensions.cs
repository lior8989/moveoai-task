using Microsoft.Extensions.Options;
using WeatherRisk.Api.Observability;
using WeatherRisk.Core.Agent;
using WeatherRisk.Core.Agent.Tools;
using WeatherRisk.Data;
using WeatherRisk.Data.External.Llm;

namespace WeatherRisk.Api.Agent;

/// <summary>Registers the conversational agent: LLM client chain, tools, memory and the agent itself.</summary>
public static class AgentServiceCollectionExtensions
{
    /// <summary>Adds the agent and returns the LLM dependency names (for health checks).</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">App configuration.</param>
    /// <returns>LLM provider dependency names, e.g. <c>llm-groq</c>.</returns>
    public static IReadOnlyList<string> AddChatAgent(this IServiceCollection services, IConfiguration configuration)
    {
        IReadOnlyList<string> llmDependencies = services.AddWeatherRiskLlm(
            configuration.GetSection(LlmSettings.SectionName),
            (dependency, client) => client.AddDependencyMetrics(dependency));

        services.AddOptions<AgentSettings>().Bind(configuration.GetSection(AgentSettings.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<AgentSettings>>().Value);
        services.AddOptions<ConversationSettings>().Bind(configuration.GetSection(ConversationSettings.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IConversationStore, InMemoryConversationStore>();

        // Tools are scoped like the HubRiskService they wrap; one registration per tool (Open/Closed).
        services.AddScoped<IAgentTool, ListHubsTool>();
        services.AddScoped<IAgentTool, RankHubsTool>();
        services.AddScoped<IAgentTool, CompareHubsTool>();
        services.AddScoped<IAgentTool, GetHubRiskTool>();
        services.AddScoped<IAgentTool, GetWeatherStatisticsTool>();
        services.AddScoped<IAgentTool, DescribeRiskModelTool>();
        services.AddScoped<ChatAgent>();

        return llmDependencies;
    }
}
