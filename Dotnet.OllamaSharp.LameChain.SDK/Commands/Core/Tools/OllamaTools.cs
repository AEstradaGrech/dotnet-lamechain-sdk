using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;


namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Tools
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
