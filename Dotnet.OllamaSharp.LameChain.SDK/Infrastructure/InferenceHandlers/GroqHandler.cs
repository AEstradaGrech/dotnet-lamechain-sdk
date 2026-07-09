using Dotnet.OllamaSharp.LameChain.SDK.Extensions.Model;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp.Models.Chat;
using System.Reflection;
using System.Text.Json;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.InferenceHandlers
{
    public class GroqHandler : BaseHandler
    {
        private IGroqClient _client;
        public GroqHandler(IServiceProvider serviceProvider, IConfiguration config) 
            : base(serviceProvider, config, "groq")
        {
            _client = serviceProvider.GetRequiredService<IGroqClient>();

            if (_client == null)
                throw new ArgumentNullException($"{nameof(GroqHandler)} >> {nameof(IGroqClient)} is not registered as service.");
        }

        public override async Task<string> GetLlmResponse(ChatRequest request, Dictionary<string, MethodInfo>? requestTools = null)
        {
            if (!isValid())
                throw new InvalidOperationException($"{GetLlmResponse} >> {nameof(isValid)}");

            var response = await _client.GetChatCompletion(request.AsGroqRequest());

            if (response.Choices.Count == 0)
                throw new InvalidDataException($"{nameof(GroqHandler)}.{nameof(GetLlmResponse)}.{nameof(_client.GetChatCompletion)} >> No message choices present in the response");

            var responseMessage = response.Choices.FirstOrDefault().Message;
            
            if (responseMessage.ToolCalls != null && responseMessage.ToolCalls.Count() > 0)
                return await handleFunctionCall<ChatRequest, Message>(request, responseMessage, requestTools);

            return response.Choices.FirstOrDefault().Message.Content.Trim();
        }

        protected override List<Message> getToolResponseMessages(string toolName, Message toolRequestMessage, object? toolResult)
        {
            var messages = new List<Message>();

            var toolResponseMessage = new Message(ChatRole.Tool, JsonSerializer.Serialize(toolResult)); // now serializes the unwrapped value, not a Task

            toolResponseMessage.ToolName = toolName;
            
            // For GroqTools the API requires the ToolCallId to be present in the request 'Tool' message.
            // The GroqMessageConverter will add that property but it needs a copy of the ToolCalls from
            // the tool invoke message that stores the Groq tool Id
            toolResponseMessage.ToolCalls = toolRequestMessage.ToolCalls;

            messages.Add(toolRequestMessage);

            messages.Add(toolResponseMessage);

            return messages;
        }

        protected override bool isValid()
            => base.isValid() && _client != null;
    }
}
