using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.AtomicValues;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Core.AtomicValues
{
    public class StringChoiceCommand : DbPromptCommand<string>
    {
        public StringChoiceCommand() : base() { }
        public StringChoiceCommand(IOllamaInferenceService ollama) : base(ollama) { }
        public StringChoiceCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, messageSourceName, messageName, retrieverLambda, guidanceMessage, settings) { }

        public override async Task<string> Prompt(PromptCommandRequest request)
        {
            var systemMessage = await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend);

            validateInputRequest<StringChoiceRequest>(request);

            var choices = string.Empty;

            var sb = new StringBuilder();
            foreach (var choice in ((StringChoiceRequest)request).Choices)
                sb.AppendLine($"- {choice}");

            systemMessage = systemMessage.Replace("<<CHOICES>>", sb.ToString());

            var response = await _ollama.CommandPrompt<StringChoiceResponse>(request.ToOllamaChat(systemMessage, _settings), _settings.CommandValidations, _settings.ValidationType, validatorFor<StringChoiceResponse>());

            return response.Selected;
        }

        protected override string getDefaultInstruction()
            => "Analyze the provided list of choices and select the value that matches the best with the user request. Output your selected choice according to the provided JSON schema.\n> CHOICES:\n<<CHOICES>>";
    }
}
