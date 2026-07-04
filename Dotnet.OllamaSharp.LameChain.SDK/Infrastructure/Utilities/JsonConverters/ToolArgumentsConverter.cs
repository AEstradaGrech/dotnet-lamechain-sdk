using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.JsonConverters
{
    public class ToolArgumentsConverter : JsonConverter<IDictionary<string, object>>
    {
        public override IDictionary<string, object>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                // Groq / OpenAI: arguments is a JSON containing escaped JSON
                case JsonTokenType.String:
                    var raw = reader.GetString();
                    if (string.IsNullOrWhiteSpace(raw))
                        return new Dictionary<string, object>();
                    // deserialize into the CONCRETE Dictionary<> — see note on recursion below
                    return JsonSerializer.Deserialize<Dictionary<string, object>>(raw, options)
                           ?? new Dictionary<string, object>();

                // Ollama: arguments is already a real JSON object
                case JsonTokenType.StartObject:
                    return JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options)
                           ?? new Dictionary<string, object>();

                case JsonTokenType.Null:
                    return new Dictionary<string, object>();

                default:
                    throw new JsonException($"Unexpected token '{reader.TokenType}' when reading tool call arguments.");
            }
        }

        public override void Write(Utf8JsonWriter writer, IDictionary<string, object> value, JsonSerializerOptions options)
        {
            // Groq/OpenAI expects `arguments` as a stringified JSON on the way OUT too,
            // so re-stringify. Materializing a concrete Dictionary<> avoids re-entering
            // this converter (which would infinitely recurse).
            var concrete = value.ToDictionary(kv => kv.Key, kv => kv.Value);
            writer.WriteStringValue(JsonSerializer.Serialize(concrete, options));
        }
    }
}
