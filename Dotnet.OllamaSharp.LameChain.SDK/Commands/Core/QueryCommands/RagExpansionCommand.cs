using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Requests;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Core.TextGenerators
{
    public class RagExpansionCommand : DbPromptCommand<ChatMessage>
    {
        public RagExpansionCommand() : base() { }
        public RagExpansionCommand(IOllamaInferenceService ollama) : base(ollama) { }

        public RagExpansionCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retriever, string? guidanceMessage = null, CommandSettings? settings = null) 
            : base(ollama, messageSourceName, messageName, retriever, guidanceMessage, settings) { }

        public override async Task<ChatMessage> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<RagExpansionRequest>(request);

            var expanseReq = (RagExpansionRequest)request;

            var promptReq = await getGenerateRequest(request);

            promptReq.System += $"\n{request.Prompt}";

            var response = new ChatMessage(ChatRole.Assistant.ToString(), string.Empty);

            for (int i = 0; i < expanseReq.Results; i++)
            {
                var result = await getExpansionResponse(_ollama, promptReq);

                response.Content += $"{result}\n";

                if (expanseReq.Results > 1 && expanseReq.UsePrevAsExample && i < expanseReq.MaxExamples)
                {
                    if(i == 0)
                        promptReq.System += $"\n\n# EXPECTED OUTPUT EXAMPLES:\n";
                    
                    promptReq.System += $"\n\n{result}";
                }
            }

            return new ChatMessage(response.Role, response.Content);
        }

        private async Task<string> getExpansionResponse(IOllamaInferenceService ollama, GenerateRequest request)
        {
            var message = await ollama.GeneratePrompt(request);

            return message.Content.Trim();
        }

        protected override string getDefaultInstruction() => @"Your task is to answer the user question in a brief yet accurate manner. The resulting text will be used to perform a similarity search and 
retrieve more data related to the user query so follow this steps in order to generate your response:

> STEP 1: Analyze the user query and extract the intent to get a better idea of the topic he is quering about.
> STEP 2: Extract the core topics of the user query to include them in your synthetized response.
> STEP 3: Use you conclussions from the previous steps to generate a cohesive response as brief as possible providing a general explanation of the core topics detected in the user query.

Note: in case you are provided some EXPECTED OUTPUT EXAMPLES, use them to get an idea of your how your response should look like, but avoid repetition.

# USER QUERY:
";
    }
}
