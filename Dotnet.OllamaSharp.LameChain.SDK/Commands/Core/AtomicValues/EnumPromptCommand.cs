using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Core.AtomicValues
{
    public class EnumPromptCommand<TEnum> : DbPromptCommand<TEnum> where TEnum : struct, Enum
    {
        public EnumPromptCommand() : base() { }
        public EnumPromptCommand(IOllamaInferenceService ollama) : base(ollama) { }
        public EnumPromptCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, messageSourceName, messageName, retrieverLambda, guidanceMessage, settings) { }

        public override async Task<TEnum> Prompt(PromptCommandRequest request)
        {
            var values = Enum.GetValues<TEnum>().ToList();
            var type = Enum.GetUnderlyingType(typeof(TEnum));

            if (type != typeof(int))
                throw new InvalidOperationException($"{nameof(EnumPromptCommand<TEnum>)} >> invalid ENUM type");

            var systemMessage = await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend);

            var sb = new StringBuilder();
            foreach (var value in values)
                sb.AppendLine($"{(int)Convert.ChangeType(value, Enum.GetUnderlyingType(typeof(TEnum)))} = {value}");

            systemMessage = systemMessage.Replace("<<CHOICES>>", sb.ToString().Trim());

            var response = await _ollama.CommandPrompt<IntegerChoiceResponse>(request.ToOllamaChat(systemMessage, _settings), validatorFor<IntegerChoiceResponse>(_settings.CommandValidations, _settings.ValidationType));

            return (TEnum)Convert.ChangeType(response.Result, type);
        }

        protected override string getDefaultInstruction()
            => @"Analyze the provided list of choices and select the integer value / key that matches the best with the user request or the DEFAULT key if the user query is not related to any choice category.
Select ONLY the NUMERIC KEY of the provided Key-Value-Pair list the DEFAULT key value if there are no relevant choices for the user intent.
Output your selected choice according to the provided JSON schema.

> CHOICES:
<<CHOICES>>
";
    }
}
