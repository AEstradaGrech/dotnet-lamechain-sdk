using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs
{
    [OllamaJsonOutput("Evaluation of a query and answering in binary terms", Description = "This schema represents the answer for a query in terms of 'YES / NO' , 'POSITIVE / NEGATIVE', 'POSSIBLE / NOT POSSIBLE'.")]
    [OllamaJsonRequirement("An answer in boolean format representing 'YES' or 'NO'")]
    public class BooleanResponse : StructuredOutput
    {
        [JsonPropertyName("answer")]
        [OllamaJsonProperty(Title ="Description", PromptDescription ="A boolean to indicate 'POSITIVE' or 'NEGATIVE' to the proposed problem or question")]
        [OllamaJsonRequirement("- The answer MUST be a boolean representing the output of of your evaluation")]
        public bool Answer { get; set; }
    }
}

