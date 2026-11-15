
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Agents
{
    /// <summary>
    /// An agent equipped with the Lame Reasoning tools (lite version).
    /// Use this version when using dumb / small models (7b - 12b or less)
    /// </summary>
    public class PhilosopherAgent : LameAgent
    {
        public PhilosopherAgent(IServiceProvider serviceProvider, string model, PromptSettings? settings = null, string? name = null, string? description = null)
            : base(serviceProvider, model, settings, name, description)
        {
            if (string.IsNullOrEmpty(name))
                Name = "Philosopher / Reasoning Agent";

            if (string.IsNullOrEmpty(description))
                Description = "An agent specialized in thinking and reasoning.";

            _useSelfInstruction = true;
            
            using var toolboxScope = _serviceProvider.CreateScope();

            var toolbox = toolboxScope.ServiceProvider.GetRequiredService<IToolbox>();

            bool hasTool = false;
            if (toolbox.TryGetMethodInfo(nameof(LameTools.ReasoningTool), out MethodInfo reasoningToolMethod))
            {
                hasTool = true;
                AddTool(nameof(LameTools.ReasoningTool), new ToolBinding(reasoningToolMethod));
            }
            else
            {
                if(_onBroadcast != null)
                    _onBroadcast(Id, $"{nameof(PhilosopherAgent)} >> {broadcastId()} >> No reasoning tool loaded: {nameof(LameTools.ReasoningTool)}", LogLevel.Error);
            }
            if (toolbox.TryGetMethodInfo(nameof(LameTools.ChainOfThoughtTool), out MethodInfo cotToolMethod))
            {
                hasTool = true;
                AddTool(nameof(LameTools.ChainOfThoughtTool), new ToolBinding(cotToolMethod));
            }
            else
            {
                if (_onBroadcast != null)
                    _onBroadcast(Id, $"{nameof(PhilosopherAgent)} >> {broadcastId()} >> No reasoning tool loaded: {nameof(LameTools.ChainOfThoughtTool)}", LogLevel.Error);
            }

            if (!hasTool) throw new ArgumentNullException($"{nameof(PhilosopherAgent)} >> No reasoning tools found in toolbox. At least one reasoning tool required for this agent");
        }

        protected override string selfInstruction() => "# IMPORTANT: ALWAYS use the ReasoningTool to think about the user query and its resolution, then use the ChainOfThoughtTool to generate a thinking plan that will help you to solve the user query";
    }
}
