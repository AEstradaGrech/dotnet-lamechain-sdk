using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared
{
    public class Instruction
    {
        public Instruction(string prompt, string? systemMessage, CommandSettings? settings = null)
        {
            Prompt = prompt;
            SystemMessage = systemMessage;
            Settings = settings;
        }

        public string Prompt { get; set; } // User prompt in chat or 'Actual instruction' (since it seems /generate produces better results when prompting a user request rather than using system message + 'Complete your instruction'
        public string? SystemMessage { get; set; } // maps to Command._systemMessage on instantiation;
        public CommandSettings? Settings { get; set; }
    }
}
