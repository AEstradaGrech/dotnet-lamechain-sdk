using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.AtomicValues;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.TextGenerators;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Model;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services
{
    public interface IPromptCommandsFactory
    {
        TCommand GetCommand<TCommand, TResult>(string? systemMessage = null, PromptSettings? settings = null) where TCommand : BasePromptCommand<TResult>, new();
        TCommand GetDbCommand<TCommand, TResult>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null) 
            where TCommand : DbPromptCommand<TResult>, new();
        VectorSearchCommand GetSimilaritySearchCommand(Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction);
        TCommand GetSimilaritySearchLlama<TCommand>(Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction) 
            where TCommand : VectorSearchCommand;
        VectorSearchSourceable GetVectorSearchSourceable(Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction);
        VectorSearchSourceable GetVectorSearchSourceable(Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction,
            string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null);
        TCommand GetSourceable<TCommand>(string? llamaGuidance = null, CommandSettings? settings = null) where TCommand : SourceableCommand;
        TCommand GetDbSourceable<TCommand>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null)
            where TCommand : SourceableCommand;
        TCommand GetEmbeddedSourceable<TCommand>(Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction, string? llamaGuidance = null, CommandSettings? settings = null)
            where TCommand : VectorSearchSourceable;
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
