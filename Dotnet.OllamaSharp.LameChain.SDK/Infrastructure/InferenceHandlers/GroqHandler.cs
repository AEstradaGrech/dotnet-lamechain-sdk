using Dotnet.OllamaSharp.LameChain.SDK.Extensions.Model;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp.Models.Chat;
using System.Reflection;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.InferenceHandlers
{
    public class GroqHandler : BaseHandler
    {
        private IGroqClient _client;
        public GroqHandler(IServiceProvider serviceProvider, IConfiguration config) 
            : base(serviceProvider, config, "groq")
        {
            _client = serviceProvider.GetRequiredService<IGroqClient>();

            if (_client == null)
                throw new ArgumentNullException($"{nameof(GroqHandler)} >> {nameof(IGroqClient)} is not registered as service.");
        }

        public override async Task<string> GetLlmResponse(ChatRequest request, Dictionary<string, MethodInfo>? requestTools = null)
        {
            if (!isValid())
                throw new InvalidOperationException($"{GetLlmResponse} >> {nameof(isValid)}");

            var response = await _client.GetChatCompletion(request.AsGroqRequest());

            if (response.Choices.Count == 0)
                throw new InvalidDataException($"{nameof(GroqHandler)}.{nameof(GetLlmResponse)}.{nameof(_client.GetChatCompletion)} >> No message choices present in the response");

            return response.Choices.FirstOrDefault().Message.Content.Trim();
        }

        protected override bool isValid()
            => base.isValid() && _client != null;
    }
}
