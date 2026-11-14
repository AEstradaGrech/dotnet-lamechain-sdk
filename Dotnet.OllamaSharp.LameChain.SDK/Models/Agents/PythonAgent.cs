
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Extensions;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using Chain = Dotnet.OllamaSharp.LameChain.SDK.Extensions.LameChain;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Agents
{
    public class PythonAgent : LameAgent
    {
        public PythonAgent() { }
        public PythonAgent(IServiceProvider serviceProvider, string model, PromptSettings? settings = null, string? name = null, string? description = null) 
            : base(serviceProvider, model, settings, name, description)
        {
            if(string.IsNullOrEmpty(name))
                Name = "Python Coding Agent";

            if(string.IsNullOrEmpty(description))
                Description = "An agent specialized in generating and executing Python code and providing insights about the results.";

            using var toolboxScope = _serviceProvider.CreateScope();

            var toolbox = toolboxScope.ServiceProvider.GetRequiredService<IToolbox>();

            if (toolbox.TryGetMethodInfo(nameof(LameTools.PythonExecutorTool), out MethodInfo toolMethod))
                AddTool(nameof(LameTools.PythonExecutorTool), new ToolBinding(toolMethod));

            else throw new ArgumentNullException($"{nameof(PythonAgent)} >> {nameof(LameTools.PythonExecutorTool)} >> No tool found in toolbox");
        }

        [Description("Solves a task by writing and running a Python script by itself. You do NOT write any code: describe in plain language WHAT has to be done and this tool generates the Python code, runs it and returns the result. Use it for calculations, statistics, data processing (Excel, CSV...) and charts.")]
        public async Task<string> SolveTaskWithPython(
            [Description("Plain-language description of what has to be done. NEVER Python code. Example: 'Compute the average salary rise per department'")]
            string goal,
            [Description("All the data the task needs: the values given by the user, or the absolute paths of the input files as the API sees them (e.g. /data/Resources/sales.xlsx), with sheet and column names if known. Empty if no input data is needed")]
            string? inputData = null,
            [Description("What the result must contain (values, keys, units, rounding) and the files to create, if any. Example: 'a dict with keys department and average_rise rounded to 2 decimals, and a bar chart saved as rises.png'")]
            string? expectedOutput = null)
        {
            var task = new StringBuilder().AppendLine($"Task: {goal}");

            if (!string.IsNullOrWhiteSpace(inputData))
                task.AppendLine($"Input data: {inputData}");

            if (!string.IsNullOrWhiteSpace(expectedOutput))
                task.AppendLine($"Expected output: {expectedOutput}");

            var factory = _serviceProvider.GetRequiredService<IPromptCommandsFactory>();

            var result = await Chain.StartWith(
                new StepSettings(
                        factory.GetPythonGeneratorCommand(),
                        new PromptCommandRequest(message: task.ToString())
                    ),
                    defaultSettings: new CommandSettings(),
                    chainIntent: "Generate and execute Python code")
                .ExecutePython(
                    new StepSettings(
                        factory.GetPythonExecutorCommand(),
                        new PythonCodingRequest())
                )
                .ExecuteChainAsync();

            return result.ResultAs<ChatMessage>().Content;
        }
    }
}
