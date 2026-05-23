using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutput
{
    public class ScoredBoolResponse : BooleanResponse
    {
        [JsonPropertyName("confidence_score")]
        [JsonRequired]
        public float Score { get; set; }
        [JsonPropertyName("justification")]
        [JsonRequired]
        public string Justification { get; set; }
    }
}
