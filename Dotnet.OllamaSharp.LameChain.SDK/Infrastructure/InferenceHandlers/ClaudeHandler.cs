using Anthropic.Models.Messages;
using Dotnet.OllamaSharp.LameChain.SDK.Extensions.Model;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Clients;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System.Reflection;
using System.Text.Json;
using AIMessage = Microsoft.Extensions.AI.ChatMessage;
using AIRole = Microsoft.Extensions.AI.ChatRole;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.InferenceHandlers
{
    public class ClaudeHandler : BaseHandler
    {
        private readonly IClaudeClient _client;
        public ClaudeHandler(IServiceProvider provider, IConfiguration config, Action<string, string>? notifyAction = null) 
            : base(provider, config, "anthropic", notifyAction) 
        {
            _client = provider.GetRequiredService<IClaudeClient>();
        }

        public override async Task<string> GetLlmResponse(ChatRequest request, Dictionary<string, MethodInfo>? requestTools = null)
        {
            if (!isValid())
                throw new InvalidOperationException($"{GetLlmResponse} >> {nameof(isValid)}");

            if (request.Messages.Count() == 0)
                throw new InvalidDataException($"{GetType().Name} >> {nameof(GetLlmResponse)} >> No messages present in the request");

            _requestModel = request.Model;

            notifyRequest(_requestModel, request.Messages.Last().Content);

            var response = await _client.GetResponseAsync(request.Messages.ToChatMessages(), request.ToClaudeChatClientRequest(mapRequestTools(requestTools), allowParallelToolCall: false, Effort.High));

            if (response.FinishReason.Value == ChatFinishReason.ToolCalls)
            {
               return await handleFunctionCall<ChatRequest, AIMessage>(request, response.Messages.FirstOrDefault(), requestTools);
            }
            else
            {
                if (response.Messages.FirstOrDefault().Contents.OfType<TextReasoningContent>().Any())
                    notifyThinking(request.Model, string.Concat(response.Messages.FirstOrDefault().Contents.OfType<TextReasoningContent>().Select(x => x.Text)));

                var llmResponse = response.Messages.FirstOrDefault().Text;

                if(request.Format != null)
                {
                    if (llmResponse.StartsWith("```json"))
                        llmResponse = llmResponse.Replace("```json", "").Replace("```", "").Trim();
                }

                return llmResponse;
            }
        }

        protected override bool isValid()
            => base.isValid() && _client != null;
        
        protected async Task<string> handleFunctionCall<TReq, TMessage>(TReq request, TMessage message, Dictionary<string, MethodInfo>? toolsLookup)
            where TReq : class
            where TMessage : class
        {
            _isSolvingTools = true;

            validateFunctionCallMessage<TMessage>(message, toolsLookup);

            var claudeRequest = request as ChatRequest;

            var llmMessage = message as AIMessage;

            var toolCallMessage = llmMessage.Contents.Where(x => x.GetType().IsAssignableTo(typeof(FunctionCallContent))).FirstOrDefault() as FunctionCallContent;

            notifyToolCall(claudeRequest.Model, toolCallMessage.Name);

            var method = toolsLookup[toolCallMessage.Name];

            var toolResult = await getToolResult(method, toolCallMessage.Arguments);

            var messages = claudeRequest.Messages.ToList();

            //Map ChatMessage to Ollama.Message with ToolRequest
            var toolRequestMessage = llmMessage.ToOllamaMessage();
            
            // use the SDK methods to update the request with the tool chat turns
            messages.AddRange(getToolResponseMessages(toolCallMessage.Name, toolRequestMessage , toolResult));
            
            claudeRequest.Messages = messages;
            // send it again in ollama format so it maps again to Microsoft models and loop recursively
            var toolExecutionResponse = await GetLlmResponse(claudeRequest, toolsLookup);

            _isSolvingTools = false;

            return toolExecutionResponse;
        }


        protected override void validateFunctionCallMessage<T>(T message, Dictionary<string, MethodInfo>? toolsLookup) where T : class
        {
            var llmMessage = message as AIMessage;

            if (llmMessage.Contents.Count() == 0)
                throw new InvalidDataException($"{nameof(handleFunctionCall)} >> {nameof(AIMessage)} content is empty");

            if (!(llmMessage.Contents.Any(x => x.GetType() == typeof(FunctionCallContent))))
                throw new InvalidDataException($"{nameof(handleFunctionCall)} >> {llmMessage} has no content of type {nameof(FunctionCallContent)}");

            var toolCallMessage = llmMessage.Contents.Where(x => x.GetType().IsAssignableTo(typeof(FunctionCallContent))).FirstOrDefault() as FunctionCallContent;

            if (!toolsLookup.ContainsKey(toolCallMessage.Name))
                throw new InvalidOperationException($"{nameof(handleFunctionCall)} >> {toolCallMessage.Name} is not present in the toolsLookup dictionary");
        }

        private List<AITool> mapRequestTools(Dictionary<string, MethodInfo>? toolsLookup)
        {
            if (toolsLookup == null) return [];

            var tools = new List<AITool>();

            toolsLookup.Keys.ToList().ForEach(key => tools.Add(OllamaTools.ToAIFunction(toolsLookup[key], _serviceProvider)));

            return tools;
        }
    }
}
