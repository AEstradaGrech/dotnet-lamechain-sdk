using Dotnet.OllamaSharp.LameChain.SDK.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects.Outputs;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Steps
{
    public class JunctionStep : SingleThrowStep
    {
        public JunctionStep() : base() { }

        public JunctionStep(StepSettings settings) : base(settings) { }
        
        public override bool CanBeForged(IChaineable previous)
            => previous != null && previous.IsMultiSocket && _commands.Count > 0;

        protected override void appendPreviousContext(StringBuilder sb, IChaineable previous)
        {
            var castedPrev = (SplitterStep)previous;

            var outputs = previous.GrouppedOutputs();

            foreach (var key in outputs.Keys)
            {
                var forgedPrevious = castedPrev.GetForgedSubStep(key);

                // Build multi-socket message result (Join after Tap | Split)
                if (outputs[key].Count > 1)
                {
                    var originalStep = castedPrev.GetPluggedById(key);

                    sb.Append($"> GOAL: ")
                      .Append(originalStep.PromptedInstruction)
                      .AppendLine()
                      .AppendLine(string.IsNullOrEmpty(originalStep.FeedForwardInstruction) ? string.Empty : $"> FURTHER INSTRUCTIONS: {originalStep.FeedForwardInstruction}");

                    outputs[key].ForEach(output =>
                    {
                        appendOutputMessage(ref sb, output);

                        _instructionsLog.AddRange(previous.InstructionsLog);
                    });
                }
                else appendOutputMessage(ref sb, outputs[key].FirstOrDefault()); // build a single output result (Join after SPST | Pipe)
            }
        }

        private void appendOutputMessage(ref StringBuilder sb, ChainLink output)
        {
            if (output == null) return;

            sb.AppendLine()
              .AppendLine(!_stepSettings.WithFullContext ?
                output.SerializedResult :
                guidanceMessageFrom(
                    output.Instruction.Replace(preInstructionTag, "").Trim(),
                    output.SerializedResult,
                    output.ForwardGuidance
                )
              );
        }
    }
}
