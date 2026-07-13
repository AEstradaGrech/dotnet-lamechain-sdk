using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System.Reflection;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.InferenceHandlers
{
    public class OllamaHandler : BaseHandler
    {
        private IOllamaApiClient _client;
        public OllamaHandler(IServiceProvider provider, IConfiguration config, Action<string, string>? notifyAction = null) 
            : base(provider, config, "ollama", notifyAction) 
        {
            _client = provider.GetRequiredService<IOllamaApiClient>();
        }

        public override async Task<string> GetLlmResponse(ChatRequest request, Dictionary<string, MethodInfo>? requestTools = null)
        {
            if (!isValid())
                throw new InvalidOperationException($"{nameof(OllamaHandler)} >> {nameof(GetLlmResponse)} >> {nameof(isValid)}");

            if (request.Messages.Count() == 0)
                throw new InvalidDataException($"{GetType().Name} >> {nameof(GetLlmResponse)} >> No messages present in the request");

            _requestModel = request.Model;

            notifyRequest(_requestModel, request.Messages.Last().Content);

            var sb = new StringBuilder();

            await foreach (var part in _client.ChatAsync(request))
            {
                if(part?.Message.ToolCalls != null && part?.Message.ToolCalls.Count() > 0)
                {
                    var toolsLoopResponse = await handleFunctionCall<ChatRequest, Message>(request, part?.Message, requestTools);

                    sb.Append(toolsLoopResponse);
                }
                else
                {
                    if (!string.IsNullOrEmpty(part?.Message.Thinking))
                        notifyThinking(request.Model, part?.Message.Thinking);

                    if (!string.IsNullOrEmpty(part?.Message.Content))
                        sb.Append(part.Message.Content);
                }
            }
                        
            return sb.ToString().Trim();
        }

        public async Task<string> GenerateLlmResponse(GenerateRequest request)
        {
            if (_client == null)
                throw new ArgumentNullException($"{nameof(OllamaHandler)}.{nameof(GenerateLlmResponse)} >> {nameof(IOllamaApiClient)} is null");

            if (string.IsNullOrEmpty(request.System) && string.IsNullOrEmpty(request.Prompt))
                throw new InvalidDataException($"{nameof(OllamaHandler)}.{nameof(GenerateLlmResponse)} >> No system message or user prompt present in the request");

            _requestModel = request.Model;

            var sb = new StringBuilder();

            await foreach (var part in _client.GenerateAsync(request))
                if (!string.IsNullOrEmpty(part?.Response))
                    sb.Append(part.Response);

            return sb.ToString().Trim();
        }
        
        protected override bool isValid()
            => base.isValid() && _client != null;
    }
}
