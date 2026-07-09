
using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Clients
{
    public interface IClaudeClient
    {
        Task<Message> GetMessageAsync(MessageCreateParams request);
        Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options);
    }
}
