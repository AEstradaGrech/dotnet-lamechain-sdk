namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration
{
    public class GroqSettings
    {
        //https://api.groq.com/openai
        public string BaseUrl { get; set; }
        public string ApiKey { get; set; }
        public string DefaultModel { get; set; }
        public List<string> ToolModels { get; set; } // Tool Call support
        public List<string> JsonModels { get; set; } // Structured Output support
        public List<string> ReasoningModels { get; set; } //Think support
        public Dictionary<string,string> Endpoints { get; set; }
        public string EndpointByKey(string key) => Endpoints.ContainsKey(key) ? Endpoints[key] : string.Empty;
    }
}
