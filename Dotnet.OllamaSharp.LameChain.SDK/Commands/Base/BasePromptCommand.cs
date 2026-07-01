using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Validators;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Response;
using DotnetLlamaSharp.Domain.Services.Inference;
using OllamaSharp.Models;
using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;


namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Bases
{
    public abstract class BasePromptCommand<T> : IPrompteable<T>, IJsoneable
    {
        protected string? _systemMessage = null;// setted on construction from params
        // overriden if necessary to use a hardcoded message as core message (usage for empty base _systemMessage (instruction) + hardcoded + promptGuidance(afterCore Y/N)
        // check the 'UserIntentCommand (with _default) : MessageCommand (without _default)' to see an example of that
       
        protected IOllamaInferenceService _ollama = null;
        protected CommandSettings? _settings = null;
        public string? SystemMessage => _systemMessage;
        public CommandSettings? CommandSettings => _settings;
        public IOllamaInferenceService BorrowLlama => _ollama;

        public BasePromptCommand() { }
        public BasePromptCommand(IOllamaInferenceService ollama) : this() { _ollama = ollama; }
        public BasePromptCommand(IOllamaInferenceService ollama, string? systemMessage = null, CommandSettings? settings = null) : this(ollama)
        {
            _settings = settings;
            _systemMessage = systemMessage;
        }

        public abstract Task<T> Prompt(PromptCommandRequest request);
        public virtual Task<T> PromptSync(PromptCommandRequest request) { throw new NotImplementedException("This method is meant to be overriden whenever required"); }

        protected virtual string getDefaultInstruction() => "";
        protected virtual async Task<string> getPromptInstruction(string? additionalData = null, bool isAppend = true)
        {
            var defaultMessage = getDefaultInstruction();

            if(!string.IsNullOrEmpty(defaultMessage))
            {
                var coreMessage = string.IsNullOrEmpty(additionalData) ? defaultMessage : isAppend ? $"{defaultMessage}\n{additionalData}" : $"{additionalData}\n{defaultMessage}";

                return string.IsNullOrEmpty(_systemMessage) ? coreMessage : $"{_systemMessage}\n{coreMessage}";
            }

            else return string.IsNullOrEmpty(additionalData) ? _systemMessage : isAppend ? $"{_systemMessage}\n{additionalData}" : $"{additionalData}\n{_systemMessage}";
        }
        
        protected void validateInputRequest<TReq>(PromptCommandRequest request) where TReq : PromptCommandRequest
        {
            if (request.GetType() != typeof(TReq))
                throw new InvalidOperationException($"{nameof(BasePromptCommand<T>)} >> request of type {request.GetType().Name} is not of type {typeof(TReq)}");
        }
       
        public async Task<JsonPromptResult> JsonPrompt(PromptCommandRequest request, CommandSettings? settingsOverride = null, bool returnFullInstruction= false, string? preInstruction = null)
        {
            if (settingsOverride != null)
                _settings = settingsOverride;

            if(!string.IsNullOrEmpty(_systemMessage) && !_systemMessage.Contains(preInstruction))
                _systemMessage = string.IsNullOrEmpty(preInstruction) ? _systemMessage : $"{preInstruction} {_systemMessage}";

            var promptInstruction = await getPromptInstruction(returnFullInstruction ? request.GuidanceMessage : null);

            var commandPrompt = await Prompt(request);

            string jsonResult = string.Empty;

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
                
            jsonResult = JsonSerializer.Serialize(commandPrompt, options);
            
            return new JsonPromptResult(promptInstruction, commandPrompt, commandPrompt.GetType(), jsonResult, JsonSerializerOptions.Default.GetJsonSchemaAsNode(commandPrompt.GetType()), options);
        }

        // Validators with defaultInstruction & optional guidanceMessage
        protected virtual CommandPromptValidation<T> validatorFor<T>(int validations, EPromptValidation type, string? guidanceMessage = null, CommandSettings? settings = null, bool useChatEndpoint = true)
            where T : class => new CommandPromptValidation<T> {
                Validations = validations,
                ValidationType = type,
                Validator = new JsonOutputRefinerCommand<T>(_ollama, guidanceMessage, settings ?? _settings),
                UseChatEndpoint = useChatEndpoint
            };
    }
}
