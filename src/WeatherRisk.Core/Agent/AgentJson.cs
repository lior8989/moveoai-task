using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;

namespace WeatherRisk.Core.Agent;

/// <summary>
/// JSON conventions shared by the agent: serializer options for tool results, strict options for parsing
/// model output, and JSON-schema generation from C# types — one source of truth for both the schema the
/// model sees and the validation our code applies.
/// </summary>
public static class AgentJson
{
    /// <summary>Options for serializing tool results to the model: camelCase, enums as names, nulls omitted.</summary>
    public static JsonSerializerOptions Output { get; } = Freeze(new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    });

    /// <summary>
    /// Strict options for parsing what the model sends (tool arguments and the final answer): unknown
    /// properties, missing required properties, nulls in non-nullable members and numeric enum values are
    /// all rejected — this is where the JSON-schema contract is enforced.
    /// </summary>
    public static JsonSerializerOptions Strict { get; } = Freeze(new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
    });

    /// <summary>
    /// Generates the JSON schema for <typeparamref name="T"/> as the model should produce it (using
    /// <see cref="Strict"/> conventions), with <see cref="DescriptionAttribute"/> texts as descriptions and
    /// <c>additionalProperties: false</c> on objects.
    /// </summary>
    /// <typeparam name="T">Arguments or answer type.</typeparam>
    /// <returns>Schema as a JSON element.</returns>
    public static JsonElement SchemaFor<T>()
    {
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = static (context, node) =>
            {
                if (node is not JsonObject obj)
                {
                    return node;
                }

                ICustomAttributeProvider? provider = context.PropertyInfo?.AttributeProvider ?? context.TypeInfo.Type;
                if (provider?.GetCustomAttributes(typeof(DescriptionAttribute), inherit: false)
                        .OfType<DescriptionAttribute>().FirstOrDefault() is { } description)
                {
                    obj.Insert(0, "description", description.Description);
                }

                if (context.PropertyInfo is null && obj["properties"] is not null)
                {
                    obj["additionalProperties"] = false; // Mirrors UnmappedMemberHandling.Disallow.
                }

                return obj;
            },
        };

        JsonNode schema = Strict.GetJsonSchemaAsNode(typeof(T), exporterOptions);
        return JsonSerializer.SerializeToElement(schema);
    }

    private static JsonSerializerOptions Freeze(JsonSerializerOptions options)
    {
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
