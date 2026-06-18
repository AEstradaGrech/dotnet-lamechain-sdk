using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.AtomicValues;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Model;
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
        public TCommand GetCommand<TCommand, TResult>(string? systemMessage = null, CommandSettings? settings = null) where TCommand : BasePromptCommand<TResult>, new()
            => Activator.CreateInstance(typeof(TCommand), _ollama, systemMessage, settings) as TCommand;

        public TCommand GetDbCommand<TCommand, TResult>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null) 
            where TCommand : DbPromptCommand<TResult>, new()
            => Activator.CreateInstance(typeof(TCommand), _ollama, source, messageName, retrieverLambda, guidanceMessage, settings) as TCommand;

        public VectorSearchCommand GetSimilaritySearchCommand(Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction)
            => Activator.CreateInstance(typeof(VectorSearchCommand), _embeddingsService, queryFunction) as VectorSearchCommand;

        public TCommand GetSimilaritySearchLlama<TCommand>(Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction) where TCommand : VectorSearchCommand
           => Activator.CreateInstance(typeof(TCommand),_ollama, _embeddingsService, queryFunction) as TCommand;

        public VectorSearchSourceable GetVectorSearchSourceable(Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction)
            => Activator.CreateInstance(typeof(VectorSearchSourceable), _embeddingsService, queryFunction) as VectorSearchSourceable;

        public TCommand GetEmbeddedSourceable<TCommand>(Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction, string? llamaGuidance = null, CommandSettings? settings = null) 
            where TCommand : VectorSearchSourceable
            => Activator.CreateInstance(typeof(TCommand), _ollama, _embeddingsService, queryFunction, llamaGuidance, settings) as TCommand;

        public VectorSearchSourceable GetVectorSearchSourceable(Func<string, ReadOnlyMemory<float>, int, Dictionary<string, object>, Task<List<ILameSearchResult>>> queryFunction,
            string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
           => Activator.CreateInstance(typeof(VectorSearchSourceable), _ollama, _embeddingsService, queryFunction, source, messageName, retrieverLambda, guidanceMessage, settings) as VectorSearchSourceable;


        // Sourceables with no DB dependency AND ollama dependency (no sysmessage. SimilaritySearchCommands for example)
        // They recieve a copy of the _ollama service because the framework is built with it)
        public TCommand GetSourceable<TCommand>(string? llamaGuidance = null, CommandSettings? settings = null) where TCommand : SourceableCommand
            => Activator.CreateInstance(typeof(TCommand), _ollama, llamaGuidance, settings) as TCommand;

        // A command that makes use of the LLM somehow to generate the List<string> result of all SourceableCommands
        public TCommand GetDbSourceable<TCommand>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null) where TCommand : SourceableCommand
            => Activator.CreateInstance(typeof(TCommand), _ollama, source, messageName, retrieverLambda, guidanceMessage, settings) as TCommand;

        //Domain object command helpers
        public MessagePromptCommand GetMessagePromptCommand(string? systemMessage = null, CommandSettings? settings = null)
            => Activator.CreateInstance(typeof(MessagePromptCommand), _ollama, systemMessage, settings) as MessagePromptCommand;

        //Atomic Value Helpers:
        // WITH DEFAULT INSTRUCTION SETUP
        public EnumPromptCommand<TEnum> GetEnumChoiceCommand<TEnum>(string? guidanceMessage = null, CommandSettings? settings = null) where TEnum : struct, Enum
            => Activator.CreateInstance(typeof(EnumPromptCommand<TEnum>), _ollama, null, null, null, guidanceMessage, settings) as EnumPromptCommand<TEnum>;

        public BoolPromptCommand GetBoolPromptCommand(string? guidanceMessage = null, CommandSettings? settings = null)
            => Activator.CreateInstance(typeof(BoolPromptCommand), _ollama, null, null, null, guidanceMessage, settings) as BoolPromptCommand;

        public StringChoiceCommand GetStringChoiceCommand(string? guidanceMessage = null, CommandSettings? settings = null)
            => Activator.CreateInstance(typeof(StringChoiceCommand), _ollama, null, null, null, guidanceMessage, settings) as StringChoiceCommand;

        public MultiChoiceCommand GetMultiChoiceCommand(string? guidanceMessage = null, CommandSettings? settings = null)
            => Activator.CreateInstance(typeof(MultiChoiceCommand), _ollama, null, null, null, guidanceMessage, settings) as MultiChoiceCommand;

        public NumericPromptCommand GetNumericPromptCommand(string? guidanceMessage = null, CommandSettings? settings = null)
            => Activator.CreateInstance(typeof(NumericPromptCommand), _ollama, null, null, null, guidanceMessage, settings) as NumericPromptCommand;

        // Atomic Values with DB SETUP
        public EnumPromptCommand<TEnum> GetEnumChoiceCommand<TEnum>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null) where TEnum : struct, Enum
            => Activator.CreateInstance(typeof(EnumPromptCommand<TEnum>), _ollama, source, messageName, retrieverLambda, guidanceMessage, settings) as EnumPromptCommand<TEnum>;
        public BoolPromptCommand GetBoolPromptCommand(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
            => Activator.CreateInstance(typeof(BoolPromptCommand), _ollama, source, messageName, retrieverLambda, guidanceMessage, settings) as BoolPromptCommand;

        public StringChoiceCommand GetStringChoiceCommand(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
            => Activator.CreateInstance(typeof(StringChoiceCommand), _ollama, source, messageName, retrieverLambda, guidanceMessage, settings) as StringChoiceCommand;

        public MultiChoiceCommand GetMultiChoiceCommand(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
            => Activator.CreateInstance(typeof(MultiChoiceCommand), _ollama, source, messageName, retrieverLambda, guidanceMessage, settings) as MultiChoiceCommand;

        public NumericPromptCommand GetNumericPromptCommand(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
            => Activator.CreateInstance(typeof(NumericPromptCommand), _ollama, source, messageName, retrieverLambda, guidanceMessage, settings) as NumericPromptCommand;


        public IJsoneable GetAsJsoneable<TCommand, TResult>(string instruction) where TCommand : BasePromptCommand<TResult>, new()
           => GetCommand<TCommand, TResult>(instruction, settings: null);
        
        //With Default setup
        public IJsoneable GetAsJsoneable<TCommand, TResult>(string? instruction = null, CommandSettings? settings = null) where TCommand : BasePromptCommand<TResult>, new()
           => GetCommand<TCommand, TResult>(instruction, settings);
        // With DB Setup
        public IJsoneable GetAsJsoneable<TCommand, TResult>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null) where TCommand : DbPromptCommand<TResult>, new()
           =>  GetDbCommand<TCommand, TResult>(source, messageName, retrieverLambda, guidanceMessage, settings);
        
        public IPrompteable<TResult> GetAsPrompteable<TCommand, TResult>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
            => Activator.CreateInstance(typeof(TCommand), _ollama, source, messageName, retrieverLambda, guidanceMessage, settings) as IPrompteable<TResult>;

        public IPrompteable<Message> GetPrompteableMessage(string? systemMessage = null, CommandSettings? settings = null)
            => Activator.CreateInstance(typeof(MessagePromptCommand), _ollama, systemMessage, settings) as IPrompteable<Message>;

        public StoreableCommand<TStored> GetStoreable<TStored>(Func<TStored, string, Task<TStored>> storingLambda) where TStored : class
            => Activator.CreateInstance(typeof(StoreableCommand<TStored>), storingLambda) as StoreableCommand<TStored>;

        public StoreableCommand<TStored> GetStoreableLlama<TStored>(Func<TStored, string, Task<TStored>> storingLambda, string? llamaGuidance = null, CommandSettings? settings = null) where TStored : class
            => Activator.CreateInstance(typeof(StoreableCommand<TStored>), _ollama, storingLambda, llamaGuidance, settings) as StoreableCommand<TStored>;

        public StoreableCommand<TStored> GetStoreableLlama<TStored>(Func<TStored, string, Task<TStored>> storingLambda, string source, string messageName, 
            Func<string, string, Task<string>> retrieverLambda, string? llamaGuidance = null, CommandSettings? settings = null) where TStored : class
            => Activator.CreateInstance(typeof(StoreableCommand<TStored>), _ollama, storingLambda, source, messageName, retrieverLambda, llamaGuidance, settings) as StoreableCommand<TStored>;
    }
}
