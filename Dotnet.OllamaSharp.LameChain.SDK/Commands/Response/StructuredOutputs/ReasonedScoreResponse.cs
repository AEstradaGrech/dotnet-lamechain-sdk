using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs
{
    [OllamaJsonOutput("Input evaluation and scoring with reasoned score justification", Description = "Evaluates the input intent and returns a score according to the given evaluation instruction, along with a justification comment explaining the scoring")]
    public class ReasonedScoreResponse : ScoredResponse
    {
        [JsonPropertyName("justification")]
        [JsonRequired]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "A brief yet accurate explanation of your deliverance justifying your score in relation to the intent")]
        [OllamaJsonRequirement("- 10-15 words long explaining your score")]
        [OllamaJsonRequirement("- Be specific in your justification, explain why the reasons that led you to score the intent the way you did")]
        public string Justification { get; set; }
    }
}
