using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.AtomicValues;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Evaluators;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.AtomicValues;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;
using Microsoft.Extensions.Options;

namespace DotnetLlamaSharp.Services.Prompting
{
    public class PromptCommandsService : IPromptCommandsService
    {
        private readonly IPromptCommandsFactory _factory;
        private readonly OllamaSettings _settings;
        public PromptCommandsService(IPromptCommandsFactory factory,   IOptions<OllamaSettings> settings)
        {
            _factory = factory;
            _settings = settings.Value;
        }

        /// <summary>
        /// Usage: Commands with NO db dependency (DbCommand inheritance) and no Instruction neither (uses the default hardcoded message, if any)
        ///        The it will use --> system = request.SystemMessage (as instruction / guidance)
        ///                        --> user prompt
        ///                        
        ///         var result = await _ollamaCommands.GuidedPromptCommand<PromptCommandRequest, ChatMessage>(
        ///             new PromptCommandRequest(request.Prompt, request.Settings.ToOllamaRequest(), request.Settings.Model),
        ///             guidanceMessage: "Send the system guidance message here",
        ///              request.Settings);
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="request"></param>
        /// <param name="instruction"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        public async Task<TResult> GuidedPromptCommand<TResult>(PromptCommandRequest request, string? instruction = null, CommandSettings settings = null)
            where TResult : class
                => await _factory.GetCommand<GuidedStructuredPrompt<TResult>, TResult>(instruction, settings)
                                 .Prompt(request);

        /// <summary>
        /// Executes TCommand with TRequest and returns TResult
        /// 
        /// This method is to execute any Command-Request-Type with NO Db dependency (including DbCommands with DefaultSetup like AtomicValue commands)
        /// </summary>
        /// <typeparam name="TCommand"></typeparam>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="request"></param>
        /// <param name="instruction"></param>
        /// <param name="settings"></param>
        /// <returns></returns>

        public async Task<TResult> PromptCommand<TCommand, TResult>(PromptCommandRequest request, string? guidanceMessage = null, CommandSettings settings = null)
            where TCommand : BasePromptCommand<TResult>, new()
            where TResult : class
                => await _factory.GetCommand<TCommand, TResult>(guidanceMessage, settings)
                                 .Prompt(request);

        // With DbSetup
        public async Task<TResult> DbPromptCommand<TCommand, TResult>(PromptCommandRequest request, string messageSource, string messageName, Func<string, string, Task<string>> retriever, string? guidanceMessage = null, CommandSettings settings = null)
            where TCommand : DbPromptCommand<TResult>, new()
               => await _factory.GetDbCommand<TCommand, TResult>(messageSource, messageName, retriever, guidanceMessage, settings)
                                 .Prompt(request);
        

        //TODO: DefaultMessageVersion for:
        public async Task<TEnum?> EnumChoice<TEnum>(string prompt, string? guidanceMessage = null, CommandSettings settings = null) where TEnum : struct, Enum
        {
            var command = _factory.GetEnumChoiceCommand<TEnum>(source: null, messageName: null, retrieverLambda: null, guidanceMessage, settings);

            return await command.Prompt(new PromptCommandRequest(prompt));
        }

        public async Task<string> StringChoice(string prompt, List<string> choices, string? guidanceMessage = null, CommandSettings settings = null)
        {
            //DefaultSetup
            var command = _factory.GetDbCommand<StringChoiceCommand, string>(source: null, messageName: null, retrieverLambda: null, guidanceMessage, settings);

             var response = await command.Prompt(new StringChoiceRequest(choices, prompt,  settings.Model));

            return response;
        }

        public async Task<List<string>> MultiChoice(string prompt, List<string> choices, int maxChoices, string? guidanceMessage = null, CommandSettings settings = null)
        {
            var command = _factory.GetDbCommand<MultiChoiceCommand, List<string>>(source: null, messageName: null, retrieverLambda: null, guidanceMessage, settings);

            var response = await command.Prompt(new MultiChoiceRequest(maxChoices, choices, prompt, settings.Model));

            return response;
        }

        public async Task<bool> BooleanChoice(string prompt, string? guidanceMessage = null, CommandSettings settings = null)
        {
            var command = _factory.GetDbCommand<BoolPromptCommand, bool>(source: null, messageName: null, retrieverLambda: null, guidanceMessage, settings);

            return await command.Prompt(new PromptCommandRequest(prompt, settings.Model));
        }

        public async Task<string> StringBoolChoice(string prompt, string? guidanceMessage = null, CommandSettings settings = null)
            => await BooleanChoice(prompt, guidanceMessage, settings) ? "YES" : "NO";

        public async Task<float?> NumericResult(string prompt, string? guidanceMessage = null, CommandSettings settings = null)
        {
            var command = _factory.GetNumericPromptCommand(source: null, messageName: null, retrieverLambda: null, guidanceMessage, settings);

            return await command.Prompt(new PromptCommandRequest(prompt, settings.Model));
        }

        public async Task<ScoredBoolResponse> ScoredBool(string prompt, string? guidanceMessage = null, CommandSettings settings = null)
            => await DbPromptCommand<ScoredBoolCommand, ScoredBoolResponse>(
                new PromptCommandRequest(prompt), messageSource: null, messageName: null, retriever: null, guidanceMessage, settings);

        public async Task<ScoredStringChoice> ScoredChoice(List<string> choices, string prompt, string? guidanceMessage = null, CommandSettings settings = null)
            => await DbPromptCommand<ScoredChoiceCommand, ScoredStringChoice>(
                new StringChoiceRequest(choices, prompt, settings.Model), messageSource: null, messageName: null, retriever: null, guidanceMessage, settings);

        public TCommand GetDbCommand<TCommand, TResult>(string source, string messageName, Func<string, string, Task<string>> retriever, string? guidanceMessage = null, PromptSettings? settings = null) where TCommand : DbPromptCommand<TResult>, new()
                => _factory.GetDbCommand<TCommand, TResult>(source, messageName, retriever, guidanceMessage, settings);

    }
}
