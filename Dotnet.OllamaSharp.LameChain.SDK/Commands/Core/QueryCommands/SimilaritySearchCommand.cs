using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Requests;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;
using DotnetLlamaSharp.Domain.Services.Embeddings;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.QueryCommands
{
    public class SimilaritySearchCommand : DbPromptCommand<List<SimilarSearchResult>>
    {
        protected readonly IEmbeddingsService _generator;

        protected Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<SimilarSearchResult>>> _queryFunction;
        public SimilaritySearchCommand(IEmbeddingsService generator, Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<SimilarSearchResult>>> queryFunction) 
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(IEmbeddingsService));

            _queryFunction = queryFunction; 
        }

        //It makes no use of the inference service (by default. You could combine a system-message + llm proces with the search results), it is added for framework support
        public SimilaritySearchCommand(IOllamaInferenceService ollama, IEmbeddingsService generator, Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<SimilarSearchResult>>> queryFunction) : base (ollama) 
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(IEmbeddingsService));

            _queryFunction = queryFunction; 
        }

        public override async Task<List<SimilarSearchResult>> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<SimilaritySearchRequest>(request);

            if (_queryFunction == null) return [];

            if (string.IsNullOrEmpty(request.Prompt)) return [];

            var castedReq = (SimilaritySearchRequest)request;

            var queryEmbeddings = await _generator.GenerateEmbeddings(request.Prompt, castedReq.Dimensions, castedReq.Model);

            return queryEmbeddings.GeneratedEmbeddings.Count == 0 ? [] :
                await _queryFunction(castedReq.QueryIndex, queryEmbeddings.GeneratedEmbeddings.First().Vector, castedReq.ReturnedResults, castedReq.Filters);
        }
    }
}
