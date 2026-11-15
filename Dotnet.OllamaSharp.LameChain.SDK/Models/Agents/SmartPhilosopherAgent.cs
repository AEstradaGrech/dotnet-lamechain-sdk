
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Agents
{
    /// <summary>
    /// A version of the PhilosopherAgent that makes use of the Advanced version of the reasoning tools
    /// Use this one with bigger / smarter models
    /// </summary>
    public class SmartPhilosopherAgent : PhilosopherAgent
    {
        public SmartPhilosopherAgent(IServiceProvider serviceProvider, string model, PromptSettings? settings = null, string? name = null, string? description = null)
            : base(serviceProvider, model, settings, name, description)
        {
            if (string.IsNullOrEmpty(name))
                Name = "Smart Philosopher / Reasoning Agent";

            if (string.IsNullOrEmpty(description))
                Description = "An agent specialized in thinking and reasoning.";

            _useSelfInstruction = true;

            using var toolboxScope = _serviceProvider.CreateScope();

            var toolbox = toolboxScope.ServiceProvider.GetRequiredService<IToolbox>();

            bool hasTool = false;
            if (toolbox.TryGetMethodInfo(nameof(LameTools.AdvancedReasoningTool), out MethodInfo reasoningToolMethod))
            {
                hasTool = true;
                AddTool(nameof(LameTools.AdvancedReasoningTool), new ToolBinding(reasoningToolMethod));
            }
            else
            {
                if (_onBroadcast != null)
                    _onBroadcast(Id, $"{nameof(SmartPhilosopherAgent)} >> {broadcastId()} >> No reasoning tool loaded: {nameof(LameTools.AdvancedReasoningTool)}", LogLevel.Error);
            }
            if (toolbox.TryGetMethodInfo(nameof(LameTools.AdvancedChainOfThoughtTool), out MethodInfo cotToolMethod))
            {
                hasTool = true;
                AddTool(nameof(LameTools.AdvancedChainOfThoughtTool), new ToolBinding(cotToolMethod));
            }
            else
            {
                if (_onBroadcast != null)
                    _onBroadcast(Id, $"{nameof(SmartPhilosopherAgent)} >> {broadcastId()} >> No reasoning tool loaded: {nameof(LameTools.AdvancedChainOfThoughtTool)}", LogLevel.Error);
            }

            if (!hasTool) throw new ArgumentNullException($"{nameof(SmartPhilosopherAgent)} >> No reasoning tools found in toolbox. At least one reasoning tool required for this agent");
        }

        protected override string selfInstruction() => "# IMPORTANT: ALWAYS use the AdvancedReasoningTool to think about the user query and its resolution, then use the AdvancedChainOfThoughtTool to generate a thinking plan that will help you to solve the user query";
    }
}
