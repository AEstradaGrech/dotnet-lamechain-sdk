
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Request;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Response.GroqProvider;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Clients
{
    public interface IGroqClient
    {
        Task<GroqChatCompletion> GetChatCompletion(GroqChatRequest request);
    }
}
