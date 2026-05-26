using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutput;
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

            if (multiChoiceReq.MaxSelections <= 0)
                multiChoiceReq.MaxSelections = 1;

            if (!multiChoiceReq.Choices.Any())
                throw new InvalidDataException($"{nameof(MultiChoiceCommand)} >> The requested number of selected choices is greater or equal to the available choices");

            var promptReq = await getGenerateRequest(multiChoiceReq);

            var sb = new StringBuilder();

            foreach (var choice in multiChoiceReq.Choices)
                sb.AppendLine(choice);

            promptReq.System = promptReq.System.Replace("<<MAX_SEL>>", $"{multiChoiceReq.MaxSelections}").Replace("<<CHOICES>>", sb.ToString());

            var response = await _ollama.CommandPrompt<MultiChoiceResponse>(promptReq, _settings.CommandValidations, _settings.ValidationType, validatorFor<MultiChoiceResponse>());

            return response.Selected;
        }

        protected override string getDefaultInstruction()
            => @"Analyze the provided list of choices and select up to (but not necessarily) <<MAX_SEL>> options that matches the best with the user request, or an empty list if the user intent is unrelated to 
any available choice. Output a list of strings containing your selected values (if any) according to the provided JSON schema.

> CHOICES:

<<CHOICES>>

#IMPORTANT: review carefully any provided information about the available choices to ensure that you select the most suitable option for the given user query.
";
    }
}
