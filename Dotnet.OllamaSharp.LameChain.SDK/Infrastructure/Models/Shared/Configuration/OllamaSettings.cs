using OllamaSharp.Models;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration
{
    public class OllamaSettings : RequestOptions
    {
        public string OllamaUrl { get; set; }
        public string DefaultModel { get; set; }
        public List<string> Models { get; set; } = new List<string>();
        public List<string> EmbeddingModels { get; set; } = new List<string>();
    }
}
