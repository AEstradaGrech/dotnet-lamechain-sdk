using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.AtomicValues;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.AtomicValues;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Model;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Embeddings;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.QueryCommands
{
    public class SmartQuerySourceable : VectorSearchSourceable
    {
        // Use MultiChoice & SearchCommand WITH NO DB
        public SmartQuerySourceable(IOllamaInferenceService ollama, IEmbeddingsService generator, Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction, string? llamaGuidance = null, CommandSettings? settings = null) 
            : base(ollama, generator, queryFunction, llamaGuidance, settings) {}

        public SmartQuerySourceable(IOllamaInferenceService ollama, IEmbeddingsService generator, Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction,
            string messageSourceName, string messageName, Func<string, string, Task<string>> retriever, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, generator, queryFunction)
        {
        }
        //TODO:
        //public SmartQueryCommand(IOllamaInferenceService ollama, IEmbeddingsService generator,
        //  DbQuerySettings{
        //      Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<SimilarSearchResult>>> QueryFunction,
        //      Func<string, string, Task<string> Retriever,
        //      
        //  })
        //    : base(ollama, generator, settings.QueryFunction, settings.Retriever)
        //{
        //}

        public override async Task<List<string>> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<SmartQueryRequest>(request);

            var castedReq = (SmartQueryRequest)request;

            var choiceCommand = new MultiChoiceCommand(_ollama, await getPromptInstruction(), _settings);

            var selectedChoices = await choiceCommand.Prompt(new MultiChoiceRequest(castedReq.MaxReturnedChoices, castedReq.CollectionChoices, request.Prompt, guidance: request.GuidanceMessage));

            if (selectedChoices.Count == 0) return [];

            var finalResults = new List<string>();
            foreach (var choice in selectedChoices)
            {
                var queryCommand = new VectorSearchSourceable(_generator, _queryFunction);

                var results = await queryCommand.Prompt(new VectorSearchRequest(choice, request.Prompt, castedReq.Model, castedReq.Dimensions, castedReq.MaxReturnedChoices, castedReq.Filters));

                if(results.Count > 0)
                    finalResults.AddRange(results);
            }
            
            // return List<string>{
            //  colA,
            //  colB.results,
            //  etc
            // }
            return finalResults;
        }
    }
}
