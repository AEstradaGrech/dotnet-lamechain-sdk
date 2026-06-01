using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands
{
    public class ChatCommandRequest : PromptCommandRequest
    {
        public ChatCommandRequest() : base() { }
        public ChatCommandRequest(string message, string? guidanceMessage, bool isGuidanceAppend = false, string? model = null) : base(message, guidanceMessage, isGuidanceAppend, model) { ChatHistory = new List<ChatMessage>(); }
        public ChatCommandRequest(bool includeSystem, List<ChatMessage> messages, string message, string? model = null) : base(message, model)
        {
            ChatHistory = messages;
            IncludeSystemMessage = includeSystem;
        }
        public List<ChatMessage> ChatHistory { get; set; } = new List<ChatMessage>();
        public bool IncludeSystemMessage { get; set; }

        /// <summary>
        /// In this override the original command system message should be already in the ChatHistory, always in the first place
        /// </summary>
        /// <param name="systemUpdate"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        public override ChatRequest ToOllamaChat(string systemUpdate, CommandSettings settings = null)
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
                Stream = false,
                Options = settings.ToOllamaRequest() ?? new RequestOptions()
            };
        }
    }
}
