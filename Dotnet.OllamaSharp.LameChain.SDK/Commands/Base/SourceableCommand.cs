using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;

using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;


namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Base
{
    /// <summary>
    /// A sourceable is a type of command that is intended to be used as a source of string data to feed other steps from it.
    /// A Sourceable command always returns a List<string>
    /// </summary>
    public abstract class SourceableCommand : DbPromptCommand<List<string>>
    {
        public SourceableCommand() : base() {}

        public SourceableCommand(IOllamaInferenceService ollama, string? llamaGuidance = null, CommandSettings? settings = null) : base(ollama, llamaGuidance, settings) {}

        public SourceableCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retriever, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, messageSourceName, messageName, retriever, guidanceMessage, settings) { }

        protected override async Task<string> getPromptInstruction(string? additionalData = null, bool isAfterCore = true)
        {
            var instruction = await base.getPromptInstruction(additionalData, isAfterCore);

            return string.IsNullOrEmpty(instruction) ? $">> SOURCING >> {nameof(SourceableCommand)}" : instruction;
        }
    }
}
