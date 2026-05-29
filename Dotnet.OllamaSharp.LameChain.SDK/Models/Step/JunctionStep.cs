using Dotnet.OllamaSharp.LameChain.SDK.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Steps
{
    public class JunctionStep : SingleThrowStep
    {
        public JunctionStep() : base() { }

        public JunctionStep(IJsoneable junctionCommand, StepSettings request, string? feedFwdMessage = null) : base(junctionCommand, request, feedFwdMessage) { }
        
        public override bool CanBeForged(IChaineable previous)
            => previous != null && previous.IsMultiSocket && _commands.Count > 0;

        public override async Task<IChaineable> Forge(IChaineable previous)
        {
             if (!hasCatchedThrow(previous))
                throw new InvalidOperationException($"{nameof(JunctionStep)} >> {nameof(Forge)} >> {nameof(hasCatchedThrow)} >> An error has occured while passing the runner. STEP CANNOT BE FORGED");

            // TODO override checkCanForge
            if (!previous.IsMultiSocket)
                throw new InvalidOperationException($"{nameof(JunctionStep)} >> BAD CHAIN CONFIGURATION >> PREVIOUS STEP IS NOT MULTISOCKET >> A JunctionStep can only be connected from a SplitterStep (or subclasses of)");

            _runner.RunnedInstructions.Add($"- JOIN: {_id}");

            var castedPrev = (SplitterStep)previous;

            var outputs = previous.GrouppedOutputs();

            var sb = new StringBuilder();

            foreach(var key in outputs.Keys)
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
                          .AppendLine(guidanceMessageFrom(forgedPrevious.PromptedInstruction, output.SerializedResult, output.SchemaForMessage(), output.GuidanceMessage).Trim());

                        _instructionsLog.AddRange(previous.InstructionsLog);
                    });
                }
                else // build a single output result (Join after SPST | Pipe)
                {
                    var output = outputs[key].FirstOrDefault();

                    sb.AppendLine()
                      .AppendLine(guidanceMessageFrom(forgedPrevious.PromptedInstruction, output.SerializedResult, output.SchemaForMessage(), output.GuidanceMessage).Trim());
                }
            }

            Request.GuidanceMessage = sb.ToString().Trim();
            
            await forgeLink();

            submitForgeLog();

            return _next != null ? await _next.Forge(this) : this;
        }
    }
}
