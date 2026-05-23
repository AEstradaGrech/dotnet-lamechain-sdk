using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Requests;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Core.TextGenerators
{
    public class MessagePromptCommand : BasePromptCommand<ChatMessage>
    {
        public MessagePromptCommand() : base() { }
        public MessagePromptCommand(IOllamaInferenceService ollama) : base(ollama) { }
        public MessagePromptCommand(IOllamaInferenceService ollama, string? systemMessage, CommandSettings? settings) : base(ollama, systemMessage, settings) { }
        public override async Task<ChatMessage> Prompt(PromptCommandRequest request)
        {
            bool isChat = request.GetType() == typeof(ChatCommandRequest);

            
            var message =  isChat ?
                await _ollama.ChatPrompt(((ChatCommandRequest)request).ToOllama()) :
                await _ollama.GeneratePrompt(await getGenerateRequest(request));

            return new ChatMessage(message.Role.ToString(), message.Content);
        }
    }
}
