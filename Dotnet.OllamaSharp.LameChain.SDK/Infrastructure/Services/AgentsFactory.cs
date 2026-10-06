
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Agents;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services
{
    public class AgentsFactory : IAgentsFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public AgentsFactory(IServiceProvider serviceProvider, IOllamaInferenceService inferenceService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException($"{nameof(IServiceProvider)}");
        }

        public LameAgent CreateAgent(string model, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(LameAgent), _serviceProvider, model, settings, null, null) as LameAgent;

        public LameAgent CreateAgent(string model, string? name, string? description, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(LameAgent), _serviceProvider, model, settings, name, description) as LameAgent;
    }
}
