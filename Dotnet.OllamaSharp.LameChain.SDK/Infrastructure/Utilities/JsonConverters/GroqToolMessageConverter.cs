using OllamaSharp.Models.Chat;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.JsonConverters
{
    public class GroqToolMessageConverter : JsonConverter<Message>
    {
        private List<string> _unsupportedFields = new List<string> { nameof(Message.ToolName), nameof(Message.Images), nameof(Message.Thinking), nameof(Message.ToolCalls) };
        public override Message? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => JsonSerializer.Deserialize<Message?>(ref reader, getSafeSerializerOptions(options));

        public override void Write(Utf8JsonWriter writer, Message value, JsonSerializerOptions options)
        {
            if (value.Role == ChatRole.Tool)
            {
                var obj = new JsonObject();

                value.GetType().GetProperties().ToList().ForEach(p => 
                {
                    if (_unsupportedFields.Contains(p.Name))
                    {
                        if (p.Name == nameof(Message.ToolCalls))
                        {
                            var toolCall = value.ToolCalls.SingleOrDefault(x => x.Function.Name == value.ToolName);

                            obj.Add(new KeyValuePair<string, JsonNode>("tool_call_id", JsonSerializer.SerializeToNode(toolCall.Id)));
                        }
                    }
                    else obj.Add(new KeyValuePair<string, JsonNode>(options.PropertyNamingPolicy.ConvertName(p.Name), JsonSerializer.SerializeToNode(p.GetValue(value))));
                });

                obj.WriteTo(writer);
            }

            else writer.WriteRawValue(JsonSerializer.Serialize<Message>(value, getSafeSerializerOptions(options)));
        }

        private JsonSerializerOptions getSafeSerializerOptions(JsonSerializerOptions currentOptions)
        {
            var result = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            result.PropertyNamingPolicy = currentOptions.PropertyNamingPolicy;
            result.DictionaryKeyPolicy = currentOptions.DictionaryKeyPolicy;
            currentOptions.Converters.ToList().ForEach(converter => {
                if (!(converter is GroqToolMessageConverter))
                    result.Converters.Add(converter);
            });

            return result;
        }
    }
}
