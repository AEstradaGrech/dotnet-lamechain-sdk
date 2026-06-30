using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OllamaSharp.Models.Chat;
using ClaudeMessage = Anthropic.SDK.Messaging.Message;
namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services
{
    public class ClaudeClient : IClaudeClient
    {
        private readonly AnthropicClient _client;
        private readonly IConfiguration _configuration;
        private readonly ClaudeSettings _settings;
        public ClaudeClient(IConfiguration config, IOptions<ClaudeSettings> settings)
        {
            _configuration = config;
            _settings = settings.Value ?? throw new ArgumentNullException(nameof(ClaudeSettings));
            //_apiKey = _config.Get("ClaudeApiKey")
            _client = new AnthropicClient(_settings.ApiKey);
        }

        public async Task<ClaudeMessage> GetMessageAsync(MessageParameters request)
        {
            var response = await _client.Messages.GetClaudeMessageAsync(request);

            return response.Message;
        }

        public async Task<MessageResponse> GetMessageResponseAsync(MessageParameters request)
            => await _client.Messages.GetClaudeMessageAsync(request);

        public async Task<string> TestClient()
        {
            var messages = new List<ClaudeMessage>()
            {
                new ClaudeMessage(RoleType.User, "Who won the world series in 2020?"),
                new ClaudeMessage(RoleType.Assistant, "The Los Angeles Dodgers won the World Series in 2020."),
                new ClaudeMessage(RoleType.User, "Where was it played?"),
            };

            var parameters = new MessageParameters()
            {
                Messages = messages,
                MaxTokens = 1024,
                Model = AnthropicModels.Claude46Sonnet,
                Stream = false,
                Temperature = 1.0m,
            };
            var firstResult = await _client.Messages.GetClaudeMessageAsync(parameters);

            //add assistant message to chain for second call
            messages.Add(firstResult.Message);

            //ask followup question in chain
            messages.Add(new ClaudeMessage(RoleType.User, "Who were the starting pitchers for the Dodgers?"));

            var finalResult = await _client.Messages.GetClaudeMessageAsync(parameters);

            //print result
            Console.WriteLine(finalResult.Message.ToString());
            return finalResult.Message.Content.ToString();
        }
    }
}
