using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Clients;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Microsoft.Extensions.Options;
using ClaudeMessage = Anthropic.SDK.Messaging.Message;
namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Clients
{
    public class ClaudeClient : IClaudeClient
    {
        private readonly AnthropicClient _client;
        private readonly ClaudeSettings _settings;
        public ClaudeClient(IOptions<ClaudeSettings> settings)
        {
            _settings = settings.Value ?? throw new ArgumentNullException(nameof(ClaudeSettings));
            
            if (string.IsNullOrEmpty(_settings.ApiKey)) 
                throw new InvalidDataException($"{nameof(ClaudeClient)} >> No Claude API KEY configured");

            _client = new AnthropicClient(_settings.ApiKey);
        }

        public async Task<ClaudeMessage> GetMessageAsync(MessageParameters request)
        {
            var response = await _client.Messages.GetClaudeMessageAsync(request);

            return response.Message;
        }

        public async Task<MessageResponse> GetMessageResponseAsync(MessageParameters request)
            => await _client.Messages.GetClaudeMessageAsync(request);
    }
}
