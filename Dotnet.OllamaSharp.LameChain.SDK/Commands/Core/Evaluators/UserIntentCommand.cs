using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.TextGenerators;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Requests;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Evaluators
{
    public class UserIntentCommand : MessagePromptCommand
    {
        public UserIntentCommand() : base() { }
        public UserIntentCommand(IOllamaInferenceService ollama) : base(ollama) { }
        public UserIntentCommand(IOllamaInferenceService ollama, string? systemMessage = null, CommandSettings? settings = null) : base(ollama, systemMessage, settings) {}

        public override async Task<ChatMessage> Prompt(PromptCommandRequest request)
        {
            var instruction = await getPromptInstruction();

            var systemMessage = string.IsNullOrEmpty(_systemMessage) ? instruction : $"{_systemMessage}.\n{instruction}"; 

            var chatReq = new ChatRequest
            {
                Model = request.Model,
                Messages = [new Message(ChatRole.System, systemMessage), new Message(ChatRole.User, request.Prompt)],
                Stream = false,
                Options = request.Settings
            };

            var response = await _ollama.ChatPrompt(chatReq);

            return new ChatMessage(response.Role.ToString(), response.Content);
        }

        //TODO: if _dbMessageName == null, este message
        protected override async Task<string> getPromptInstruction(string? additionalData = null, bool isAfterCore = true)
            => isAfterCore ? getDefaultInstruction() + $"\n{additionalData}" : additionalData + $"\n{getDefaultInstruction()}";

        protected virtual string getDefaultInstruction() => @"Analyze the user prompt and try to determine the user intent. Pay attention to what is the user trying to do, what does the user expect you to do, 
            what's the user query about or the scope of the user request (in case is chatting or role-playing) and output a short description (10 words maximum) of the user intent";
    }
}
