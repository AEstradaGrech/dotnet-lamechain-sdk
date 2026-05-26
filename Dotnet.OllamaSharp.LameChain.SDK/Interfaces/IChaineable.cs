using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects.Outputs;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Steps;

namespace Dotnet.OllamaSharp.LameChain.SDK.Interfaces
{
    public interface IChaineable
    {
        //Accessors / read config
        Guid Id { get; }
        bool IsMultiSocket { get; }
        public bool IsForged { get; }
        public ChainRunner? Runner { get; }
        public PromptCommandRequest Request { get; }
        public bool IsRunning { get; }
        public IChaineable Previous { get; }
        public IChaineable Next { get; }
        public List<IJsoneable> Commands { get; }
        public List<string> InstructionsLog { get; }
        public string? PromptedInstruction { get; }
        public string? FeedForwardInstruction { get; }
        public string Input { get; }
        public List<ChainLink> Outputs { get; }

        // Checkers and delegate friendly type accesors

        // Method version accessors to allow the deferred chaining of steps 
        Guid GetRunnerId();
        // You can't feed a subChain from the owning step in the same extension
        // because the subchain is created first as a variable of a owning step
        // that doesn't exists yet (hence, it cannot be outted, cannot be exposed later because
        // the subchain cannot store the id of an unexistent step. So, for that cases
        // you have to store 'WhoIsPrevious' as a delegate in Feeds && NestedFeeds and the Id
        // will be retrieven on chain runtime
        Guid WhoIsPrevious();
        Guid WhoIsNext();
        public IChaineable GetFirstStep();
        public IChaineable GetLastStep();

        bool IsFirstStep();
        bool IsFirstSubstep();
        bool IsReady();
        bool IsChained(bool? checkNextOnly = true);

        // Config & execution
        public void WithChainFeeds(List<Func<Guid>> stepIds);
        public void BoostWith(List<string> feeds, string? feedMsg);
        public bool CanBeForged(IChaineable previous);
        public void Link(IChaineable next, bool isForward, bool isTwoWay = false);
        Task<IChaineable> Forge(IChaineable previous);
        ChainRunner Drop();

        // Read Output
        TDeserialized GetOutputAs<TDeserialized>() where TDeserialized : class;
        public Dictionary<Guid, List<ChainLink>> GrouppedOutputs();

        // ChainRunner Events
        void SendReplay();
        IChaineable OnRunnerCall();
        void OnRunnerSupport(Guid id);
        void FollowRunner(IChaineable current);

        //Factory methods
        TStep ExpandTo<TStep>(IJsoneable command, StepSettings settings, string? feedForwardInstruction = null) where TStep : ChainStep;
        SingleThrowStep ExpandTo(IJsoneable command, StepSettings settings, string? feedForwardInstruction = null);
        SplitterStep Plug(List<StepInstruction> commands, StepSettings settings, string? splitterFeedFwd = null);
        TStep ExpandTo<TStep, TCommand, TResult>(string instruction, CommandSettings commandSettings, StepSettings settings, string? feedForwardInstruction = null) 
            where TCommand : BasePromptCommand<TResult>, new() where TStep : ChainStep;
        SingleThrowStep ExpandTo<TCommand, TResult>(string instruction, CommandSettings commandSettings, StepSettings settings, string? feedForwardInstruction = null) 
            where TCommand : BasePromptCommand<TResult>, new();
    }
}
