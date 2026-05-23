using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Requests;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutput;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;


namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Core.AtomicValues
{
    public class BoolPromptCommand : DbPromptCommand<bool>
    {
        public BoolPromptCommand() : base() { }
        public BoolPromptCommand(IOllamaInferenceService ollama) : base(ollama) { }
        // Values from factory injected services
        public BoolPromptCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, messageSourceName, messageName, retrieverLambda, guidanceMessage, settings) { }

        public override async Task<bool> Prompt(PromptCommandRequest request)
        {
            //TODO: --> ValidateWith(sourceName, validatorMsgName) --> DB validator support >> validatorFor<BooleanResponse>(sourceName, validatorMsgName, _retrieverLambda)
            var response = await _ollama.CommandPrompt<BooleanResponse>(await getGenerateRequest(request), _settings.CommandValidations, _settings.ValidationType, validatorFor<BooleanResponse>());

            return response.Answer;
        }

        protected override string getDefaultInstruction()
            => "Analyze the user question, reason your response and output a boolean to indicate YES / NO according to the provided JSON schema.";
    }
}
