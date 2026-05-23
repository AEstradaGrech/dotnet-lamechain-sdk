using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.AtomicValues;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.TextGenerators;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;
using DotnetLlamaSharp.Domain.Services.Inference;
using OllamaSharp.Models.Chat;

namespace DotnetLlamaSharp.Services.Prompting
{
    public class PromptCommandsFactory : IPromptCommandsFactory
    {
        private readonly IOllamaInferenceService _ollama;
        public PromptCommandsFactory(IOllamaInferenceService ollama) 
        { 
            _ollama = ollama;
        }
        
        //Generic factory methods
        public TCommand GetCommand<TCommand, TResult>(string? systemMessage = null, PromptSettings? settings = null) where TCommand : BasePromptCommand<TResult>, new()
            => Activator.CreateInstance(typeof(TCommand), _ollama, systemMessage, settings) as TCommand;

        public TCommand GetDbCommand<TCommand, TResult>(string source, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, PromptSettings? settings = null) 
            where TCommand : DbPromptCommand<TResult>, new()
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
