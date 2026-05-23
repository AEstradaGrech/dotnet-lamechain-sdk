using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service
{
    public interface IOllamaStreamService
    {
        IAsyncEnumerable<GenerateResponseStream?> SimplePromptStream(Instruction request);
        IAsyncEnumerable<ChatResponseStream?> ChatPromptStream(ChatInstruction request, bool bWithSysmsgUpdate = false);
    }
}
