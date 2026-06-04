using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Evaluators;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Steps;
using System.Linq.Expressions;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Step
{
    /// <summary>
    /// Triggers the execution of a subchain if a certain condition is met on the evaluated type
    /// </summary>
    /// <typeparam name="TEvaluated"></typeparam>
    public class ConditionalStep : SingleThrowStep
    {
        //private Expression<Func<TEvaluated, bool>> _condition;
        public IChaineable TrueBranchRunner;
        private Func<bool> _condition;
        public ConditionalStep() : base() { }


        public ConditionalStep(Expression<Func<bool>> evaluator, StepSettings stepSettings, string? feedFwdInstruction = null) : base(stepSettings, feedFwdInstruction)
        {
            _condition = evaluator.Compile();
        }

        public void IfTrueThen(IChaineable trueBranch)
        {
            TrueBranchRunner = trueBranch;
        }

        public override bool CanBeForged(IChaineable previous)
            => IsRunning && IsChained(isForwardCheck: false) && TrueBranchRunner != null && _condition != null;

        protected override async Task runStep(IChaineable previous)
        {
            onRunBegin(previous);

            notify($"{nameof(runStep)}");
            
            //Copy outputs to pass to the truebranch, the next one or return them as chain END (04/06/26 not it is valid)
            _outputs.AddRange(previous.Outputs);

            if (_condition())
            {
                notify($"{nameof(runStep)} >> CONDITION PASSED >> SWAPPING CHAINS");

                if (IsChained(isForwardCheck: true))
                    TrueBranchRunner.GetLastStep().Link(_next, isForward: true, isTwoWay: true);

                Link(TrueBranchRunner, isForward: true, isTwoWay: true);
            }

            notify($"{nameof(runStep)} >> CONDITION NOT PASSED >> CONTINUEING CHAIN");
        }
    }
}
