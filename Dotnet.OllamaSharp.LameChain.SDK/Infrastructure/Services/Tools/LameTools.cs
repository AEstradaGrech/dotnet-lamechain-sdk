

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

        [Description("Generates and executes python code to fulfill the task requirements")]
        public async Task<string> PythonExecutorTool([Description("Definition of the python code to generate based on the user request")]string taskDefinition)
        {
            var factory = _services.GetRequiredService<IPromptCommandsFactory>();

            var result = await Chain.StartWith(
                new StepSettings(
                        factory.GetPythonGeneratorCommand(),
                        new PromptCommandRequest(message: taskDefinition)
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
