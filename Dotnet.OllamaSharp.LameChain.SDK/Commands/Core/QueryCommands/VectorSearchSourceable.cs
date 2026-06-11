using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Model;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Embeddings;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.QueryCommands
{
    public class VectorSearchSourceable : SourceableCommand
    {
        protected readonly IEmbeddingsService _generator;

        protected Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> _queryFunction;

        public VectorSearchSourceable(IEmbeddingsService generator, Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction) : base() 
        {
            _generator = generator;
            _queryFunction = queryFunction;
        }

        // Use ollama to process the vector search before or after the query function execution
        public VectorSearchSourceable(IOllamaInferenceService ollama, IEmbeddingsService generator, Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction, 
            string? llamaGuidance = null, CommandSettings? settings = null) : base(ollama, llamaGuidance, settings)
        {
            _generator = generator;
            _queryFunction = queryFunction;
        }

        //Use ollama to process the results AND use a DB message as SystemInstruction
        public VectorSearchSourceable(IOllamaInferenceService ollama, IEmbeddingsService generator, Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction,
            string messageSourceName, string messageName, Func<string, string, Task<string>> retriever, string? guidanceMessage = null, CommandSettings? settings = null) 
            : base(ollama, messageSourceName, messageName, retriever, guidanceMessage, settings)
        {
            _generator = generator;
            _queryFunction = queryFunction;
        }

        public override async Task<List<string>> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<VectorSearchRequest>(request);

            var searchCommand = new VectorSearchCommand(_generator, _queryFunction);
            // It is assumed that the guidance message of any VectorsearchSourceable is an augment or nothing
            if (!string.IsNullOrEmpty(request.GuidanceMessage))
                request.Prompt = $"{request.Prompt}\n{request.GuidanceMessage}";

            var results = await searchCommand.Prompt(request);

            return results.Count == 0 ? [] : results.Select(result => result.Text).ToList();
        }
    }
}
