namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration
{
    public class GroqSettings
    {
        //https://api.groq.com/openai
        public string BaseUrl { get; set; }
        public string ApiKey { get; set; }
        // ["chat"] => "/v1/chat/completions"
        public Dictionary<string,string> Endpoints { get; set; }
        public string EndpointByKey(string key) => Endpoints.ContainsKey(key) ? Endpoints[key] : string.Empty;
    }
}
