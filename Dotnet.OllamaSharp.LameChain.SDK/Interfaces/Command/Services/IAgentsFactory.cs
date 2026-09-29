
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Agents;

namespace Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services
{
    public interface IAgentsFactory
    {
        LameAgent CreateAgent(string model, PromptSettings? settings = null);
        LameAgent CreateAgent(string model, string name, string description, PromptSettings? settings = null);
    }
}
