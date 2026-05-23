
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Requests
{
    public class ChatCommandRequest : PromptCommandRequest
    {
        public ChatCommandRequest() { }
        public ChatCommandRequest(bool includeSystem, List<ChatMessage> messages, string message, RequestOptions? settings, string? model = null) : base(message, settings, model)
        {
            ChatHistory = messages;
            IncludeSystemMessage = includeSystem;
        }
        public List<ChatMessage> ChatHistory { get; set; } = new List<ChatMessage>();
        public bool IncludeSystemMessage { get; set; }

        public ChatRequest ToOllama(string? systemUpdate = null, bool isStream = false)
        {
            if (IncludeSystemMessage && !string.IsNullOrEmpty(systemUpdate))
                ChatHistory.Add(new ChatMessage(ChatRole.System.ToString(), systemUpdate));

            ChatHistory.Add(new ChatMessage(ChatRole.User.ToString(), Prompt));

            var messages = new List<Message>();

            ChatHistory.ForEach(x => messages.Add(new Message(new ChatRole(x.Role), x.Content)));

            return new ChatRequest
            {
                Model = Model,
                Messages = messages,
                Stream = isStream,
                Options = Settings
            };
        }
    }
}
