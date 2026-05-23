using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutput
{
    public class IntegerChoiceResponse
    {
        [JsonPropertyName("numeric_result")]
        public int Result { get; set; }
    }
}
