using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Core.AtomicValues
{
    public class BoolPromptCommand : DbPromptCommand<bool>
    {
        public BoolPromptCommand() : base() { }
        public BoolPromptCommand(IOllamaInferenceService ollama) : base(ollama) { }
        
        public BoolPromptCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, messageSourceName, messageName, retrieverLambda, guidanceMessage, settings) { }

        public override async Task<bool> Prompt(PromptCommandRequest request)
        {
            var response = await _ollama.CommandPrompt<BooleanResponse>(request.ToOllamaChat(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings), validatorFor<BooleanResponse>(_settings.CommandValidations, _settings.ValidationType));

            return response.Answer;
        }

        protected override string getDefaultInstruction()
            => "Analyze the user question, reason your response and output a boolean to indicate YES / NO according to the provided JSON schema.";
    }
}
