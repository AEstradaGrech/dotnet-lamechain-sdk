using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using Microsoft.Extensions.DependencyInjection;


namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Agents
{
    public class SysAdminAgent : LameAgent
    {
        protected override string selfInstruction() => """
            You have access to two tools: ShellGeneratorTool and ShellExecutorTool.

            # RULES:
            - Use the ShellGeneratorTool to create a shell script that satisfies the user request.
              Pass the user request as input as it is, without modifications of any type, the tool will generate the right shell script for you.
              If the user explicitely asks for a PowerShell or Bash script, pass that as a tool parameter, otherwise leave it null.
              When generating a script you must ALWAYS RETURN: 
                1 - The 'ScriptPath' value.
                2 - The full script and the generated script parameters (if any) so the user can review it and approve it.
                3 - A brief explanation of what the script and how to use it.
                
            - Use the ShellExecutorTool to run the shell script specified in the path (the user must provide it).
              You CANNOT USE the ShellExecutorTool without the EXPLICIT APPROVAL of the user (you can't decide to use it by your own, ONLY when the user commands you to do so).
              NEVER make up or reinterpret the results of the scriptm You must inform the user of the actual script results. In case the script produces no results, just tell that to the user.
            """;

        public SysAdminAgent() { }
        public SysAdminAgent(IServiceProvider serviceProvider, string model, PromptSettings? settings = null, string? name = null, string? description = null)
            : base(serviceProvider, model, settings, name, description)
        {
            if (string.IsNullOrEmpty(name))
                Name = "System Administrator Agent";

            if (string.IsNullOrEmpty(description))
                Description = "An agent specialized in generating and executing shell scripts and providing insights about the results.";

            _useSelfInstruction = true;

            var toolbox = _serviceProvider.GetRequiredService<IToolbox>()
                ?? throw new ArgumentNullException($"{nameof(SysAdminAgent)} >> {nameof(IToolbox)} >> No toolbox registered. Cannot add agent tools");

            bool hasTools = true;

            if (toolbox.TryGetMethodInfo(nameof(LameTools.ShellGeneratorTool), out var generatorMethodInfo))
            {
                AddTool(nameof(LameTools.ShellGeneratorTool), new ToolBinding(generatorMethodInfo, target: null));
            }
            else hasTools = false;

            if (toolbox.TryGetMethodInfo(nameof(LameTools.ShellExecutorTool), out var executorMethodInfo))
            {
                AddTool(nameof(LameTools.ShellExecutorTool), new ToolBinding(executorMethodInfo, target: null));
            }
            else hasTools = false;

            if (!hasTools)
                throw new InvalidOperationException($"{nameof(SysAdminAgent)} >> Agent configuration fail >> One of the agent tools (or both) are missing");
        }
    }
}
