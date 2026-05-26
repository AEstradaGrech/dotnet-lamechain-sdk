using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Validators;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
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
        protected CommandSettings _settings = null;
        public string? SystemMessage => _systemMessage;

        public IOllamaInferenceService BorrowLlama => _ollama;

        public BasePromptCommand() { _settings = new CommandSettings(maxTokens: 600, temperature: 0f, topP: .1f, topK: 10); }
        public BasePromptCommand(IOllamaInferenceService ollama) : this() { _ollama = ollama; }
        public BasePromptCommand(IOllamaInferenceService ollama, string? systemMessage = null, CommandSettings? settings = null) : this(ollama)
        {
            _settings = settings ?? new CommandSettings();
            _systemMessage = systemMessage;
        }

        public abstract Task<T> Prompt(PromptCommandRequest request);
        public virtual Task<T> PromptSync(PromptCommandRequest request) { throw new NotImplementedException("This method is meant to be overriden whenever required"); }

        protected virtual string getDefaultInstruction() => "";
        protected virtual async Task<string> getPromptInstruction(string? additionalData = null, bool isAfterCore = true)
        {
            var defaultMessage = getDefaultInstruction();

            return string.IsNullOrEmpty(defaultMessage) ?
                string.IsNullOrEmpty(additionalData) ? _systemMessage : isAfterCore ? $"{_systemMessage}\n\n{additionalData}" : $"{additionalData}\n\n{_systemMessage}" :
                string.IsNullOrEmpty(_systemMessage) ? 
                string.IsNullOrEmpty(additionalData) ? defaultMessage : isAfterCore ? $"{defaultMessage}\n\n{additionalData}" : $"{additionalData}\n\n{defaultMessage}" :
                $"{_systemMessage}\n" + (string.IsNullOrEmpty(additionalData) ? defaultMessage : isAfterCore ? $"{defaultMessage}\n\n{additionalData}" : $"{additionalData}\n\n{defaultMessage}");
        }
        
        protected void validateInputRequest<TReq>(PromptCommandRequest request) where TReq : PromptCommandRequest
        {
            if (request.GetType() != typeof(TReq))
                throw new InvalidOperationException($"{nameof(BasePromptCommand<T>)} >> request of type {request.GetType().Name} is not of type {typeof(TReq)}");
        }
        protected async Task<GenerateRequest> getGenerateRequest(PromptCommandRequest request, bool withInstruction = true)
            => new GenerateRequest
            {
                Model = string.IsNullOrEmpty(request.Model) ? "qwen2.5:7b" : request.Model,
                Prompt = request.Prompt,
                System = withInstruction ? await getPromptInstruction(request.GuidanceMessage) : string.Empty,
                Stream = false,
                Options = _settings.ToOllamaRequest()
            };
       
        public async Task<JsonPromptResult> JsonPrompt(PromptCommandRequest request, bool returnFullInstruction= false, string? preInstruction = null, bool withStringEnums = true)
        {
            var sysmsg = _systemMessage;

            _systemMessage = string.IsNullOrEmpty(preInstruction) ? _systemMessage : $"{preInstruction} {_systemMessage}";

            var promptInstruction = await getPromptInstruction(returnFullInstruction ? request.GuidanceMessage : null);

            var commandPrompt = await Prompt(request);

            _systemMessage = sysmsg;

            string jsonResult = string.Empty;

            if (withStringEnums) 
            {
                var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

                options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                
                jsonResult = JsonSerializer.Serialize(commandPrompt, options);
            }
            else jsonResult = JsonSerializer.Serialize(commandPrompt);

            return new JsonPromptResult(promptInstruction, commandPrompt, commandPrompt.GetType(), jsonResult, JsonSerializerOptions.Default.GetJsonSchemaAsNode(commandPrompt.GetType()));
        }

        // Validators with defaultInstruction & optional guidanceMessage
        protected virtual JsonOutputRefinerCommand<T> validatorFor<T>(string? guidanceMessage = null, CommandSettings? settings = null) 
            where T : class => new JsonOutputRefinerCommand<T>(_ollama, guidanceMessage, settings ?? _settings);
    }
}
