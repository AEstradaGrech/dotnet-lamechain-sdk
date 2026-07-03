using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Evaluators
{
    public class ScoredResponseCommand : DbPromptCommand<ScoredResponse>
    {
        public ScoredResponseCommand() : base() { }
        public ScoredResponseCommand(IOllamaInferenceService ollama, string? guidanceMessage = null, CommandSettings? settings = null) : base(ollama, guidanceMessage, settings) { }

        public ScoredResponseCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retriever, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, messageSourceName, messageName, retriever, guidanceMessage, settings) { }

        public override async Task<ScoredResponse> Prompt(PromptCommandRequest request)
            => await _ollama.CommandPrompt<ScoredResponse>(request.ToOllamaChat(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings), _settings == null ? null : validatorFor<ScoredResponse>(_settings.CommandValidations, _settings.ValidationType), provider: request.Provider, requestTools: GetRequestTools(request));

        protected override string getDefaultInstruction()
            => @"Analyze the user request and reason a coherent score for the given input that represents the best a response that ranges from 0.0 to 1.0 to indicate 'NEGATIVE' or 'POSITIVE' according to the provided JSON schema. Add also a brief yet accurate 'justification' comment explaining your score";
    }
}
