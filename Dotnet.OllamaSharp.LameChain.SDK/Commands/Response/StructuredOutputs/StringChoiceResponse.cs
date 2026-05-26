using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs
{
    [OllamaJsonOutput("Selection of the best choice from a list based on the query intent", Description = "This schema contains the selected choice that matches the best for the query intent. The choice must be selected according to how relevant or close it is to the request")]
    public class StringChoiceResponse : StructuredOutput
    {
        [JsonPropertyName("selected")]
        [OllamaJsonProperty(Title ="Description", PromptDescription = "The selected best choice according to the given instructions, in string format")]
        [OllamaJsonHint("You must analyze the query intent, review the available choices and reason which one fits the best with the intent")]
        [OllamaJsonRequirement("- The selected choice MUST be the choice that matches the best with the query intent")]
        public string Selected { get; set;  }
    }
}
