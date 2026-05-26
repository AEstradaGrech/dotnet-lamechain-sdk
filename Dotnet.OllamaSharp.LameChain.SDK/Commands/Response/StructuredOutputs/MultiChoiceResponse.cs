using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs
{
    [OllamaJsonOutput("Selection of a choice or choices according to a list of available choices and a maximum number of selections requested", Description = @"A list of strings containing the selected choices. The list must contain a reasoned and ponderated choices 
selected based on how relevant are they to the request and its intent. In case there is not any choice that could match the intent you must return an empty list. The list MUST NO contain more elements than the requested in the instruction, even when there are many
matching value (if that is the case, select the most relevant option according to the intent). Also, you don't need to return always the specified number of choices if there are not enough matches in the list according to your analysis of the intent and the choices list")]
    [OllamaJsonRequirement("The list must contain UP to the requested max selections but not necessarily that number when there are not enough matches in the choices list")]
    public class MultiChoiceResponse : StructuredOutput
    {
        [JsonPropertyName("selected")]
        [OllamaJsonProperty(Title ="Description", PromptDescription = "A list containing the selected values according to the given instructions")]
        [OllamaJsonHint("The list must contain the option or options that matches the best with the query intent")]
        [OllamaJsonHint("Analyze carefully the query intent AND the available options and reason which of them might be good camdidates to select and return those who fit the best with the intent")]
        [OllamaJsonRequirement("- ENSURE that don't select more options than the maximum allowed specified in the instruction.")]
        public List<string> Selected { get; set; }
    }
}
