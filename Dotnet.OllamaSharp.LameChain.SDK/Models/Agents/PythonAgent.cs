
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Extensions;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Text;
using Chain = Dotnet.OllamaSharp.LameChain.SDK.Extensions.LameChain;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Agents
{
    public class PythonAgent : LameAgent
    {
        // Settings for the python coding chain
        private CommandSettings? _toolChainSettings = null;

        protected override string selfInstruction() => """
            You solve the user's requests that need calculations, statistics, data processing (Excel, CSV...) or charts with the SolveTaskWithPython tool.
            You NEVER write Python code and NEVER compute the results yourself: call the tool with the task in plain language and ALL the data the user gave you in inputData, copied verbatim.
            Report the values exactly as the tool returns them in its EXACT RESULT section, without recomputing, rounding or mixing them with your own calculations, and include the path of every file the tool created.
            """;
        public PythonAgent() { }
        public PythonAgent(IServiceProvider serviceProvider, string model, PromptSettings? settings = null, string? name = null, string? description = null) 
            : base(serviceProvider, model, settings, name, description)
        {
            if(string.IsNullOrEmpty(name))
                Name = "Python Coding Agent";

            if(string.IsNullOrEmpty(description))
                Description = "An agent specialized in generating and executing Python code and providing insights about the results.";

            _useSelfInstruction = true;

            AddTool(nameof(SolveTaskWithPython), new ToolBinding(GetType().GetMethod(nameof(SolveTaskWithPython)), target: this));
        }

        [Description("Solves a task by writing and running a Python script by itself. You do NOT write any code: describe in plain language WHAT has to be done and this tool generates the Python code, runs it and returns the result. Use it for calculations, statistics, data processing (Excel, CSV...) and charts.")]
        public async Task<string> SolveTaskWithPython(
            [Description("Plain-language description of what has to be done. NEVER Python code. Example: 'Compute the average salary rise per department'")]
            string goal,
            [Description("ALL the data the task needs, copied verbatim from the user's messages: every value, list and table, or the absolute paths of the input files as the API sees them (e.g. /data/Resources/sales.xlsx), with sheet and column names if known. Do not summarize or round it. Empty only if the task needs no input data")]
            string? inputData = null,
            [Description("What the result must contain (values, keys, units, rounding) and the files to create, if any. Example: 'a dict with keys department and average_rise rounded to 2 decimals, and a bar chart saved as rises.png'")]
            string? expectedOutput = null)
        {
            var factory = _serviceProvider.GetRequiredService<IPromptCommandsFactory>();

            var result = await Chain.StartWith(
                new StepSettings(
                        factory.GetPythonGeneratorCommand(),
                        new PromptCommandRequest(message: BuildPythonTask(goal, inputData, expectedOutput, UserInput))
                    ),
                    defaultSettings: _toolChainSettings ?? defaultToolSettings(),
                    chainIntent: "Generate and execute Python code")
                .ExecutePython(
                    new StepSettings(
                        factory.GetPythonExecutorCommand(),
                        new PythonCodingRequest())
                )
                .ExecuteChainAsync();

            return result.ResultAs<ChatMessage>().Content;
        }

        /// <summary>
        /// Task for the Python generator. The tool arguments are written by the LLM, which may summarize or drop the data the user gave
        /// (then the generator invents sample data or reads it from somewhere that does not exist). The original user request is
        /// always passed too, so the generator gets the data exactly as the user wrote it whatever the LLM put in the arguments.
        /// </summary>
        internal static string BuildPythonTask(string goal, string? inputData, string? expectedOutput, string? userRequest)
        {
            var task = new StringBuilder().AppendLine($"Task: {goal}");

            if (!string.IsNullOrWhiteSpace(inputData))
                task.AppendLine($"Input data: {inputData}");

            if (!string.IsNullOrWhiteSpace(expectedOutput))
                task.AppendLine($"Expected output: {expectedOutput}");

            if (!string.IsNullOrWhiteSpace(userRequest))
                task.AppendLine()
                    .AppendLine("Original user request (if the task needs data, take it from here exactly as written):")
                    .AppendLine(userRequest);

            return task.ToString();
        }

        public LameAgent UseToolSettings(CommandSettings settings)
        {
            _toolChainSettings = settings;

            return this;
        }

        private CommandSettings defaultToolSettings()
            => new CommandSettings
            {
                Model = "gpt-oss:20b",
                ContextLength = 10000,
                MaxTokens = 4000,
                Temperature = 0.3f,
                TopP = 0.6f,
                TopK = 20,
                RepeatPenalty = 1.1f,
                RepeatLastN = 2147483647
            };
    }
}
