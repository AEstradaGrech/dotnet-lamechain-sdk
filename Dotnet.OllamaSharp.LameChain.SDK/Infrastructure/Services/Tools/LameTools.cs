

using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Extensions;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using Chain = Dotnet.OllamaSharp.LameChain.SDK.Extensions.LameChain;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools
{
    public class LameTools : ToolsService<LameTools>
    {
        public LameTools() : base() { }
        public LameTools(IServiceProvider serviceProvider) : base(serviceProvider) { }

        // Add this tool to any agent or command
        [Description("Solves a task by writing and running a Python script by itself. You do NOT write any code: describe in plain language WHAT has to be done and this tool generates the Python code, runs it and returns the result. Use it for calculations, statistics, data processing (Excel, CSV...) and charts.")]
        public async Task<string> PythonExecutorTool([Description("Definition of the task to be done based on the user request. NEVER Python code (this will be used to generate the python code to execute")]string goal)
        {
            var factory = _services.GetRequiredService<IPromptCommandsFactory>();

            var result = await Chain.StartWith(
                new StepSettings(
                        factory.GetPythonGeneratorCommand(),
                        new PromptCommandRequest(message: goal)
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

        // Use the python executor to write files from the specified container path to the host machine
        public async Task<string> DockerFileExtractor(string filePath, string fileName)
        {
            var containerName = Environment.GetEnvironmentVariable("CONTAINER_NAME");

            if (string.IsNullOrEmpty(containerName)) return "No container name has been found in the ENVIRONMENT variables, unable to transfer files";
            /*
             import docker
client = docker.from_env()
container = client.containers.get(containerName)
bits, stat = container.get_archive($"{filePath}/{fileName}")
             */
            return "WIP";
        }
    }
}
