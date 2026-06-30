using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
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

        protected override bool isValid()
            => base.isValid() && _client != null;
    }
}
