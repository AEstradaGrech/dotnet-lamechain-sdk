using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.AtomicValues;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Core.AtomicValues
{
    public class MultiChoiceCommand : DbPromptCommand<List<string>>
    {
        public MultiChoiceCommand() { }
        public MultiChoiceCommand(IOllamaInferenceService ollama, string? guidanceMessage = null, CommandSettings? settings = null) : base(ollama, guidanceMessage, settings) { }
        public MultiChoiceCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, messageSourceName, messageName, retrieverLambda, guidanceMessage, settings) { }
        public override async Task<List<string>> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<MultiChoiceRequest>(request);

            var multiChoiceReq = (MultiChoiceRequest) request;

            if (multiChoiceReq.MaxSelections.HasValue && multiChoiceReq.MaxSelections <= 0)
                multiChoiceReq.MaxSelections = 1;

            if (!multiChoiceReq.Choices.Any())
                throw new InvalidDataException($"{nameof(MultiChoiceCommand)} >> The requested number of selected choices is greater or equal to the available choices");

            var systemMessage = await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend);

            var sb = new StringBuilder();

            foreach (var choice in multiChoiceReq.Choices)
                sb.AppendLine(choice);

            var chatReq = new ChatCommandRequest(request.Prompt,[], includeSystem: true, model: request.Model ?? _settings.Model);

            var response = await _ollama.CommandPrompt<MultiChoiceResponse>(
                chatReq.ToOllamaChat(
                    systemUpdate: systemMessage.Replace("<<LIMIT_RULE>>", getChoiceLimitRule(multiChoiceReq.MaxSelections)).Replace("<<CHOICES>>", sb.ToString()), 
                    _settings),
                _settings == null ? null : validatorFor<MultiChoiceResponse>(_settings.CommandValidations, _settings.ValidationType), 
                request.Provider, 
                GetRequestTools(request));

            return response.Selected;
        }
        protected override string getDefaultInstruction()
            => @"You are a selection engine. Read the user's message and select, from the CHOICES list, every choice that satisfies it.

# CHOICES
<<CHOICES>>

# RULES
- <<LIMIT_RULE>>
- Judge by meaning, not by shared words: a choice is not relevant just because it contains a word from the request.
- If no choice satisfies the request, return an empty selection.
- Copy the selected values exactly as written in CHOICES.
";

        private string getChoiceLimitRule(int? limit)
                => limit.HasValue ? $"Select at most {limit} choices; if more qualify, keep the {limit} most relevant." : "Select ALL the choices that clearly satisfy the request, and only those.";
    }
}
