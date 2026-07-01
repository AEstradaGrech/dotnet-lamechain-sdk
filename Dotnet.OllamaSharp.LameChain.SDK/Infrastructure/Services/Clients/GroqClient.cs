using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Clients;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Request;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Response.GroqProvider;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Clients
{
    public class GroqClient : IGroqClient
    {
        private readonly HttpClient _httpClient;
        private readonly GroqSettings _settings;
        public GroqClient(HttpClient client, IOptions<GroqSettings> settings)
        {
            _httpClient = client ?? throw new ArgumentNullException($"{nameof(GroqClient)} >> {nameof(HttpClient)}");

            _settings = settings.Value ?? throw new ArgumentNullException($"{nameof(GroqClient)} >> {nameof(settings)}");
        }

        public async Task<GroqChatCompletion> GetChatCompletion(GroqChatRequest request)
        {
            if (!_settings.Endpoints.ContainsKey("chat"))
                throw new InvalidDataException($"{nameof(GroqClient)}.{GetChatCompletion} >> Chat Completions Endpoint not found in app settings");

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

            var response = await _httpClient.PostAsJsonAsync<GroqChatRequest>(_settings.EndpointByKey("chat"), request, options);

            if(!response.IsSuccessStatusCode)
                throw new HttpIOException(HttpRequestError.InvalidResponse, "ERROR / TODO");

            var completionResponse = await response.Content.ReadFromJsonAsync<GroqChatCompletion>();

            return completionResponse;
        }
    }
}
