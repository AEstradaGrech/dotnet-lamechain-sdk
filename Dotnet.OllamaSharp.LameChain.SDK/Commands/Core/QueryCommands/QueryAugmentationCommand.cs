using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Core.TextGenerators
{
    public class QueryAugmentationCommand : SourceableCommand
    {
        public QueryAugmentationCommand() : base() { }
        public QueryAugmentationCommand(IOllamaInferenceService ollama, string? llamaGuidance = null, CommandSettings? settings = null) : base(ollama, llamaGuidance, settings) { }

        public QueryAugmentationCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retriever, string? guidanceMessage = null, CommandSettings? settings = null) 
            : base(ollama, messageSourceName, messageName, retriever, guidanceMessage, settings) { }

        public override async Task<List<string>> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<RagExpansionRequest>(request);

            var expanseReq = (RagExpansionRequest)request;

            var systemMessage = await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend);

            systemMessage = systemMessage.Replace("<<USER_PROMPT>>",request.Prompt).Replace("<<USER_QUERY>>", request.Prompt);

            var results = new List<string>();

            for (int i = 0; i < expanseReq.Results; i++)
            {
                var result = await getAugmentedQuery(_ollama, request.ToOllamaChat(systemMessage, _settings));

                results.Add(result.Trim());

                if (expanseReq.Results > 1 && expanseReq.UsePrevAsExample && i < expanseReq.MaxExamples)
                {
                    if (i == 0)
                        systemMessage += $"\n\n# EXPECTED OUTPUT EXAMPLES:";
                    
                    systemMessage += $"\n\n{result}";
                }
            }

            return results;
        }

        private async Task<string> getAugmentedQuery(IOllamaInferenceService ollama, ChatRequest request)
        {
            var message = await ollama.ChatPrompt(request);

            return message.Content.Trim();
        }

        protected override string getDefaultInstruction() => @"Your task is to generate a variant of the user query that is similar in core and concept, but using different words or mentioning different related topics
so your response can be used to cover more points in a similarity search and retrieve more relevant data from the knowledge base.

Follow this steps in order to generate your response:

> STEP 1: Analyze the user query and extract the intent to get a better idea of the topic he is quering about.
> STEP 2: Extract the core topics of the user query to include them in your synthetized response.
> STEP 3: Use you conclussions from the previous steps to generate a cohesive response emulating the original user query.

# RULES: ensure you are compliant with this rules before outputting your response:

- ENSURE your response EMULATES a user query similar to the provided user query, you MUST answer as if you where a user making a similar request.
- DO NOT chat with the user, output only your simulated user query.
- In case you are provided some EXPECTED OUTPUT EXAMPLES, use them to get an idea of your how your response should look like, but AVOID REPETITION.

# USER QUERY: <<USER_QUERY>>
";
    }
}

