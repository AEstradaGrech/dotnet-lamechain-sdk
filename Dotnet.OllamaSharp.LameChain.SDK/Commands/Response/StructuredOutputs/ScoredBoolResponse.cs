using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs
{
    [OllamaJsonOutput("Evaluation and scored answer of a user query with justification", Description = "Evaluates the user intent and returns an answer in boolean format representing 'YES' or 'NO' along with a justification comment and a confidence score")]
    public class ScoredBoolResponse : BooleanResponse
    {
        [JsonPropertyName("confidence_score")]
        [JsonRequired]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "A numeric score representing how sure are you about your answer")]
        [OllamaJsonRequirement("- Value range MUST be from 0.0 to 1.0")]

        public float Score { get; set; }


        [JsonPropertyName("justification")]
        [JsonRequired]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "A brief yet accurate explanation of your deliverance justifying your answer in relation to the intent")]
        [OllamaJsonRequirement("- 10-15 words long explaining your answer")]
        [OllamaJsonRequirement("- Be specific in your justification, explain why you confirm or negate the question")]
        public string Justification { get; set; }
    }
}
