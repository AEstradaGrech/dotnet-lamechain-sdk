using Anthropic.SDK.Messaging;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service
{
    public interface IClaudeClient
    {
        Task<MessageResponse> GetMessageResponseAsync(MessageParameters request);
        Task<Message> GetMessageAsync(MessageParameters request);

        Task<string> TestClient();
    }
}
