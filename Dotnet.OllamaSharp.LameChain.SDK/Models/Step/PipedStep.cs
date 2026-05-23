using Dotnet.OllamaSharp.LameChain.SDK.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Steps
{
    public class PipedStep : SplitterStep
    {
        public PipedStep() : base() { }
        public PipedStep(StepInstruction piped) : base(piped) { }

        public override bool CanBeForged(IChaineable previous)
            => previous != null && previous.IsMultiSocket && _commands.Count == 1;

        public override async Task<IChaineable> Forge(IChaineable previous)
        {
            if (!hasCatchedThrow(previous))
                throw new InvalidOperationException($"{nameof(SplitterStep)} >> {nameof(Forge)} >> {nameof(hasCatchedThrow)} >> An error has occured while passing the runner. STEP CANNOT BE FORGED");

            var castedPrev = (SplitterStep)previous;

            var outputs = castedPrev.GrouppedOutputs();

            var prevsMap = new Dictionary<Guid, IChaineable>();

            outputs.Keys.ToList().ForEach(key =>
            {
                var origin = castedPrev.GetPluggedById(key);

                outputs[key].ForEach(link =>
                {
                    var branch = ThrowTo(swapRunner: true, _commands.First(), _stepSettings, _feedForwardInstruction);

                    _branches.Add(branch);

                    prevsMap.Add(branch.Id, origin);
                });
            });

            processBranchResults(await Task.WhenAll(_branches.Select(branch => Task.Run(() => {
                branch.Link(prevsMap[branch.Id], isForward: false, isTwoWay: false);
                return branch.Forge(prevsMap[branch.Id]);  
            }))));

            handleNonSplittedStepData(previous.Id);

            submitForgeLog();

            return await _next.Forge(this);
        }
    }
}
