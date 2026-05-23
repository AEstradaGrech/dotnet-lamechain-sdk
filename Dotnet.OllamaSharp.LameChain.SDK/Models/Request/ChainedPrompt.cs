using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Request
{
    public class ChainedPrompt : Instruction
    {
        public ChainedPrompt(string prompt, string? systemMessage = null, CommandSettings? settings = null) : base(prompt, systemMessage, settings) {}
        public ChainedPrompt(string prompt, List<Instruction> instructions, string? systemMessage = null, CommandSettings? settings = null) 
            : this(prompt, systemMessage, settings)
        {
            Instructions = instructions;
        }
        //settings
        public bool WithReport { get; set; }
        public bool WithFinalMessage { get; set; }
        public string? FinalSystemMessage { get; set; } = string.Empty;
        public CommandSettings? FinalMessageSettings { get; set; } = null; // ?? Default (Input)
        
        public List<Instruction> Instructions { get; set; } = new List<Instruction>(); // TODO: ChainInstruction w/building logic
    }
}
