
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Agents
{
    public class PythonAgent : LameAgent
    {
        public PythonAgent() { }
        public PythonAgent(IServiceProvider serviceProvider, string model, PromptSettings? settings = null, string? name = null, string? description = null) 
            : base(serviceProvider, model, settings, name, description)
        {
            if(string.IsNullOrEmpty(name))
                name = "Python Coding Agent";

            if(string.IsNullOrEmpty(description))
                description = "An agent specialized in generating and executing Python code and providing insights about the results.";
            
            using var scope = _serviceProvider.CreateScope();

            var lameTools = scope.ServiceProvider.GetRequiredService<IToolsService<LameTools>>();

            AddTool(nameof(LameTools.PythonExecutorTool), new ToolBinding(lameTools.GetType().GetMethod(nameof(LameTools.PythonExecutorTool))));
        }
    }
}
