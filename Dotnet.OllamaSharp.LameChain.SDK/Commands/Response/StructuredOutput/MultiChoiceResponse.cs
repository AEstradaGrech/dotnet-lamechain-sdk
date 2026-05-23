using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutput
{
    public class MultiChoiceResponse
    {
        [JsonPropertyName("selected")]
        public List<string> Selected { get; set; }
    }
}
