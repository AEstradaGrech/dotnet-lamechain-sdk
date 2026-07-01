using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System.Text;


namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.InferenceHandlers
{
    public class OllamaHandler : BaseHandler
    {
        private IOllamaApiClient _client;
        public OllamaHandler(IServiceProvider provider, IConfiguration config, ChatRequest commandRequest) 
            : base(provider, config, commandRequest, "ollama") 
        {
            _client = provider.GetRequiredService<IOllamaApiClient>();
        }

        public override async Task<string> GetLlmResponse()
        {
            if (!isValid())
                throw new InvalidOperationException($"{nameof(OllamaHandler)} >> {nameof(GetLlmResponse)} >> {nameof(isValid)}");

            var sb = new StringBuilder();

            await foreach (var part in _client.ChatAsync(CommandRequest))
                if (!string.IsNullOrEmpty(part?.Message.Content))
                    sb.Append(part.Message.Content);

            return sb.ToString().Trim();
        }

        public async Task<string> GenerateLlmResponse(GenerateRequest request)
        {
            if (_client == null)
                throw new ArgumentNullException($"{nameof(OllamaHandler)}.{nameof(GenerateLlmResponse)} >> {nameof(IOllamaApiClient)} is null");

            if (string.IsNullOrEmpty(request.System) && string.IsNullOrEmpty(request.Prompt))
                throw new InvalidDataException($"{nameof(OllamaHandler)}.{nameof(GenerateLlmResponse)} >> No system message or user prompt present in the request");

            var sb = new StringBuilder();

            await foreach (var part in _client.GenerateAsync(request))
                if (!string.IsNullOrEmpty(part?.Response))
                    sb.Append(part.Response);

            return sb.ToString().Trim();
        }

        protected override bool isValid()
            => base.isValid() && _client != null;
    }
}
