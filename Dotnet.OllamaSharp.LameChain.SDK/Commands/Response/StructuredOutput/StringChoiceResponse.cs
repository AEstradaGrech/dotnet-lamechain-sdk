using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutput
{
    public class StringChoiceResponse
    {
        [JsonPropertyName("selected")]
        public string Selected { get; set;  }
    }
}
