using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Evaluators;
using Dotnet.OllamaSharp.LameChain.SDK.Extensions.Model;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Exceptions;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.InferenceHandlers;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Schema;

namespace DotnetLlamaSharp.Infrastructure.Services.Inference
{
    public class OllamaInferenceService : IOllamaInferenceService
    {
        private readonly IOllamaApiClient _client;
        private readonly OllamaSettings _settings;
        private readonly ILogger<OllamaInferenceService> _logger;
        private BaseHandler _handler;

        public OllamaInferenceService(IOllamaApiClient client, IConfiguration config, IServiceProvider provider, IOptions<OllamaSettings> settings, ILogger<OllamaInferenceService> logger)
        {
            _client = client;
            _settings = settings.Value;
            _logger = logger;
            _handler = new OllamaHandler(provider, config, new Action<string,string>(onHandlerNotify));
            
        }
       
        private void onHandlerNotify(string title, string message)
        {
            _logger.Log(LogLevel.Information, $"{title} >> {message}");
        }
        public async Task<Message> GeneratePrompt(GenerateRequest request, string provider)
        {
            provider = provider.Trim();
            
            if (string.IsNullOrEmpty(provider))
                throw new InvalidDataException($"{nameof(OllamaInferenceService)}.{nameof(GeneratePrompt)} >> NO LLM PROVIDER SELECTED");


            if (string.IsNullOrEmpty(request.Model))
                request.Model = _settings.DefaultModel; //TODO: getDefaultModelForProvider(provider)

            string llmResponse = string.Empty;
            request.Stream = false;

            if (!_handler.IsOfType<OllamaHandler>())
            {
                _handler = _handler.UpdateHandler(provider);

                llmResponse = await _handler.GetLlmResponse(request.GenerateRequestToChat());
            }

            else llmResponse = await _handler.AsType<OllamaHandler>().GenerateLlmResponse(request);

           return new Message { Role = ChatRole.Assistant, Content = llmResponse };
        }

        public async Task<Message> ChatPrompt(ChatRequest request, string provider, Dictionary<string, MethodInfo>? requestTools = null)
        {
            provider = provider.Trim();

            if (string.IsNullOrEmpty(provider))
                throw new InvalidDataException($"{nameof(OllamaInferenceService)}.{nameof(GeneratePrompt)} >> NO LLM PROVIDER SELECTED");

            if (string.IsNullOrEmpty(request.Model))
                request.Model = !string.IsNullOrEmpty(_handler.ScopedModel) ? _handler.ScopedModel : _settings.DefaultModel; //TODO: getDefaultModelForProvider(provider) request.Model = _provider.IsSolvingTools ? _provider.CurrentModel : _settings.DefaultModelByProvider(_provider.Name)

            string llmResponse = string.Empty;
            request.Stream = false;

            // the handler is scoped by request but when it calls any tool
            // and the default settings are applied, it updates the handler to the default ollama provider
            // to solve the any tool using a LLM request. Because the handler keeps its state during the request
            // the boolean is preserved and avoids the SDK handler update
            if (!_handler.IsProvider(provider) && !_handler.IsSolvingTools)
                _handler = _handler.UpdateHandler(provider);

            llmResponse = await _handler.GetLlmResponse(request, requestTools);

            return new Message { Role = ChatRole.Assistant, Content = llmResponse };
        }

        public IAsyncEnumerable<GenerateResponseStream?> GeneratePromptStream(GenerateRequest request)
        {
            request.Stream = true;
            
            return _client.GenerateAsync(request);
        }

        public IAsyncEnumerable<ChatResponseStream?> ChatPromptStream(ChatRequest request)
        {
            request.Stream = true;

            return _client.ChatAsync(request);
        }

        public async Task<EmbedResponse> GetEmbeddings(EmbedRequest request)
            => await _client.EmbedAsync(request);

        public async Task<T> StructuredPrompt<T>(string prompt, string model, string provider, string? systemGuidance = null, RequestOptions? options = null) where T : class
        {
            if (string.IsNullOrEmpty(prompt) && string.IsNullOrEmpty(systemGuidance))
                throw new InvalidDataException($"{nameof(OllamaInferenceService)} >> {nameof(StructuredPrompt)} >> no messages to send");

            if(typeof(T).IsAssignableTo(typeof(StructuredOutput)))
            {
                var requestModel = Activator.CreateInstance(typeof(T)) as StructuredOutput;

                if (string.IsNullOrEmpty(systemGuidance))
                    systemGuidance = requestModel.ToSystemMessage();

                else systemGuidance += $"\n{requestModel.ToSystemMessage()}";
            }

            if (options == null)
                options = _settings;

            var request = new ChatRequest
            {
                Model = string.IsNullOrEmpty(model) ? _settings.DefaultModel : model,
                Messages = !string.IsNullOrEmpty(systemGuidance) ? [new Message { Role = ChatRole.System, Content = systemGuidance }, new Message { Role = ChatRole.User, Content = prompt}] : [new Message { Role = ChatRole.User, Content = prompt }],
                Format = JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(T), new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true }),
                Options = options,
                Stream = false
            };

            if (!_handler.IsProvider(provider) && !_handler.IsSolvingTools)
                _handler.UpdateHandler(provider);
            
            string llmResponse = await _handler.GetLlmResponse(request);

            return JsonSerializer.Deserialize<T>(llmResponse);
        }

        public async Task<T> CommandPrompt<T>(GenerateRequest request, CommandPromptValidation<T>? validation = null, string provider = "ollama", bool withJsonInfo = true) where T : class
        {
            string llmResponse = string.Empty;
            int validations = validation != null ? validation.Validations : 0;
            for (int i = 0; i < validations + 1; i++)
            {
                try
                {
                    if (string.IsNullOrEmpty(request.Prompt))
                        throw new InvalidDataException($"{nameof(OllamaInferenceService)} >> {nameof(CommandPrompt)} >> no messages to send");

                    if (i == 0 && withJsonInfo && typeof(T).IsAssignableTo(typeof(StructuredOutput)))
                    {
                        var requestModel = Activator.CreateInstance(typeof(T)) as StructuredOutput;

                        if (string.IsNullOrEmpty(request.System))
                            request.System = requestModel.ToSystemMessage();

                        else request.System += $"\n\n{requestModel.ToSystemMessage()}";
                    }

                    request.Format = JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(T), new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true });
                    request.Stream = false;

                    if (string.IsNullOrEmpty(request.Model))
                        request.Model = !string.IsNullOrEmpty(_handler.ScopedModel) ? _handler.ScopedModel : _settings.DefaultModel; //TODO: getDefaultModelForProvider(provider) request.Model = _provider.IsSolvingTools ? _provider.CurrentModel : _settings.DefaultModelByProvider(_provider.Name)

                    if (!_handler.IsOfType<OllamaHandler>() && provider != "ollama" && !_handler.IsSolvingTools)
                    {
                        _handler = _handler.UpdateHandler(provider);

                        llmResponse = await _handler.GetLlmResponse(request.GenerateRequestToChat());                        
                    }

                    else llmResponse = await _handler.AsType<OllamaHandler>().GenerateLlmResponse(request);

                    //has no default | db message. Orchestrates commands with default | db message. uses the ChromaCommands FactoryMethod to get a ChromaRepo for the child commands
                    if (validation != null && validations > 0)
                        return await validation.Validator.Prompt(new JsonRefineRequest<T> { ValidatedPrompt = request.Prompt, SystemMessage = request.System, ValidationType = validation.ValidationType,  RawOutput = llmResponse, UseChatEndpoint = validation.UseChatEndpoint });
                    
                }
                catch(JsonOutputValidationException ex)
                {
                    if (i == validations)
                        throw new PromptRetryException($"{nameof(CommandPrompt)} >> JSON OUTPUT VALIDATIONS LIMIT REACHED", retries: validations);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return string.IsNullOrEmpty(llmResponse) ? default(T) : JsonSerializer.Deserialize<T>(llmResponse);
        }

        public async Task<T> CommandPrompt<T>(ChatRequest chatRequest, CommandPromptValidation<T>? validation = null, string provider = "ollama", Dictionary<string, MethodInfo>? requestTools = null, bool withJsonInfo = true) where T : class
        {
            string llmResponse = string.Empty;
            int validations = validation != null ? validation.Validations : 0;
            for (int i = 0; i < validations + 1; i++)
            {
                try
                {
                    if (chatRequest.Messages.Count() == 0)
                        throw new InvalidDataException($"{nameof(OllamaInferenceService)} >> {nameof(CommandPrompt)} >> no messages to send");

                    var sysmsg = chatRequest.Messages.FirstOrDefault(m => m.Role == ChatRole.System);
                    var usermsg = chatRequest.Messages.LastOrDefault(m => m.Role == ChatRole.User);

                    if (i == 0 && withJsonInfo && typeof(T).IsAssignableTo(typeof(StructuredOutput)))
                    {
                        var requestModel = Activator.CreateInstance(typeof(T)) as StructuredOutput;

                        sysmsg.Content += $"\n\n{requestModel.ToSystemMessage()}";
                    }

                    chatRequest.Format = JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(T), new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true });
                    chatRequest.Stream = false;

                    if (string.IsNullOrEmpty(chatRequest.Model))
                        chatRequest.Model = !string.IsNullOrEmpty(_handler.ScopedModel) ? _handler.ScopedModel : _settings.DefaultModel; //TODO: getDefaultModelForProvider(provider) request.Model = _provider.IsSolvingTools ? _provider.CurrentModel : _settings.DefaultModelByProvider(_provider.Name)

                    if (!_handler.IsProvider(provider) && !_handler.IsSolvingTools)
                        _handler = _handler.UpdateHandler(provider);

                    llmResponse = await _handler.GetLlmResponse(chatRequest, requestTools);

                    //has no default | db message. Orchestrates commands with default | db message. uses the ChromaCommands FactoryMethod to get a ChromaRepo for the child commands
                    if (validation != null && validations > 0)
                        return await validation.Validator.Prompt(new JsonRefineRequest<T> { ValidatedPrompt = usermsg.Content, SystemMessage = sysmsg.Content, ValidationType = validation.ValidationType, RawOutput = llmResponse, UseChatEndpoint = validation.UseChatEndpoint });

                }
                catch (JsonOutputValidationException ex)
                {
                    if (i == validations)
                        throw new PromptRetryException($"{nameof(CommandPrompt)} >> JSON OUTPUT VALIDATIONS LIMIT REACHED", retries: validations);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return string.IsNullOrEmpty(llmResponse) ? default(T) : JsonSerializer.Deserialize<T>(llmResponse);
        }

    }
}
