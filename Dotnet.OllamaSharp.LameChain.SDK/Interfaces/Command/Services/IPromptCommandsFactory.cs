using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.AtomicValues;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.TextGenerators;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services
{
    public interface IPromptCommandsFactory
    {
        TCommand GetCommand<TCommand, TResult>(string? systemMessage = null, PromptSettings? settings = null) where TCommand : BasePromptCommand<TResult>, new();
        TCommand GetDbCommand<TCommand, TResult>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null) 
            where TCommand : DbPromptCommand<TResult>, new();
        MessagePromptCommand GetMessagePromptCommand(string? systemMessage = null, PromptSettings? settings = null);

        // With Default setup
        EnumPromptCommand<TEnum> GetEnumChoiceCommand<TEnum>(string? guidanceMessage = null, PromptSettings? settings = null) where TEnum : struct, Enum;
        BoolPromptCommand GetBoolPromptCommand(string? guidanceMessage = null, PromptSettings? settings = null);
        StringChoiceCommand GetStringChoiceCommand(string? guidanceMessage = null, PromptSettings? settings = null);
        MultiChoiceCommand GetMultiChoiceCommand(string? guidanceMessage = null, PromptSettings? settings = null);
        NumericPromptCommand GetNumericPromptCommand(string? guidanceMessage = null, PromptSettings? settings = null);
        // With DB setup
        EnumPromptCommand<TEnum> GetEnumChoiceCommand<TEnum>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null) where TEnum : struct, Enum;
        BoolPromptCommand GetBoolPromptCommand(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null);
        StringChoiceCommand GetStringChoiceCommand(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null);
        NumericPromptCommand GetNumericPromptCommand(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null);
        MultiChoiceCommand GetMultiChoiceCommand(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null);

        IPrompteable<TResult> GetAsPrompteable<TCommand, TResult>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null);
        IPrompteable<Message> GetPrompteableMessage(string? systemMessage = null, PromptSettings? settings = null);

        IJsoneable GetAsJsoneable<TCommand, TResult>(string instruction) where TCommand : BasePromptCommand<TResult>, new();
        IJsoneable GetAsJsoneable<TCommand, TResult>(string instruction, PromptSettings? settings = null) where TCommand : BasePromptCommand<TResult>, new();
        IJsoneable GetAsJsoneable<TCommand, TResult>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null) 
            where TCommand : DbPromptCommand<TResult>, new();
    }
}
