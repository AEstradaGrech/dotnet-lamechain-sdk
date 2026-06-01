using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs
{
    [OllamaJsonOutput("Evaluation answering in boolean binary terms", Description = "This schema represents the result of an evaluation on a requested topic answered in terms of 'YES / NO' , 'POSITIVE / NEGATIVE', 'POSSIBLE / NOT POSSIBLE'.")]
    [OllamaJsonRequirement("An answer in boolean format representing 'YES' or 'NO' as response to your given instructions and or queries")]
    public class BooleanResponse : StructuredOutput
    {
        [JsonPropertyName("answer")]
        [OllamaJsonProperty(Title ="Description", PromptDescription ="A boolean response indicating 'POSITIVE' or 'NEGATIVE' in relation with YOUR proposed problem or question")]
        [OllamaJsonRequirement("- The answer MUST be a boolean representing the output of your evaluation")]
        public bool Answer { get; set; }
    }
}

