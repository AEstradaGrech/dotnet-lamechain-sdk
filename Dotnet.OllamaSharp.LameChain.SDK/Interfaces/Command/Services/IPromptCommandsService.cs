using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;

namespace Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services
{
    public interface IPromptCommandsService
    {
        Task<TResult> GuidedPromptCommand<TResult>(PromptCommandRequest request, string? guidanceMessage = null, CommandSettings settings = null)
            where TResult : class;

        Task<TResult> PromptCommand<TCommand, TResult>(PromptCommandRequest request, string? instruction = null, CommandSettings settings = null)
            where TCommand : BasePromptCommand<TResult>, new()
            where TResult : class;

        Task<TResult> DbPromptCommand<TCommand, TResult>(PromptCommandRequest request, string messageSource, string messageName, Func<string, string, Task<string>> retriever, string? guidanceMessage = null, CommandSettings settings = null)
            where TCommand : DbPromptCommand<TResult>, new();

        TCommand GetDbCommand<TCommand, TResult>(string messageSource, string messageName, Func<string, string, Task<string>> retriever, string? guidanceMessage = null, PromptSettings? settings = null)
            where TCommand : DbPromptCommand<TResult>, new();

        Task<TEnum?> EnumChoice<TEnum>(string prompt, string? guidanceMessage = null, CommandSettings settings = null) where TEnum : struct, Enum;
        Task<bool> BooleanChoice(string prompt, string? guidanceMessage = null, CommandSettings settings = null);
        Task<string> StringBoolChoice(string prompt, string? guidanceMessage = null, CommandSettings settings = null);
        Task<string> StringChoice(string prompt, List<string> choices, string? guidanceMessage = null, CommandSettings settings = null);
        Task<List<string>> MultiChoice(string prompt, List<string> choices, int maxChoices, string? guidanceMessage = null, CommandSettings settings = null);
        Task<float?> NumericResult(string prompt, string? guidanceMessage = null, CommandSettings settings = null);
        Task<ScoredBoolResponse> ScoredBool(string prompt, string? instrucion = null, CommandSettings settings = null);
        Task<ScoredStringChoice> ScoredChoice(List<string> choices, string prompt, string? instrucion = null, CommandSettings settings = null);
    }
}
