using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.QueryCommands
{
    public class RagQueryCommand : DbPromptCommand<ChatMessage>
    {
        public RagQueryCommand()
        {
        }

        /// <summary>
        /// Use llama to perform Qestion-Answer tasks with gathered data
        /// </summary>
        /// <param name="ollama"></param>
        /// <param name="systemMessage"></param>
        /// <param name="settings"></param>
        public RagQueryCommand(IOllamaInferenceService ollama, string? systemMessage = null, CommandSettings? settings = null) : base(ollama, systemMessage, settings)
        {
        }

        /// <summary>
        /// Use llama to perform Question-Answer task reading the SystemMessage from an external source using the retriever lambda function (that reads from a DB, for example)
        /// </summary>
        /// <param name="ollama"></param>
        /// <param name="messageSourceName"></param>
        /// <param name="messageName"></param>
        /// <param name="retrieverLambda"></param>
        /// <param name="guidanceMessage"></param>
        /// <param name="settings"></param>
        public RagQueryCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retrieverLambda, 
            string? guidanceMessage = null, CommandSettings? settings = null) : base(ollama, messageSourceName, messageName, retrieverLambda, guidanceMessage, settings)
        {
        }

        public override async Task<ChatMessage> Prompt(PromptCommandRequest request)
        {
            var message = request.GetType() == typeof(ChatCommandRequest) ?
                await _ollama.ChatPrompt(request.ToOllamaChat(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings)) :
                await _ollama.GeneratePrompt(request.ToOllamaGenerate(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings));

            return new ChatMessage(message.Role.ToString(), message.Content);
        }

        protected override string getDefaultInstruction() => @"
Your task is to answer any user question or query using the provided data as a grounded source of truth.
Follow this steps in order to generate your final response:

> STEP 1: Analyze the user input prompt to get a clear idea of what is the user querying about.
> STEP 2: Review the provided sources in the ADDITIONAL INFORMATION section (if any) to find relevant information related to the user query.
> STEP 3: Reason your final response using any relevant data found to generate a coherent and consistent response to answer the user.
";
    }
}
