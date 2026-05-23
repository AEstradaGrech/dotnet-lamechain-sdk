using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutput
{
    public class NumericResponse
    {
        [JsonPropertyName("numeric_result")]
        public float? Result { get; set; }
    }
}
