using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs
{
    [OllamaJsonOutput("Evaluation and boolean response with reasoned answer justification", Description = "Evaluates the input intent and returns an answer in boolean format representing 'YES' or 'NO' along with a justification comment explaining the evaluation")]
    public class ReasonedBoolResponse : BooleanResponse
    {
        [JsonPropertyName("justification")]
        [JsonRequired]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "An accurate explanation of your deliverance justifying your answer in relation your task.")]
        [OllamaJsonRequirement("- NOT MORE THAN 10-15 words explaining your answer.")]
        [OllamaJsonRequirement("- Be specific in your justification, explain why you confirm or negate the question.")]
        public string Justification { get; set; }
    }
}
