using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Evaluators
{
    public class ReasonedBoolCommand : DbPromptCommand<ReasonedBoolResponse>
    {
        public ReasonedBoolCommand() : base() { }
        public ReasonedBoolCommand(IOllamaInferenceService ollama, string? guidanceMessage = null, CommandSettings? settings = null) : base(ollama, guidanceMessage, settings) { }

        public ReasonedBoolCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retriever, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, messageSourceName, messageName, retriever, guidanceMessage, settings) { }

        public override async Task<ReasonedBoolResponse> Prompt(PromptCommandRequest request)
            => await _ollama.CommandPrompt<ReasonedBoolResponse>(request.ToOllamaChat(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings), _settings.CommandValidations, _settings.ValidationType, validatorFor<ReasonedBoolResponse>());

        protected override string getDefaultInstruction()
            => @"Analyze the user request and reason a coherent response that can be synthetized in a boolean response to indicate 'YES' or 'NO' according to the given instruction and your provided JSON schema. 
Add also a brief yet accurate 'justification' comment explaining your answer.

# IMPORTANT: your boolean answer MUST be consistent with the content of your justification comment. ENSURE that your boolean answer IS NOT CONTRADICTORY in relation with your justification comment.";
    }
}
