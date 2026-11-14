
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
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
            
            // The base class is enough to check the boolean, any info from a subclass (like a ScoredBoolResponse)
            // will be added to the chain context anyway
            var evaluation = GetOutputAs<ReasonedBoolResponse>();

            if (evaluation.Answer)
            {
                //Link the end of the subchaine with the next step of the main chain
                TrueBranchRunner.GetLastStep().Link(_next, isForward: true, isTwoWay: true);
                //Link this step with the first step of the subchain
                Link(TrueBranchRunner, isForward: true, isTwoWay: true);
            }
            //Chain again
            return await _next.Forge(this);
        }
    }
}
