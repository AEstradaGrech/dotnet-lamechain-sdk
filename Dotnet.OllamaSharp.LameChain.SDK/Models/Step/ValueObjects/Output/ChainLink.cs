using System.Text.Json;
using System.Text.Json.Nodes;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects.Outputs
{
    public class ChainLink
    {
        public ChainLink(Guid stepId, string instruction, string jsonResult, JsonNode result, Type serializedType, string? feedForwardMessage = null)
        {
            StepId = stepId;
            Instruction = instruction;
            SerializedResult = jsonResult;
            JsonSchema = result;
            ForwardGuidance = feedForwardMessage;
            SerializedType = serializedType;
        }
        public Guid StepId { get; set; }
        public string Instruction { get; set; }
        public string SerializedResult { get; set; } // THE RESULT SERIALIZED TO JSON (seems weird for ChatMessageCommands, but it is meant to store any kind of complex StructuredOutput with many types and/or properties)
        public JsonNode JsonSchema { get; set; } // WHAT IS THE RESULT IN LLM TERMS. PASS TO LLM AGAIN TO REPEAT, SERIALIZE AND PASS TO SYSTEM FOR CONTEXT ABOUT PREV STEP
        public string? ForwardGuidance { get; set;  } // What to do with the result. This is for the NEXT STEP TOO
        public Type SerializedType { get; set; }
        public bool IsValid() => !string.IsNullOrEmpty(SerializedResult) && JsonSchema != null; // Message && Deserializable
        
        public string SchemaForMessage() => IsValid() ? 
            JsonSerializer.Serialize(JsonSchema) : 
            "[CONTEXT ERROR: MISSING DATA]"; //Tell the LLM there is an error in this section so it does not hallucinate the response because it is instructed to review an empty section
        
        public object TypedResult() => JsonSerializer.Deserialize(SerializedResult, SerializedType) ?? throw new InvalidOperationException($"Failed to deserialize JSON to type {SerializedType.Name}");
    }
}
