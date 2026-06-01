using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs
{
    [OllamaJsonOutput("Scoring of an input given an evaluation instruction", Description = "Evaluates the input intent and returns a decimal number representing a confidence score about the  evaluation, where 0.0 and 1.0 represents FALSE and TRUE respectively and 0.5 represents NOT SURE")]
    public class ScoredResponse : StructuredOutput
    {
        [JsonPropertyName("confidence_score")]
        [JsonRequired]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "A decimal number that scores the given input according to a given instruction")]
        [OllamaJsonHint("The score should represent the confidence about the answer")]
        [OllamaJsonRequirement("Value MUST range from 0.0 to 1.0")]

        public float Score { get; set; }
    }
}
