using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Agents;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services
{
    public class AgentsFactory : IAgentsFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPromptCommandsFactory _commandsFactory;
        public AgentsFactory(IServiceProvider serviceProvider, IPromptCommandsFactory commandsFactory)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException($"{nameof(IServiceProvider)}");
            _commandsFactory = commandsFactory ?? throw new ArgumentNullException($"{nameof(IPromptCommandsFactory)}");
        }

        public LameAgent CreateAgent(string model, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(LameAgent), _serviceProvider, model, settings, null, null) as LameAgent;

        public LameAgent CreateAgent(string model, string? name, string? description, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(LameAgent), _serviceProvider, model, settings, name, description) as LameAgent;

        public SelectedSkillsAgent CreateSelectedSkillsAgent(string model, string? name = null, string? description = null, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(SelectedSkillsAgent), _serviceProvider, _commandsFactory, model, settings, name, description) as SelectedSkillsAgent;

        public AutoSkilledAgent CreateAutoSkilledAgent(string model, string? name, string? description, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(AutoSkilledAgent), _serviceProvider, model, settings, name, description) as AutoSkilledAgent;
    }
}
