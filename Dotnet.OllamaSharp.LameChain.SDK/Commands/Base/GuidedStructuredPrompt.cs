using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Bases
{
    /// <summary>
    /// Atomic Value Commands returns simple values (c# structs) mapped from a structured class json (return type IS NOT a class)
    /// Simplest form of structured output command (ensures return type IS a class, so the same structured class json is returned as it is). 
    /// Use with / without system guidance message, returns StructuredOutput response.
    /// HAS NOT Chroma dependancy
    /// </summary>
    public class GuidedStructuredPrompt<TJson> : BasePromptCommand<TJson> where TJson : class
    {
        public GuidedStructuredPrompt() : base() { }
        public GuidedStructuredPrompt(IOllamaInferenceService ollama) : base(ollama) { }
        public GuidedStructuredPrompt(IOllamaInferenceService ollama, string? systemMessage = null, CommandSettings? settings = null) : base(ollama, systemMessage, settings){ }
        public override async Task<TJson> Prompt(PromptCommandRequest request)
            => await _ollama.CommandPrompt<TJson>(request.ToOllamaChat(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings), _settings == null ? null : validatorFor<TJson>(_settings.CommandValidations, _settings.ValidationType), request.Provider, request.HasTools ? request.Tools : null);
    }
}
