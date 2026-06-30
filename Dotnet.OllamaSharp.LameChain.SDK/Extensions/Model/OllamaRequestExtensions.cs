using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using OllamaSharp.Models.Chat;
using ClaudeMessage = Anthropic.SDK.Messaging.Message;

namespace Dotnet.OllamaSharp.LameChain.SDK.Extensions.Model
{
    public static class OllamaRequestExtensions
    {
        public static MessageParameters AsClaudeRequest(this ChatRequest ollamaRequest)
        {
            if (ollamaRequest.Messages.Count() == 0)
                throw new InvalidOperationException($"{nameof(ChatRequest)}.{nameof(AsClaudeRequest)} >> No messages present in the request");

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
    }
}
