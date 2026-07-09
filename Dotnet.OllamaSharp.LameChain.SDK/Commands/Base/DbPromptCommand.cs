using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Validators;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Bases
{
    /// <summary>
    /// All ChromaPromptCommands must have a db message name. Atomic Values are DbPromptCommands that 
    /// may read an instruction message from Chroma or use a default hardcoded message. 
    /// The also accept some extra guidance instruction from the http request that is stored as _systemMessage
    /// So they can be used:
    ///     - with _systemMessage only
    ///     - default hardcoded only
    ///     - default hardcoded + guidance
    ///     - chroma stored only
    ///     - chroma stored + guidance
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class DbPromptCommand<T> : BasePromptCommand<T>
    {
        protected readonly string _messageName = string.Empty; // messageId to pass to the message retriever
        protected readonly string _dbSourceName = string.Empty; // Collection | Table name to pass to the retriever

        protected readonly Func<string, string, Task<string>> _retrieverLambda; 

        public bool IsDefaultSetup => string.IsNullOrEmpty(_dbSourceName) && string.IsNullOrEmpty(_messageName) && _retrieverLambda ==  null;
        public DbPromptCommand() : base() { }
        public DbPromptCommand(IOllamaInferenceService ollama) : base(ollama){ }

        //This constructor is meant to support the usage of AtomicValueCommands w/DefaultInstruction and commands like the JsonRefiner (no instruction but orchestrates DB commands)
        public DbPromptCommand(IOllamaInferenceService ollama, string? systemMessage = null, CommandSettings? settings = null) : base(ollama, systemMessage, settings) { }

        public DbPromptCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null) 
            : base(ollama, guidanceMessage, settings)
        {
            _retrieverLambda = retrieverLambda;
            _messageName = messageName;
            _dbSourceName = messageSourceName;
        }

        // Method overload for validators that retrieve the validator message from DB (or any other source from the factory method stored in the lambda)
        protected virtual JsonOutputRefinerCommand<T> validatorFor<T>(string messageSource, string name, Func<string, string, Task<string>> retriever) 
            where T : class => new JsonOutputRefinerCommand<T>(_ollama, messageSource,name, retriever, null, _settings);

        protected override async Task<string> getPromptInstruction(string? additionalData = null, bool isAfterCore = true)
        {
            try
            {
                //if prefer default instruction, try get instruction
                string systemMessage = string.Empty;

                //TODO: review option & remove
                if(_settings == null || _settings.UseDefaultCommandMessage)
                    systemMessage = getDefaultInstruction(); // default core message
                // else try db msg
                if(string.IsNullOrEmpty(systemMessage) && !IsDefaultSetup) // db core message
                {
                    var dbInstruction = await _retrieverLambda(_dbSourceName, _messageName);

                    if (string.IsNullOrEmpty(dbInstruction))
                        throw new InvalidDataException($"{nameof(DbPromptCommand<T>)} >> No message found with name {_messageName} in source: {_dbSourceName}");

                    systemMessage = dbInstruction;
                }
                // if no db message and/or !defaultInstruction throw ex & try defaultInstruction (if db fail) + _system (request guidance msg)
                if (string.IsNullOrEmpty(systemMessage))
                    throw new InvalidDataException($"{nameof(DbPromptCommand<T>)} >> NO SYSTEM MESSAGE FOUND >> TRYING DEFAULT MESSAGES");
                
                if (!string.IsNullOrEmpty(additionalData))
                    systemMessage = isAfterCore ? 
                        $"{systemMessage}\n\n{additionalData}" :
                        $"{additionalData}\n\n{systemMessage}"; // enhanced core message

                return string.IsNullOrEmpty(_systemMessage) ? systemMessage : $"{_systemMessage}\n{systemMessage}";
            }
            catch (Exception ex) // 04-06-26 --> commands can work with no system message because now there are commands with no LLM logic like Sourceables & Storeables
            {
                // default hardcoded message (if any) + any guidance message from constructor
                var defaultMessage = $"{(string.IsNullOrEmpty(_systemMessage) ? "" : $"{_systemMessage}\n")}{getDefaultInstruction()}";

                if (!string.IsNullOrEmpty(additionalData))
                    defaultMessage = $"{defaultMessage}\n{additionalData}";

                return defaultMessage.Trim();
            }
            
        }
    }
}
