using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Model;
using DotnetLlamaSharp.Domain.Services.Embeddings;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.QueryCommands
{
    public class VectorSearchCommand : DbPromptCommand<List<ILameSearchResult>>
    {
        protected readonly IEmbeddingsService _generator;

        protected Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> _queryFunction;
        public VectorSearchCommand(IEmbeddingsService generator, Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction) 
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(IEmbeddingsService));

            _queryFunction = queryFunction; 
        }

        //It makes no use of the inference service (by default. You could combine a system-message + llm proces with the search results), it is added for framework support
        public VectorSearchCommand(IOllamaInferenceService ollama, IEmbeddingsService generator, Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction) : base (ollama) 
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(IEmbeddingsService));

            _queryFunction = queryFunction; 
        }

        public override async Task<List<ILameSearchResult>> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<VectorSearchRequest>(request);

            if (_queryFunction == null) return [];

            if (string.IsNullOrEmpty(request.Prompt)) return [];

            var castedReq = (VectorSearchRequest)request;

            var queryEmbeddings = await _generator.GenerateEmbeddings(request.Prompt, castedReq.Dimensions, castedReq.Model ?? "nomic-embed-text");

            return queryEmbeddings.GeneratedEmbeddings.Count == 0 ? [] :
                await _queryFunction(castedReq.QueryIndex, queryEmbeddings.GeneratedEmbeddings.First().Vector, castedReq.ReturnedResults, castedReq.Filters);
        }
    }
}
