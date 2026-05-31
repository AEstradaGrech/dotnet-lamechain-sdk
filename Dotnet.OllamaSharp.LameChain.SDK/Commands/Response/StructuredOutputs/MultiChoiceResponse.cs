using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs
{
    [OllamaJsonOutput("Ranged multi-selection of choices according to a list of options and a range upper limit", Description = @"A list of strings containing the selected choices (if any). The list must contain a reasoned and ponderated selection of choices
based on how relevant are they to the request and the user intent when there are available choices in the list matching the user query.")]
    [OllamaJsonRequirement("A list containing the best matching choices (if any) clamped to the MAXIMUM ALLOWED SELECTIONS")]
    [OllamaJsonRequirement("The list MUST be empty ONLY IF there are no valid choices in the list to answer the user")]
    [OllamaJsonRequirement("The selected choices (if any) MUST be always present in the provided available choices list.")]
    public class MultiChoiceResponse : StructuredOutput
    {
        [JsonPropertyName("selected")]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "A list containing the selected choices from the provided list of choices OR empty when there are no suitable choices to answer the user properly.")]
        [OllamaJsonHint("Analyze carefully the query intent AND the available choices in the list and reason which of them match with the intent")]
        [OllamaJsonHint("The list must contain ONLY valid choices from the available list that match with the query intent, else return an empty list")]
        [OllamaJsonRequirement("- ENSURE that you DON'T SELECT MORE OPTIONS THAN THE MAXIMUM SELECTIONS ALLOWED specified in the instruction.")]
        public List<string> Selected { get; set; }
    }
}
