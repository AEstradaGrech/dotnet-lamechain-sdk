using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.QueryCommands
{
    public class MessagePromptCommand : BasePromptCommand<ChatMessage>
    {
        public MessagePromptCommand() : base() { }
        public MessagePromptCommand(IOllamaInferenceService ollama, string? systemMessage = null, CommandSettings? settings = null) : base(ollama, systemMessage, settings) { }
        public override async Task<ChatMessage> Prompt(PromptCommandRequest request)
        {
            var message = request.GetType() == typeof(ChatCommandRequest) ?
                await _ollama.ChatPrompt(request.ToOllamaChat(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings), provider: request.Provider, request.HasTools ? request.Tools : null) :
                await _ollama.GeneratePrompt(request.ToOllamaGenerate(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings), provider: request.Provider);

            return new ChatMessage(message.Role.ToString(), message.Content);
        }
    }
}
