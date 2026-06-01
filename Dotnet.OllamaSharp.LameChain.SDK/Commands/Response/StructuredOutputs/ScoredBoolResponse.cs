using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs
{
    [OllamaJsonOutput("Evaluation and scored boolean answer with justification", Description = "Evaluates the input intent and returns an answer in boolean format representing 'YES' or 'NO' along with a justification comment and a confidence score")]
    public class ScoredBoolResponse : ReasonedBoolResponse
    {
        [JsonPropertyName("confidence_score")]
        [JsonRequired]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "A numeric score representing how sure are you about your answer")]
        [OllamaJsonRequirement("- Value range MUST be from 0.0 to 1.0")]
        public float Score { get; set; }
    }
}
