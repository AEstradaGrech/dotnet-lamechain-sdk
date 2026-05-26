using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes;
using System.Text.Json.Serialization;

// This one fails with dumber models (wrong decimal number calculations and failing to return null when necessary)
// Use at least qwen2.5:14b to make it work
namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs
{
    [OllamaJsonOutput("Numerical answer for a query that can be answered with a decimal / float number according to the user intent and/or any specified instructions", Description = @"This schema contains a 'numeric_result' OR NULL in response to a given query. You MUST analyze the 
query to extract the intent, review any given instructions and reason if the query can be answered in numerical terms. In case it is, reason the problem to a numerical value that is logic and consistent with the analyzed intent. 
# IMPORTANT: In case the query CAN'T be answered in numerical terms,you MUST return null.")]
    [OllamaJsonRequirement("Output value MUST be a float number if the query intent can be answered in numerical terms OR NULL in case it can be not")]
    public class NumericResponse : StructuredOutput
    {
        [JsonPropertyName("numeric_result")]
        [OllamaJsonProperty(Title ="Description", PromptDescription = "A decimal or float value with the right logical response when it is possible to answer with a number, else null")]
        [OllamaJsonHint("Review carefully the input to reason if the request can be answered numerically or not to provide an accurate response that is compliant with the schema and the provided details about the expected output")]
        [OllamaJsonRequirement("- The answer MUST BE A FLOAT WHEN the proposed problem CAN be answered with a number. For example: { 'numeric_result' : 89.89 }")]
        [OllamaJsonRequirement("- The answer MUST BE NULL WHEN the proposed problem CANNOT be answered in numerical terms. For example: { 'numeric_result': null } ")]
        public float? Result { get; set; }
    }
}
