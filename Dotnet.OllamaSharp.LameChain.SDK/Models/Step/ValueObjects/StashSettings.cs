
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects
{
    public class StashSettings : StepSettings
    {
        public bool IsGreedy { get; }
        public bool IsIsolated { get; }
        public StashSettings(bool isGreedy, bool isIsolated) : base()
        {
            IsGreedy = isGreedy;
            IsIsolated = isIsolated;
        }

        public StashSettings(IJsoneable command, string? requestPrompt, string? feedFwd = null, bool isGreedy = false, bool isIsolated = true, bool withFullContext = true, bool withPrevSchema = false)
            : base(command, new PromptCommandRequest(requestPrompt ?? string.Empty), feedFwd, withFullContext, withPrevSchema)
        {
            IsGreedy = isGreedy;
            IsIsolated = isIsolated;
        }

        public StashSettings(IJsoneable command, PromptCommandRequest request, string? feedFwd = null, bool isGreedy = false, bool isIsolated = true, bool withFullContext = true, bool withPrevSchema = false) 
            : base(command, request, feedFwd, withFullContext, withPrevSchema) 
        {
            IsGreedy = isGreedy;
            IsIsolated = isIsolated;
        }

        public StashSettings(StepSettings cloned, bool cloneFeeds = true, bool cloneBoosters = true) : base(cloned, cloneFeeds, cloneBoosters) 
        { 
            if(cloned.GetType() == typeof(StashSettings))
            {
                IsGreedy = ((StashSettings)cloned).IsGreedy;
                IsIsolated = ((StashSettings)cloned).IsIsolated;
            }
        }
    }
}
