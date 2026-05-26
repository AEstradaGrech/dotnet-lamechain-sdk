using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs
{
    [OllamaJsonOutput("Numeric answer for a query that can be answered with an integer number according to the user intent or any specified instructions")]
    [OllamaJsonRequirement("Output value MUST be an integer")]
    public class IntegerChoiceResponse : StructuredOutput
    {
        [JsonPropertyName("numeric_result")]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "The requested integer value")]
        [OllamaJsonHint("Analyze the query input and any given instruction to figure out how to answer the query with an integer (analyze wheter you are being asked to count, perform some calculus or select an option based on an integer value).")]
        public int Result { get; set; }
    }
}
