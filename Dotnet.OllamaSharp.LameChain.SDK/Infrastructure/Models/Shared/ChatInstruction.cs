using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared
{
    public class ChatInstruction : Instruction
    {
        public ChatInstruction(string prompt, string? systemMessage = null, CommandSettings? settings = null) : base(prompt, systemMessage, settings) {}

        public ChatInstruction(string prompt, List<ChatMessage> chatHistory, string? systemMessage = null, CommandSettings? settings = null) : base(prompt, systemMessage, settings) { ChatHistory = chatHistory; }

        public List<ChatMessage> ChatHistory { get; set; }

        public ChatInstruction ForChat(bool bWithSysmsgUpdate)
        {
            if (ChatHistory.Count == 0)
            {
                if (string.IsNullOrEmpty(SystemMessage))
                    SystemMessage = "You are a helpful assistant";

                ChatHistory.Add(new ChatMessage(ChatRole.System.ToString(), SystemMessage));
            }

            if (bWithSysmsgUpdate && !string.IsNullOrEmpty(SystemMessage))
                ChatHistory.Add(new ChatMessage(ChatRole.System.ToString(), SystemMessage));

            ChatHistory.Add(new ChatMessage(ChatRole.User.ToString(), Prompt));

            return this;
        }
    }
}
