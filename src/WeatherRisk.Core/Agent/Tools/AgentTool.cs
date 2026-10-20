using System.Text.Json;
using WeatherRisk.Core.Agent.Llm;
using WeatherRisk.Core.Application;

namespace WeatherRisk.Core.Agent.Tools;

/// <summary>A capability the model can invoke (Command pattern): name, description, argument schema, execution.</summary>
public interface IAgentTool
{
    /// <summary>Definition sent to the model.</summary>
    LlmToolDefinition Definition { get; }

    /// <summary>Executes the tool. Never throws for bad input — returns a JSON error the model can act on.</summary>
    /// <param name="argumentsJson">Arguments from the model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result JSON (or <c>{"error": "..."}</c>).</returns>
    Task<ToolResult> InvokeAsync(string argumentsJson, CancellationToken cancellationToken);
}

/// <summary>Outcome of a tool invocation.</summary>
/// <param name="Json">JSON returned to the model.</param>
/// <param name="Succeeded">False when the arguments were invalid or the request failed validation.</param>
public sealed record ToolResult(string Json, bool Succeeded);

/// <summary>
/// Template for tools with typed arguments: the JSON schema is generated from <typeparamref name="TArgs"/>,
/// arguments are parsed strictly, and expected domain errors become messages for the model (so it can
/// correct itself, e.g. after a misspelled hub id) instead of failing the conversation.
/// </summary>
/// <typeparam name="TArgs">Argument record; its schema is what the model sees.</typeparam>
public abstract class AgentTool<TArgs> : IAgentTool
    where TArgs : class
{
    /// <summary>Creates the tool definition.</summary>
    /// <param name="name">Tool name (snake_case).</param>
    /// <param name="description">When to use the tool.</param>
    protected AgentTool(string name, string description) =>
        Definition = new LlmToolDefinition(name, description, AgentJson.SchemaFor<TArgs>());

    /// <inheritdoc />
    public LlmToolDefinition Definition { get; }

    /// <inheritdoc />
    public async Task<ToolResult> InvokeAsync(string argumentsJson, CancellationToken cancellationToken)
    {
        TArgs? args;
        try
        {
            // Some models send "" for no-argument tools; treat it as an empty object.
            args = JsonSerializer.Deserialize<TArgs>(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson, AgentJson.Strict);
        }
        catch (JsonException ex)
        {
            return Error($"Invalid arguments for {Definition.Name}: {ex.Message}");
        }

        if (args is null)
        {
            return Error($"Missing arguments for {Definition.Name}.");
        }

        try
        {
            object result = await ExecuteAsync(args, cancellationToken);
            return new ToolResult(JsonSerializer.Serialize(result, AgentJson.Output), Succeeded: true);
        }
        catch (HubNotFoundException ex)
        {
            return Error($"{ex.Message} Use list_hubs to see valid hub ids.");
        }
        catch (RequestValidationException ex)
        {
            return Error(ex.Message);
        }
    }

    /// <summary>Runs the tool with validated arguments.</summary>
    /// <param name="args">Parsed arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result object serialized to JSON for the model.</returns>
    protected abstract Task<object> ExecuteAsync(TArgs args, CancellationToken cancellationToken);

    private static ToolResult Error(string message) =>
        new(JsonSerializer.Serialize(new { error = message }, AgentJson.Output), Succeeded: false);
}
