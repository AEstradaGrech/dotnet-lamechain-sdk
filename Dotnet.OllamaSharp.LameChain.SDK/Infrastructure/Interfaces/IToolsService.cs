
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces
{
    public interface IToolsService<TSelf> where TSelf : ToolsService<TSelf>
    {
        IEnumerable<ToolInfo> GetToolsCatalogue();
    }
}
