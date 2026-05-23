using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutput
{
    public class BooleanResponse
    {
        [JsonPropertyName("answer")]
        public bool Answer { get; set; }
    }
}

