using System.Text.Json;
using System.Text.Json.Nodes;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Response
{
    public class JsonPromptResult
    {
        public JsonPromptResult() { }
        public JsonPromptResult(string instruction, object result, Type type, string jsonString, JsonNode schemaNode, JsonSerializerOptions serializerOptions)
        {
            Instruction = instruction;
            Object = result;
            Type = type;
            RawJson = jsonString;
            Schema = schemaNode;
            SerializerOptions = serializerOptions;
        }
        public string Instruction { get; set; }
        public Type Type { get; set; }
        public object Object { get; set; }
        public string RawJson { get; set; }
        public JsonNode Schema { get; set; }
        public JsonSerializerOptions SerializerOptions { get; set; }
    }
}
