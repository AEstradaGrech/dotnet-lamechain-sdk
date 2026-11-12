
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;

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

            AddTool(nameof(LameTools.PythonExecutorTool), new ToolBinding(GetType().GetMethod(nameof(LameTools.PythonExecutorTool)), target: this));
        }
    }
}
