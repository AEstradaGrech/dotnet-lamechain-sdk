using OllamaSharp.Models.Chat;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.JsonConverters
{
    public class GroqToolCallConverter : JsonConverter<Message.ToolCall>
    {

        public override Message.ToolCall? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => JsonSerializer.Deserialize<Message.ToolCall>(ref reader, getSafeSerializerOptions(options));

        public override void Write(Utf8JsonWriter writer, Message.ToolCall value, JsonSerializerOptions options)
        {
            JsonObject obj  = new JsonObject { ["type"] = "function" }; // Generate the patched version of the GroqToolCall model with the missing property

            value.GetType().GetProperties().ToList().ForEach(p => obj.Add(new KeyValuePair<string, JsonNode?>(options.PropertyNamingPolicy.ConvertName(p.Name), JsonSerializer.SerializeToNode(p.GetValue(value), options))));

            obj.WriteTo(writer);
        }

        //Avoid recursive loop when deserializing the ToolCall in the Read method
        private JsonSerializerOptions getSafeSerializerOptions(JsonSerializerOptions currentOptions)
        {
            var result = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            result.PropertyNamingPolicy = currentOptions.PropertyNamingPolicy;
            result.DictionaryKeyPolicy = currentOptions.DictionaryKeyPolicy;
            currentOptions.Converters.ToList().ForEach(converter => {
                if (!(converter is GroqToolCallConverter))
                    result.Converters.Add(converter);
            });

            return result;
        }
    }
}
