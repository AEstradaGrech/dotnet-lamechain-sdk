using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;


namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities
{
    public static class OllamaTools
    {
        public static object FromMethod(MethodInfo methodInfo)
        {
            return new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = methodInfo.Name,
                    ["description"] = methodInfo.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "",
                    ["parameters"] = (JsonNode)buildParametersSchema(methodInfo)
                }
            };
        }

        public static object?[] ParseToolCallArguments(MethodInfo methodInfo, IDictionary<string, object> arguments)
            => methodInfo.GetParameters().Select(p => {
                if (arguments.TryGetValue(p.Name!, out var raw) && raw is JsonElement jsonElem)
                    return jsonElem.Deserialize(p.ParameterType);

                if (p.HasDefaultValue) return p.DefaultValue;

                throw new InvalidOperationException($"Missing required argument '{p.Name}' for tool '{methodInfo.Name}'");
            }).ToArray();

        public static AIFunction ToAIFunction(MethodInfo methodInfo, IServiceProvider serviceProvider)
            => AIFunctionFactory.Create(
                methodInfo,
                // The Func overload defers instance creation to invoke time, so the tool instance is
                // resolved fresh (and correctly scoped) on each call instead of being captured now.
                // The AIFunctionArguments parameter is ignored — we don't need it because we close over
                // the scoped provider. Resolve the same way getToolResult does: via IToolsService<T>,
                // whose registration hands back the concrete tools instance the method is invoked on.
                createInstanceFunc: _ => serviceProvider.GetRequiredService(
                    typeof(IToolsService<>).MakeGenericType(methodInfo.DeclaringType!)),
                new AIFunctionFactoryOptions
                {
                    Name = methodInfo.Name,
                    Description = methodInfo.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty,
                    SerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                        // AIFunctionFactory freezes these options (MakeReadOnly); STJ won't freeze an
                        // options instance with no resolver, so set the default reflection resolver explicitly.
                        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
                    }
                });
        
        private static JsonNode buildParametersSchema(MethodInfo method)
        {
            var props = new JsonObject();
            var required = new JsonArray();

            foreach (var p in method.GetParameters())
            {
                var schema = JsonSerializerOptions.Default.GetJsonSchemaAsNode(p.ParameterType);

                // Inyecta la descripción del atributo dentro del nodo de schema
                var desc = p.GetCustomAttribute<DescriptionAttribute>()?.Description;
                if (desc is not null && schema is JsonObject obj)
                    obj["description"] = desc;

                props[p.Name!] = schema;

                if (!p.IsOptional && !p.HasDefaultValue)
                    required.Add(p.Name!);
            }
            return new JsonObject
            {
                ["type"] = "object",
                ["properties"] = props,
                ["required"] = required
            };
        }
    }
}
