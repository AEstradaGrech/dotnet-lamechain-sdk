using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Steps;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Step
{
    public class SmartConditionalStep : SingleThrowStep
    {
        public IChaineable TrueBranchRunner;

        public SmartConditionalStep() : base() { }

        public SmartConditionalStep(StepSettings settings) : base(settings) { }
  
        public void IfTrueThen(IChaineable trueBranch)
        {
            TrueBranchRunner = trueBranch;
        }

        public override bool CanBeForged(IChaineable previous)
            => IsReady() && IsChained(isForwardCheck: true) && TrueBranchRunner != null;

        public override async Task<IChaineable> Forge(IChaineable previous)
        {
             await runStep(previous);

            if (Outputs.Count == 0)
                throw new InvalidOperationException($"{nameof(SmartConditionalStep)} >> {nameof(Forge)} >> An error has occured while running the EvaluatorCommand >> INVALID CHAIN RUN");
            
            var evaluation = GetOutputAs<ScoredBoolResponse>();

            if (evaluation.Answer)
            {
                TrueBranchRunner.GetLastStep().Link(_next, isForward: true, isTwoWay: true);

                Link(TrueBranchRunner, isForward: true, isTwoWay: true);
            }
            //Chain again
            return await _next.Forge(this);
        }
    }
}
