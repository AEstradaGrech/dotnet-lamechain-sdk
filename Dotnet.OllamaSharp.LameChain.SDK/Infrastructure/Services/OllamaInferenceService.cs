using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Validators;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Evaluators;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Exceptions;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System.Text.Json;
using System.Text.Json.Schema;

namespace DotnetLlamaSharp.Infrastructure.Services.Inference
{
    public class OllamaInferenceService : IOllamaInferenceService
    {
        private readonly IOllamaApiClient _client;
        private readonly OllamaSettings _settings;

        public OllamaInferenceService(IOllamaApiClient client, IOptions<OllamaSettings> settings)
        {
            _client = client;
            _settings = settings.Value;
        }
       
        public async Task<Message> GeneratePrompt(GenerateRequest request)
        {
            var sb = new System.Text.StringBuilder();

            request.Stream = false;

            await foreach (var part in _client.GenerateAsync(request))
                if (!string.IsNullOrEmpty(part?.Response))
                    sb.Append(part.Response);

           return new Message(ChatRole.Assistant.ToString(), sb.ToString());
        }

        public async Task<Message> ChatPrompt(ChatRequest request)
        {
            var sb = new System.Text.StringBuilder();

            await foreach (var part in _client.ChatAsync(request))
                if (!string.IsNullOrEmpty(part?.Message.Content))
                    sb.Append(part.Message.Content);

            return new Message(ChatRole.Assistant.ToString(), sb.ToString());
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

        public async Task<T> StructuredPrompt<T>(string prompt, string model, string? systemGuidance = null, RequestOptions? options = null) where T : class
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
                Model = model ?? _settings.DefaultModel,
                Messages = !string.IsNullOrEmpty(systemGuidance) ? [new Message { Role = ChatRole.System, Content = systemGuidance }, new Message { Role = ChatRole.User, Content = prompt}] : [new Message { Role = ChatRole.User, Content = prompt }],
                Format = JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(T)),
                Stream = false
            };
            
            var sb = new System.Text.StringBuilder();
          
            await foreach (var part in _client.ChatAsync(request))
                if (!string.IsNullOrEmpty(part?.Message.Content))
                    sb.Append(part.Message.Content);

            return JsonSerializer.Deserialize<T>(sb.ToString());
        }

        public async Task<T> CommandPrompt<T>(GenerateRequest request, int validations = 0, EPromptValidation type = EPromptValidation.REVIEW_ONLY, JsonOutputRefinerCommand<T> validator = null) where T : class
        {
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < validations + 1; i++)
            {
                try
                {
                    if (string.IsNullOrEmpty(request.Prompt))
                        throw new InvalidDataException($"{nameof(OllamaInferenceService)} >> {nameof(StructuredPrompt)} >> no messages to send");

                    if (typeof(T).IsAssignableTo(typeof(StructuredOutput)))
                    {
                        var requestModel = Activator.CreateInstance(typeof(T)) as StructuredOutput;

                        if (string.IsNullOrEmpty(request.System))
                            request.System = requestModel.ToSystemMessage();

                        else request.System += $"\n\n{requestModel.ToSystemMessage()}";
                    }

                    request.Format = JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(T));
                    request.Stream = false;

                    await foreach (var part in _client.GenerateAsync(request))
                        if (!string.IsNullOrEmpty(part?.Response))
                            sb.Append(part.Response);

                    //has no default | db message. Orchestrates commands with default | db message. uses the ChromaCommands FactoryMethod to get a ChromaRepo for the child commands
                    if (validations > 0 && validator != null)
                        return await validator.Prompt(new JsonRefineRequest<T> { Prompt = request.Prompt, SystemMessage = request.System, ValidationType = type,  RawOutput = sb.ToString(), Model = request.Model });
                    
                }
                catch(StructuredOutputException ex)
                {
                    if (i == validations)
                        throw new PromptRetryException($"{nameof(StructuredPrompt)} >> JSON OUTPUT VALIDATIONS LIMIT REACHED", retries: validations);
                }
                catch (Exception ex)
                {
                    if (ex.GetType() == typeof(PromptRetryException))
                        throw ex;

                    if (i == validations)
                        throw new PromptRetryException($"{nameof(StructuredPrompt)} >> JSON OUTPUT VALIDATIONS LIMIT REACHED", retries: validations);
                }
            }

            return JsonSerializer.Deserialize<T>(sb.ToString());
        }

        public async Task<T> CommandPrompt<T>(ChatRequest chatRequest, int validations = 0, EPromptValidation type = EPromptValidation.REVIEW_ONLY, JsonOutputRefinerCommand<T> validator = null) where T : class
        {
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < validations + 1; i++)
            {
                try
                {
                    if (chatRequest.Messages.Count() == 0)
                        throw new InvalidDataException($"{nameof(OllamaInferenceService)} >> {nameof(StructuredPrompt)} >> no messages to send");

                    var sysmsg = chatRequest.Messages.FirstOrDefault(m => m.Role == ChatRole.System);
                    var usermsg = chatRequest.Messages.LastOrDefault(m => m.Role == ChatRole.User);

                    if (typeof(T).IsAssignableTo(typeof(StructuredOutput)))
                    {
                        var requestModel = Activator.CreateInstance(typeof(T)) as StructuredOutput;

                        sysmsg.Content += $"\n\n{requestModel.ToSystemMessage()}";
                    }

                    chatRequest.Format = JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(T));
                    chatRequest.Stream = false;

                    await foreach (var part in _client.ChatAsync(chatRequest))
                        if (!string.IsNullOrEmpty(part?.Message.Content))
                            sb.Append(part.Message.Content);

                    //has no default | db message. Orchestrates commands with default | db message. uses the ChromaCommands FactoryMethod to get a ChromaRepo for the child commands
                    if (validations > 0 && validator != null)
                        return await validator.Prompt(new JsonRefineRequest<T> { Prompt = usermsg.Content, SystemMessage = sysmsg.Content, ValidationType = type, RawOutput = sb.ToString(), Model = chatRequest.Model });

                }
                catch (StructuredOutputException ex)
                {
                    if (i == validations)
                        throw new PromptRetryException($"{nameof(StructuredPrompt)} >> JSON OUTPUT VALIDATIONS LIMIT REACHED", retries: validations);
                }
                catch (Exception ex)
                {
                    if (ex.GetType() == typeof(PromptRetryException))
                        throw ex;

                    if (i == validations)
                        throw new PromptRetryException($"{nameof(StructuredPrompt)} >> JSON OUTPUT VALIDATIONS LIMIT REACHED", retries: validations);
                }
            }

            return JsonSerializer.Deserialize<T>(sb.ToString());
        }
    }
}
