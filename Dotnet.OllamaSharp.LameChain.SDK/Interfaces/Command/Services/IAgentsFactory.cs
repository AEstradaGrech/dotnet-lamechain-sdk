
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Agents;

namespace Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services
{
    public interface IAgentsFactory
    {
        T CreateAgent<T>(string model, string? name, string? description, PromptSettings? settings = null) where T : LameAgent;
        T CreateAgent<T>(string model, PromptSettings? settings = null) where T : LameAgent;
        LameAgent CreateAgent(string model, PromptSettings? settings = null);
        LameAgent CreateAgent(string model, string? name, string? description, PromptSettings? settings = null);
        SelectedSkillsAgent CreateSelectedSkillsAgent(string model, string? name, string? description, PromptSettings? settings = null);
        AutoSkilledAgent CreateAutoSkilledAgent(string model, string? name, string? description, PromptSettings? settings = null);
        PythonAgent CreatePythonAgent(string model, PromptSettings? settings = null);
    }
}
