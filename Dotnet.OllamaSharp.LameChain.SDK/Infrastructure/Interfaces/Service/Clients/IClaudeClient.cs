using Anthropic.SDK.Messaging;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Clients
{
    public interface IClaudeClient
    {
        Task<MessageResponse> GetMessageResponseAsync(MessageParameters request);
        Task<Message> GetMessageAsync(MessageParameters request);
    }
}
