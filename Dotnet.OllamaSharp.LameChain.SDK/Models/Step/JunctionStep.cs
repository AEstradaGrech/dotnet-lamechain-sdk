using Dotnet.OllamaSharp.LameChain.SDK.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
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
                        // BUILD MESSAGE
                        sb.AppendLine()
                          .AppendLine(guidanceMessageFrom(forgedPrevious.PromptedInstruction.Replace(preInstructionTag, "").Trim(), output.SerializedResult, output.SchemaForMessage(), output.ForwardGuidance).Trim());

                        _instructionsLog.AddRange(previous.InstructionsLog);
                    });
                }
                else // build a single output result (Join after SPST | Pipe)
                {
                    var output = outputs[key].FirstOrDefault();

                    sb.AppendLine()
                      .AppendLine(guidanceMessageFrom(forgedPrevious.PromptedInstruction.Replace(preInstructionTag, "").Trim(), output.SerializedResult, output.SchemaForMessage(), output.ForwardGuidance).Trim());
                }
            }
        }
    }
}
