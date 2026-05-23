using Dotnet.OllamaSharp.LameChain.SDK.Command.Requests;

namespace Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command
{
    public interface IPrompteable<TResult>
    {
        Task<TResult> Prompt(PromptCommandRequest request);
        Task<TResult> PromptSync(PromptCommandRequest request);
    }
}
