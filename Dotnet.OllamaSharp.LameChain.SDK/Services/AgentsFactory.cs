using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Agents;

namespace Dotnet.OllamaSharp.LameChain.SDK.Services
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

        public T CreateAgent<T>(string model, string? name, string? description, PromptSettings? settings = null) where T : LameAgent
            => Activator.CreateInstance(typeof(T), _serviceProvider, model, settings, name, description) as T;
        public T CreateAgent<T>(string model, PromptSettings? settings = null) where T : LameAgent
            => Activator.CreateInstance(typeof(T), _serviceProvider, model, settings, null, null) as T;

        public LameAgent CreateAgent(string model, PromptSettings? settings = null)
            => new LameAgent(_serviceProvider, model, settings, null, null);

        public LameAgent CreateAgent(string model, string? name, string? description, PromptSettings? settings = null)
            => new LameAgent(_serviceProvider, model, settings, name, description);

        public SelectedSkillsAgent CreateSelectedSkillsAgent(string model, string? name = null, string? description = null, PromptSettings? settings = null)
            => new SelectedSkillsAgent(_serviceProvider, _commandsFactory, model, settings, name, description);

        public AutoSkilledAgent CreateAutoSkilledAgent(string model, string? name, string? description, PromptSettings? settings = null)
            => new AutoSkilledAgent(_serviceProvider, model, settings, name, description);

        public PythonAgent CreatePythonAgent(string model, PromptSettings? settings = null)
            => new PythonAgent(_serviceProvider, model, settings, null, null);

        public PhilosopherAgent CreatePhilosopherAgent(string model, PromptSettings? settings = null)
            => new PhilosopherAgent(_serviceProvider, model, settings, null, null);

        public SmartPhilosopherAgent CreateSmartPhilosopherAgent(string model, PromptSettings? settings = null)
            => new SmartPhilosopherAgent(_serviceProvider, model, settings, null, null);
    }
}
