using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects
{
    public class StepInstruction
    {
        public StepInstruction() { }
        public StepInstruction(IJsoneable command, StepSettings settings, string? feedFwd = null) 
        {
            Command = command;
            StepSettings = settings;
            FeedFwdInstruction = feedFwd;
        }
        public IJsoneable Command { get; }
        public StepSettings StepSettings { get; }
        public string? FeedFwdInstruction { get; }
    }
}
