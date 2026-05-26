using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;

using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;


namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Base
{
    // EL OBJETIVO ES GENERAR UNA FUENTE DE DATOS PARA LLMs
    // PRODUCE SIEMPRE UNA LISTA DE STRINGS
    // PUEDEN PRODUCIRSE POR INFERENCIA A LLM (RagExpansionCommand | QueryAugmentCommand) O NO (SimilaritySearch, WebSearch, ReadSummariesFromADirectory... COMANDOS QUE NO USAN LLM PERO SON UTILES O NECESARIOS PARA OTRO LLM-COMMAND)
    public abstract class SourceableCommand : DbPromptCommand<List<string>>
    {
        public SourceableCommand() : base() {}

        public SourceableCommand(IOllamaInferenceService ollama, string? llamaGuidance = null, CommandSettings? settings = null) : base(ollama, llamaGuidance, settings) {}

        public SourceableCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retriever, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, messageSourceName, messageName, retriever, guidanceMessage, settings) { }
    }
}
