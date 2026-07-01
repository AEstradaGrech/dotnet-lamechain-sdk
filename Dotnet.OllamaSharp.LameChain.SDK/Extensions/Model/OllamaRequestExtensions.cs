using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Request;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System.Text.Json;
using ClaudeMessage = Anthropic.SDK.Messaging.Message;
using OllamaMessage = OllamaSharp.Models.Chat.Message;

namespace Dotnet.OllamaSharp.LameChain.SDK.Extensions.Model
{
    public static class OllamaRequestExtensions
    {
        public static MessageParameters AsClaudeRequest(this ChatRequest ollamaRequest)
        {
            if (ollamaRequest.Messages.Count() == 0)
                throw new InvalidOperationException($"{nameof(ChatRequest)}.{nameof(AsClaudeRequest)} >> No messages present in the request");

            if (ollamaRequest.Options == null)
                ollamaRequest.Options = new RequestOptions();

            var claudeReq = new MessageParameters
            {
                Messages = [],
                System = [],
                MaxTokens = ollamaRequest.Options.NumPredict ?? 1024,
                Model = AnthropicModels.Claude4Sonnet,
                Stream = ollamaRequest.Stream,
                Temperature = (decimal)(ollamaRequest.Options.Temperature ?? .7f),
                TopP = (decimal)(ollamaRequest.Options.TopP ?? .6f),
                TopK = ollamaRequest.Options.TopK ?? 10
            };

            foreach (var ollamaMessage in ollamaRequest.Messages)
            {
                if (string.IsNullOrEmpty(ollamaMessage.Content)) continue;

                if (ollamaMessage.Role == ChatRole.System)
                    claudeReq.System.Add(new SystemMessage(ollamaMessage.Content));

                else claudeReq.Messages.Add(new ClaudeMessage(ollamaMessage.Role == ChatRole.User ? RoleType.User : RoleType.Assistant, ollamaMessage.Content));
            }

            return claudeReq;
        }

        public static GroqChatRequest AsGroqRequest(this ChatRequest req, bool includeReasoning = false)
        {
            if (req.Messages.Count() == 0)
                throw new InvalidOperationException($"{nameof(ChatRequest)}.{nameof(AsClaudeRequest)} >> No messages present in the request");

            if (req.Options == null)
                req.Options = new RequestOptions();

            var request = new GroqChatRequest
            {
                Model = req.Model,
                Temperature = req.Options.Temperature ?? .7f,
                FrequencyPenalty = req.Options.FrequencyPenalty = 0,
                PresencePenalty = req.Options.PresencePenalty ?? .0f,
                MaxCompletionTokens = req.Options.NumPredict,
                TopP = req.Options.TopP ?? 1.0f,
                Stream = req.Stream,
                IncludeReasoning = includeReasoning,
                ResponseFormat = new
                {
                    type = "json_schema",
                    json_schema = new
                    {
                        name = "response",
                        schema = req.Format,
                        strict = true
                    }
                }
            };

            return request;
        }

        public static ChatRequest GenerateRequestToChat(this GenerateRequest req)
        {
            if (string.IsNullOrEmpty(req.System) && string.IsNullOrEmpty(req.Prompt))
                throw new InvalidOperationException($"{nameof(GenerateRequest)}.{nameof(GenerateRequestToChat)} >> No system or user prompt found in request");

            ChatRequest chatRequest = new ChatRequest 
            { 
                Model = req.Model,
                Options = req.Options,
                KeepAlive = req.KeepAlive,
                Template = req.Template,
                Format = req.Format,
                Stream = req.Stream,
                Think = req.Think,
                Messages = []
            };

            var messages = new List<OllamaMessage>();

            if(!string.IsNullOrEmpty(req.System))
                messages.Add(new OllamaMessage(ChatRole.System, req.System));
            
            if(!string.IsNullOrEmpty(req.Prompt))
                messages.Add(new OllamaMessage(ChatRole.User, req.Prompt));

            chatRequest.Messages = messages;

            return chatRequest;
        }
    }
}
