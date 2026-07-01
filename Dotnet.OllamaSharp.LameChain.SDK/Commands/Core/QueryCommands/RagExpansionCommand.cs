using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.QueryCommands
{
    /// <summary>
    /// This is a sourceable command that relies on the LLM to generate the data source (in this case a naive answer to the user question without RAG data
    /// to cover more similarity points in a semantic similarity search)
    /// The command generates the sourceable data and then you must configure a feed in the RagQueryCommand yo want to expand with this sourceable
    /// </summary>
    public class RagExpansionCommand : SourceableCommand
    {
        public RagExpansionCommand() : base() { }
        public RagExpansionCommand(IOllamaInferenceService ollama, string? llamaGuidance = null, CommandSettings? settings = null) : base(ollama, llamaGuidance, settings) { }

        public RagExpansionCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retriever, string? guidanceMessage = null, CommandSettings? settings = null) 
            : base(ollama, messageSourceName, messageName, retriever, guidanceMessage, settings) { }

        public override async Task<List<string>> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<RagExpansionRequest>(request);

            var expanseReq = (RagExpansionRequest)request;

            var systemMessage = await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend);

            systemMessage = systemMessage.Replace("<<USER_PROMPT>>",request.Prompt);

            var response = new ChatMessage(ChatRole.Assistant.ToString(), string.Empty);

            var results = new List<string>();

            for (int i = 0; i < expanseReq.Results; i++)
            {
                var result = await getExpansionResponse(_ollama, request.ToOllamaChat(systemMessage, _settings), request.Provider);

                results.Add(result.Trim());

                if (expanseReq.Results > 1 && expanseReq.UsePrevAsExample && i < expanseReq.MaxExamples)
                {
                    if(i == 0)
                        systemMessage += $"\n\n# EXPECTED OUTPUT EXAMPLES:\n";
                    
                    systemMessage += $"\n\n{result}";
                }
            }

            return results;
        }

        private async Task<string> getExpansionResponse(IOllamaInferenceService ollama, ChatRequest request, string provider)
        {
            var message = await ollama.ChatPrompt(request, provider);

            return message.Content.Trim();
        }

        protected override string getDefaultInstruction() => @"Your task is to answer the user question in a brief yet accurate manner. The resulting text will be used to perform a similarity search and 
retrieve more data related to the user query so follow this steps in order to generate your response:

> STEP 1: Analyze the user query and extract the intent to get a better idea of the topic he is quering about.
> STEP 2: Extract the core topics of the user query to include them in your synthetized response.
> STEP 3: Use you conclussions from the previous steps to generate a cohesive response as brief as possible providing a general explanation of the core topics detected in the user query.

Note: in case you are provided some EXPECTED OUTPUT EXAMPLES, use them to get an idea of your how your response should look like, but avoid repetition.

# USER QUERY: <<USER_PROMPT>>
";
    }
}
