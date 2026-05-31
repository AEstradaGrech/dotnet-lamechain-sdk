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

            if (multiChoiceReq.MaxSelections <= 0)
                multiChoiceReq.MaxSelections = 1;

            if (!multiChoiceReq.Choices.Any())
                throw new InvalidDataException($"{nameof(MultiChoiceCommand)} >> The requested number of selected choices is greater or equal to the available choices");

            //var promptReq = await getGenerateRequest(multiChoiceReq);
            var systemMessage = await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend);

            var sb = new StringBuilder();

            foreach (var choice in multiChoiceReq.Choices)
                sb.AppendLine(choice);

            var chatReq = new ChatCommandRequest(includeSystem: true, [], request.Prompt, request.Model ?? _settings.Model);

            var response = await _ollama.CommandPrompt<MultiChoiceResponse>(
                chatReq.ToOllamaChat(
                    systemUpdate: systemMessage.Replace("<<MAX_SEL>>", $"{multiChoiceReq.MaxSelections}").Replace("<<CHOICES>>", sb.ToString()), 
                    _settings),
                _settings.CommandValidations,
                _settings.ValidationType,
                validatorFor<MultiChoiceResponse>());

            return response.Selected;
        }
        protected override string getDefaultInstruction()
            => @"Your task is to analyze the provided list of choices and select the choices that match exactly with the request intent (if any) WITHOUT exceeding the MAXIMUM SELECTION ALLOWED of <<MAX_SEL>> choices.
To do that, follow this steps:

> STEP 1: Analyze the query and extract the intent tho get a clear idea of what you should decide on.
> STEP 2: Analyze the provided list of choices and reason which of them might be selected according to the intent.
> STEP 3: Review carefully the MAXIMUM number of requested selections to know exactly the upper limit in case there are many suitable options.
> STEP 4: Return a list that contains from 0 to the MAXIMUM allowed choices based on your analysis.

# AVAILABLE CHOICES:

<<CHOICES>>

# MAXIMUM SELECTIONS ALLOWED: 0 - <<MAX_SEL>>

# IMPORTANT: follow this rules in order to generate your final response:

- ENSURE that you return a list with an amount of values ranging from 0 to <<MAX_SEL>> depending on the availability of suitable choices in the provided list.
- ENSURE that you return an empty list IF there are no choices in the provided list matching the analyzed request intent.
- ENSURE that you ONLY return choices that are present in the list. DO NOT return items that are NOT PRESENT in the provided choices list.
- ENSURE that you NEVER RETURN MORE THAN <<MAX_SEL>> choices.
- ENSURE that you return ONLY choices that match exactly the request intent or an empty list if there are none.
";
    }
}
