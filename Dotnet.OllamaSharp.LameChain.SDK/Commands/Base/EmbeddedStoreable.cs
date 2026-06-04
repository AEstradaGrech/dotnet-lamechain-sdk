using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Embeddings;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Base
{
    public class EmbeddedStoreable<TStored> : StoreableCommand<TStored> where TStored : class
    {
        private readonly IEmbeddingsService _embeddingsService;
        public EmbeddedStoreable() : base() { }

        public EmbeddedStoreable(IEmbeddingsService embedder, Func<TStored, string, Task<TStored>> storingLambda) : base(storingLambda)
        {
            _embeddingsService = embedder;
        }

        public EmbeddedStoreable(IOllamaInferenceService ollama, IEmbeddingsService embedder, Func<TStored, string, Task<TStored>> storingLambda, string? systemMessage = null, CommandSettings? settings = null) 
            : base(ollama, storingLambda, systemMessage, settings)
        {
            _embeddingsService = embedder;
        }

        public EmbeddedStoreable(IOllamaInferenceService ollama, IEmbeddingsService embedder, Func<TStored, string, Task<TStored>> storingLambda, string dbMessageSource, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null) 
            : base(ollama, storingLambda, dbMessageSource, messageName, retrieverLambda, guidanceMessage, settings)
        {
            _embeddingsService = embedder;
        }

        public override async Task<TStored> Prompt(PromptCommandRequest request)
        {
            // embedd request data & store

            return await base.Prompt(request);
        }
    }
}
