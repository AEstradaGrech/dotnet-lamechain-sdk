using Dotnet.OllamaSharp.LameChain.SDK.Extensions.Model;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.InferenceHandlers
{
    public class GroqHandler : BaseHandler
    {
        private IGroqClient _client;
        public GroqHandler(IServiceProvider serviceProvider, IConfiguration config, ChatRequest commandRequest, string provider) : base(serviceProvider, config, commandRequest, provider)
        {
            _client = serviceProvider.GetRequiredService<IGroqClient>();

            if (_client == null)
                throw new ArgumentNullException($"{nameof(GroqHandler)} >> {nameof(IGroqClient)} is not registered as service.");
        }

        public override async Task<string> GetLlmResponse()
        {
            if (!isValid())
                throw new InvalidOperationException($"{GetLlmResponse} >> {nameof(isValid)}");

            var request = CommandRequest.AsGroqRequest();

            var response = await _client.GetChatCompletion(request);

            if (response.Choices.Count == 0)
                throw new InvalidDataException($"{nameof(GroqHandler)}.{nameof(GetLlmResponse)}.{nameof(_client.GetChatCompletion)} >> No message choices present in the response");

            return response.Choices.FirstOrDefault().Message.Content.Trim();
        }

        protected override bool isValid()
            => base.isValid() && _client != null;
    }
}
