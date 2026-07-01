
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Response.GroqProvider
{
    public class GroqCompletionUsage
    {

        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}
