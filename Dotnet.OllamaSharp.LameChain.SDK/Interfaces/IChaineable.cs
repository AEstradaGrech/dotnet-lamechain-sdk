using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Requests;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects.Outputs;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Steps;

namespace Dotnet.OllamaSharp.LameChain.SDK.Interfaces
{
    public interface IChaineable
    {
        Guid Id { get; }
        bool IsChained(bool? checkNextOnly = true);
        bool IsFirstStep();
        bool IsFirstSubstep();
        public bool CanBeForged(IChaineable previous);
        bool IsMultiSocket { get; }
        public bool IsForged { get; }
        public ChainRunner? Runner { get; }
        public PromptCommandRequest Request { get; }
        public bool IsRunning { get; }
        public void Link(IChaineable next, bool isForward, bool isTwoWay = false);
        Task<IChaineable> Forge(IChaineable previous);
        ChainRunner Drop();
        bool IsReady();
        void SendReplay();
        IChaineable OnRunnerCall();
        void OnRunnerSupport(Guid id);
        void FollowRunner(IChaineable current);
        TStep ExpandTo<TStep>(IJsoneable command, StepSettings settings, string? feedForwardInstruction = null) where TStep : ChainStep;
        SingleThrowStep ExpandTo(IJsoneable command, StepSettings settings, string? feedForwardInstruction = null);
        SplitterStep Plug(List<StepInstruction> commands, StepSettings settings, string? splitterFeedFwd = null);
        TStep ExpandTo<TStep, TCommand, TResult>(string instruction, CommandSettings commandSettings, StepSettings settings, string? feedForwardInstruction = null) 
            where TCommand : BasePromptCommand<TResult>, new() where TStep : ChainStep;
        SingleThrowStep ExpandTo<TCommand, TResult>(string instruction, CommandSettings commandSettings, StepSettings settings, string? feedForwardInstruction = null) 
            where TCommand : BasePromptCommand<TResult>, new();
        TDeserialized GetOutputAs<TDeserialized>() where TDeserialized : class;
        public void BoostWith(List<string> feeds, string? feedMsg);
        public IChaineable Previous { get; }
        public IChaineable Next { get; }
        public List<IJsoneable> Commands { get; }
        public List<string> InstructionsLog { get; }
        public string? PromptedInstruction { get; }
        public string? FeedForwardInstruction { get; }
        public string Input { get; }
        public List<ChainLink> Outputs { get; }
        public Dictionary<Guid, List<ChainLink>> GrouppedOutputs();
        public IChaineable GetFirstStep();
    }
}
