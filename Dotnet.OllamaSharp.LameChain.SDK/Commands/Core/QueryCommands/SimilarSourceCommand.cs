using Dotnet.OllamaSharp.LameChain.SDK.Command.Requests;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Embeddings;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.QueryCommands
{
    public class SimilarSourceCommand : SourceableCommand
    {
        protected readonly IEmbeddingsService _generator;

        protected Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<SimilarSearchResult>>> _queryFunction;

        public SimilarSourceCommand(IEmbeddingsService generator, Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<SimilarSearchResult>>> queryFunction) : base() 
        {
            _generator = generator;
            _queryFunction = queryFunction;
        }

        public SimilarSourceCommand(IOllamaInferenceService ollama, IEmbeddingsService generator, Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<SimilarSearchResult>>> queryFunction) : base(ollama)
        {
            _generator = generator;
            _queryFunction = queryFunction;
        }

        public SimilarSourceCommand(IOllamaInferenceService ollama, IEmbeddingsService generator, Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<SimilarSearchResult>>> queryFunction,
            string messageSourceName, string messageName, Func<string, string, Task<string>> retriever, string? guidanceMessage = null, CommandSettings? settings = null) 
            : base(ollama, messageSourceName, messageName, retriever, guidanceMessage, settings)
        {
            _generator = generator;
            _queryFunction = queryFunction;
        }

        public override async Task<List<string>> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<SimilaritySearchRequest>(request);

            var searchCommand = new SimilaritySearchCommand(_generator, _queryFunction);

            var results = await searchCommand.Prompt(request);

            return results.Count == 0 ? [] : results.Select(result => result.Text).ToList();
        }
    }
}
