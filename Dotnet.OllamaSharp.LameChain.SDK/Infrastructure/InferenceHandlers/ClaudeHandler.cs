using Dotnet.OllamaSharp.LameChain.SDK.Extensions.Model;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp.Models.Chat;
using System.Reflection;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.InferenceHandlers
{
    public class ClaudeHandler : BaseHandler
    {
        private readonly IClaudeClient _client;
        public ClaudeHandler(IServiceProvider provider, IConfiguration config) 
            : base(provider, config, "claude") 
        {
            _client = provider.GetRequiredService<IClaudeClient>();
        }

        public override async Task<string> GetLlmResponse(ChatRequest request, Dictionary<string, MethodInfo>? requestTools = null)
        {
            if (!isValid())
                throw new InvalidOperationException($"{GetLlmResponse} >> {nameof(isValid)}");

            var response = await _client.GetMessageAsync(request.AsClaudeRequest());
            
            return response.ToString();
        }

        protected override bool isValid()
            => base.isValid() && _client != null;
    }
}
