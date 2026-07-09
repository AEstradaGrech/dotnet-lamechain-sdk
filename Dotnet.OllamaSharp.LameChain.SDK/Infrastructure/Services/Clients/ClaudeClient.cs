using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Clients;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Microsoft.Extensions.Options;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;

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

            _client = new AnthropicClient() { ApiKey = _settings.ApiKey };
            
        }

        /// <summary>
        /// Anthropic SDK implementation
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<Message> GetMessageAsync(MessageCreateParams request)
        {
            var chatClient = _client.AsIChatClient(request.Model);

            var res = await chatClient.GetResponseAsync(new ChatMessage());
            
            var response = await _client.Messages.Create(request);

            return response;
        }

        /// <summary>
        /// OpenAI standard implementation
        /// </summary>
        /// <param name="messages"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options)
        {   
            var chatClient = _client.AsIChatClient();

            return await chatClient.GetResponseAsync(messages, options);
        }
    }
}
