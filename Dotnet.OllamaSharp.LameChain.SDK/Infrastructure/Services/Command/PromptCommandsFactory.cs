using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.AtomicValues;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.TextGenerators;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;
using DotnetLlamaSharp.Domain.Services.Embeddings;
using DotnetLlamaSharp.Domain.Services.Inference;
using OllamaSharp.Models.Chat;

namespace DotnetLlamaSharp.Services.Prompting
{
    public class PromptCommandsFactory : IPromptCommandsFactory
    {
        private readonly IOllamaInferenceService _ollama;
        private readonly IEmbeddingsService _embeddingsService;
        public PromptCommandsFactory(IOllamaInferenceService ollama, IEmbeddingsService embeddingsService) 
        { 
            _ollama = ollama;
            _embeddingsService = embeddingsService;
        }
        
        //Generic factory methods
        public TCommand GetCommand<TCommand, TResult>(string? systemMessage = null, PromptSettings? settings = null) where TCommand : BasePromptCommand<TResult>, new()
            => Activator.CreateInstance(typeof(TCommand), _ollama, systemMessage, settings) as TCommand;

        public TCommand GetDbCommand<TCommand, TResult>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null) 
            where TCommand : DbPromptCommand<TResult>, new()
            => Activator.CreateInstance(typeof(TCommand), _ollama, source, messageName, retrieverLambda, guidanceMessage, settings) as TCommand;

        public SimilaritySearchCommand GetSimilaritySearchCommand(Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<SimilarSearchResult>>> queryFunction)
            => Activator.CreateInstance(typeof(SimilaritySearchCommand), _embeddingsService, queryFunction) as SimilaritySearchCommand;

        public TCommand GetSimilaritySearchLlama<TCommand>(Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<SimilarSearchResult>>> queryFunction) where TCommand : SimilaritySearchCommand
           => Activator.CreateInstance(typeof(TCommand),_ollama, _embeddingsService, queryFunction) as TCommand;

        public SimilarSourceCommand GetSimilaritySearchSourceable(Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<SimilarSearchResult>>> queryFunction)
            => Activator.CreateInstance(typeof(SimilarSourceCommand), _embeddingsService, queryFunction) as SimilarSourceCommand;

        public SimilarSourceCommand GetSimilaritySearchSourceable(Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<SimilarSearchResult>>> queryFunction,
            string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null)
           => Activator.CreateInstance(typeof(SimilarSourceCommand), _embeddingsService, queryFunction, source, messageName, retrieverLambda, guidanceMessage, settings) as SimilarSourceCommand;

        // Sourceables with no DB dependency AND ollama dependency (no sysmessage. SimilaritySearchCommands for example)
        // They recieve a copy of the _ollama service because the framework is built with it)
        public TCommand GetSourceable<TCommand>() where TCommand : SourceableCommand
            => Activator.CreateInstance(typeof(TCommand), _ollama) as TCommand;

        // A command that makes use of the LLM somehow to generate the List<string> result of all SourceableCommands
        public TCommand GetSourceable<TCommand>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null) where TCommand : SourceableCommand
            => Activator.CreateInstance(typeof(TCommand), _ollama, source, messageName, retrieverLambda, guidanceMessage, settings) as TCommand;

        //Domain object command helpers
        public MessagePromptCommand GetMessagePromptCommand(string? systemMessage = null, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(MessagePromptCommand), _ollama, systemMessage, settings) as MessagePromptCommand;

        //Atomic Value Helpers:
        // WITH DEFAULT INSTRUCTION SETUP
        public EnumPromptCommand<TEnum> GetEnumChoiceCommand<TEnum>(string? guidanceMessage = null, PromptSettings? settings = null) where TEnum : struct, Enum
            => Activator.CreateInstance(typeof(EnumPromptCommand<TEnum>), _ollama, null, null, null, guidanceMessage, settings) as EnumPromptCommand<TEnum>;

        public BoolPromptCommand GetBoolPromptCommand(string? guidanceMessage = null, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(BoolPromptCommand), _ollama, null, null, null, guidanceMessage, settings) as BoolPromptCommand;

        public StringChoiceCommand GetStringChoiceCommand(string? guidanceMessage = null, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(StringChoiceCommand), _ollama, null, null, null, guidanceMessage, settings) as StringChoiceCommand;

        public MultiChoiceCommand GetMultiChoiceCommand(string? guidanceMessage = null, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(MultiChoiceCommand), _ollama, null, null, null, guidanceMessage, settings) as MultiChoiceCommand;

        public NumericPromptCommand GetNumericPromptCommand(string? guidanceMessage = null, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(NumericPromptCommand), _ollama, null, null, null, guidanceMessage, settings) as NumericPromptCommand;

        // Atomic Values with DB SETUP
        public EnumPromptCommand<TEnum> GetEnumChoiceCommand<TEnum>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null) where TEnum : struct, Enum
            => Activator.CreateInstance(typeof(EnumPromptCommand<TEnum>), _ollama, source, messageName, retrieverLambda, guidanceMessage, settings) as EnumPromptCommand<TEnum>;
        public BoolPromptCommand GetBoolPromptCommand(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(BoolPromptCommand), _ollama, source, messageName, retrieverLambda, guidanceMessage, settings) as BoolPromptCommand;

        public StringChoiceCommand GetStringChoiceCommand(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(StringChoiceCommand), _ollama, source, messageName, retrieverLambda, guidanceMessage, settings) as StringChoiceCommand;

        public MultiChoiceCommand GetMultiChoiceCommand(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(MultiChoiceCommand), _ollama, source, messageName, retrieverLambda, guidanceMessage, settings) as MultiChoiceCommand;

        public NumericPromptCommand GetNumericPromptCommand(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(NumericPromptCommand), _ollama, source, messageName, retrieverLambda, guidanceMessage, settings) as NumericPromptCommand;


        public IJsoneable GetAsJsoneable<TCommand, TResult>(string instruction) where TCommand : BasePromptCommand<TResult>, new()
           => GetCommand<TCommand, TResult>(instruction, settings: null);
        
        //With Default setup
        public IJsoneable GetAsJsoneable<TCommand, TResult>(string? instruction = null, PromptSettings? settings = null) where TCommand : BasePromptCommand<TResult>, new()
           => GetCommand<TCommand, TResult>(instruction, settings);
        // With DB Setup
        public IJsoneable GetAsJsoneable<TCommand, TResult>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null) where TCommand : DbPromptCommand<TResult>, new()
           =>  GetDbCommand<TCommand, TResult>(source, messageName, retrieverLambda, guidanceMessage, settings);
        
        public IPrompteable<TResult> GetAsPrompteable<TCommand, TResult>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(TCommand), _ollama, source, messageName, retrieverLambda, guidanceMessage, settings) as IPrompteable<TResult>;

        public IPrompteable<Message> GetPrompteableMessage(string? systemMessage = null, PromptSettings? settings = null)
            => Activator.CreateInstance(typeof(MessagePromptCommand), _ollama, systemMessage, settings) as IPrompteable<Message>;
    }
}
