using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Evaluators
{
    public class ScoredBoolCommand : DbPromptCommand<ScoredBoolResponse>
    {
        public ScoredBoolCommand() : base() { }
        public ScoredBoolCommand(IOllamaInferenceService ollama, string? guidanceMessage = null, CommandSettings? settings = null) : base(ollama, guidanceMessage, settings) { }

        public ScoredBoolCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retriever, string? guidanceMessage = null, CommandSettings? settings = null) 
            : base(ollama, messageSourceName, messageName, retriever, guidanceMessage, settings) { }

        public override async Task<ScoredBoolResponse> Prompt(PromptCommandRequest request)
            => await _ollama.CommandPrompt<ScoredBoolResponse>(request.ToOllamaChat(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend)), _settings.CommandValidations, _settings.ValidationType, validatorFor<ScoredBoolResponse>());

        protected override string getDefaultInstruction()
            => @"Analyze the user request and reason a coherent response that can be sythetized in a boolean response to indicate 'YES' or 'NO' according to the provided JSON schema. 
Also, add a 'confidence' score to your response to indicate how sure you are about your answer, and a 'justification' comment.";
    }
}
