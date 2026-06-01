using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;


namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Evaluators
{
    public class ReasonedScoreCommand : DbPromptCommand<ReasonedScoreResponse>
    {
        public ReasonedScoreCommand() : base() { }
        public ReasonedScoreCommand(IOllamaInferenceService ollama, string? guidanceMessage = null, CommandSettings? settings = null) : base(ollama, guidanceMessage, settings) { }

        public ReasonedScoreCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retriever, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, messageSourceName, messageName, retriever, guidanceMessage, settings) { }

        public override async Task<ReasonedScoreResponse> Prompt(PromptCommandRequest request)
            => await _ollama.CommandPrompt<ReasonedScoreResponse>(request.ToOllamaChat(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings), _settings.CommandValidations, _settings.ValidationType, validatorFor<ReasonedScoreResponse>());

        protected override string getDefaultInstruction()
            => @"Analyze the user request and reason a coherent score for the given input that represents the best a response that ranges from 0.0 to 1.0 to indicate 'NEGATIVE' or 'POSITIVE' according to the provided JSON schema. Add also a brief yet accurate 'justification' comment explaining your score";
    }
}
