using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs
{
    [OllamaJsonOutput("Selection of the best choice from a list based on the query intent along with a confidence score and a justification comment about your selection.")]
    public class ScoredStringChoice : StringChoiceResponse
    {
        [JsonPropertyName("confidence_score")]
        [JsonRequired]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "A numerical score representing how sure are you about your selection")]
        [OllamaJsonRequirement("- Value range MUST be from 0.0 to 1.0")]
        public float Score { get; set; }

        [JsonPropertyName("justification")]
        [JsonRequired]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "A brief yet accurate explanation of your deliverance justifying your selection")]
        [OllamaJsonHint("Analyze the query intent and review carefully all the avaliable options, then reson which one matches the best for the given intent.")]
        [OllamaJsonRequirement("- Around 10-15 words explaining your selected choice")]
        public string Justification { get; set; }
    }
}
