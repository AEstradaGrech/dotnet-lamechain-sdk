using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Steps;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Step
{
    // A StashedStep is a step that executes a IRetrievable* and Stores the result to serve as common source of data to read from. 
    // They store datasets as Boosters with title (contextual info about what is the dataset) and the TextContents
    // A StashedStep can be configured to feed the next by default or not (you must configure a feed from other step to use it)
    // A StashedStep can also store the results of selected previous steps using the .FeedFrom([stepIds]).
    // The recieve the pass to read any produced feed so far and subscribes to the _runner.OnStepForged to get the new chain Feeds

    //*ISourceable should be a command that executes some logic and returns List<Text>, whether it is
    // a SimilaritySearch or a WebSearch (encapsulating _langSearch, for example) or any kind of command whose output is meant
    // to be a source of data to analyze, review ... add to LLM context. A StashedStep is the recipient for a ISourceable and serves as source for other steps using .FeedFrom(stashStepId)
    // example: let's say you have a command that generates N reports based on its previous (custom) chain steps (so, the ISourceable command recieves 1 || N inputs, anlyzes-processes with llm and produces N reports) and you want to use
    //          those N text the ISourceable produces to Feed many other stes / processes later in the chain. You would use .Stash(ISourceable cmd, out stashId) to create and expose the stash and then .FeedFrom(stashId) in any other substep you want)
    public class StashedStep : SingleThrowStep
    {
        //A greedy stash won't lend it's stashed texts to the nextOne by default
        // You must setup a .FeedFrom(stashId) to read the content
        private bool _isGreedy = false;

        //The stash WON'T USE the previous output in its process
        private bool _isIsolated = true;
        public bool IsGreedy => _isGreedy;
        public bool IsEmpty => _stepSettings.Boosters.Count == 0;
        
        public StashedStep(SourceableCommand retrieverCommand, StepSettings settings, bool isGreedy = false, bool isIsolated = true, string? feedForwardMessage = null) : base(null, feedForwardMessage)
        {
            _isGreedy = isGreedy;
            _isIsolated = isIsolated;
        }

        public override bool CanBeForged(IChaineable previous) => IsReady() && IsChained(isForwardCheck: true);

        public override async Task<IChaineable> Forge(IChaineable previous)
        {
            if (!hasCatchedThrow(previous))
                throw new InvalidOperationException($"{nameof(StashedStep)} >> {nameof(Forge)} >> An error has occured while revieving the ChainRunner from the previous step >> INVALID CHAIN RUN");
            
            if(!_isIsolated)
            {
                // append previous output
            }
            
            await forgeLink();

            var result = _outputs.First();

            var sources = GetOutputAs<List<string>>();

            return await _next.Forge(this);
        }
    }
}
