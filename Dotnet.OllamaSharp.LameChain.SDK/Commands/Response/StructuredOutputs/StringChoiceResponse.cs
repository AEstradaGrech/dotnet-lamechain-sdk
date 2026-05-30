using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs
{
    [OllamaJsonOutput("Selection of the best choice from a list based on the query intent", Description = @"This schema contains the selected choice that matches the best for the query intent. 
The choice must be selected according to how relevant or close it is to the request")]
    [OllamaJsonRequirement("Select ONLY values that are available in the list.")]
    [OllamaJsonRequirement("Return an empty string IF you don't find any choice in the list that can be use to answer the query.")]

    public class StringChoiceResponse : StructuredOutput
    {
        [JsonPropertyName("selected")]
        [OllamaJsonProperty(Title ="Description", PromptDescription = "The selected best choice according to the given instructions, in string format")]
        [OllamaJsonHint("You must analyze the query intent, review the available choices and reason which one fits the best with the intent")]
        [OllamaJsonHint("IF you don't find any good choice in the list for the given query, return an empty string")]
        [OllamaJsonRequirement("- The selected choice MUST be the choice that matches the best with the query intent OR empty if there is not any.")]
        public string Selected { get; set;  }
    }
}
