using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.TextGenerators;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Response;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Steps
{
    public class SingleThrowStep : ChainStep
    {
        public SingleThrowStep() : base() { }

        public SingleThrowStep(StepInstruction instruction) : base(instruction.StepSettings, instruction.FeedFwdInstruction)
        {
            _commands.Add(instruction.Command);
        }

        /// <summary>
        /// Constructor for steps with no main command but that execute some orchestration logic (like ConditionalSteps)
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="feedForwardMessage"></param>
        public SingleThrowStep(StepSettings settings, string? feedForwardMessage = null) : base(settings, feedForwardMessage) { }

        public SingleThrowStep(IJsoneable command, StepSettings stepSettings, string? feedFwdInstruction = null) : base(stepSettings, feedFwdInstruction)
        {
            _commands.Add(command);
        }

        /// <summary>
        /// This is the constructor for the FIRST RUNNER. ALWAYS

        /// It recieves and stores the original (and unique-per-chain) ChainRunner or a Clone (if swapped in a ThrowTo(parallelSubchain) [in splitters | pipes])
        /// 
        /// > If it is the FIRST RUNNER, the ChainRunner MUST CONTAIN the general INIT DATA (user prompt, chain intent and finalSysMessage) to 
        /// be used as contextual info on each step. Also...
        /// 
        /// > Subscribes to the NECESSARY events to persist outputs and messages across the chain steps. This is the only place for a FIRST RUNNER to do that, the 
        /// other runners must catch the previous pass on runtime (calling Forge(previous)) to do this and keep running the chain.
        /// 
        /// > The UserPrompt is prompted ONLY in the first step (the rest should work on interpretations of the previous data and 
        /// feedFwd guidance messages)
        /// 
        /// > If it is the FIRST SUBRUNNER, it works exactly as any other step in terms of recieved data / template instruction formation, but
        ///   it recieves a clone of the ChainRunner to run their chain in parallel and then return their ChainRunner clone with the results
        ///   to append  them to the (unique) original ChainRunner and keep running the main chain.
        ///   
        ///     *Note: subrunners MUST use Link(prev, fwd: false, two-way-bind: false) to recieve a previous from the main chain
        /// 
        /// > FIRST RUNNER is differenced from a FIRST SUB_RUNNER because the total RunnedInstruction stored in the ChainRunner & SwappedClones
        /// 
        /// </summary>
        /// <param name="command"> The initial chain command</param>
        /// <param name="runner">A data structure containing the initial data and store the generated data to pass it along the chain</param>
        /// <param name="feedFwdInstruction">This is like 'what you want to tell the next one about the instruction output' </param>
        public SingleThrowStep(StepInstruction instruction, ChainRunner runner) 
            : this(instruction.Command, instruction.StepSettings, instruction.FeedFwdInstruction)
        {
            if(runner != null)
            {
                _runner = runner;
                _passCatchTimestamp = DateTime.Now;

                onFinishNotify += _runner.OnRunnerFinished; // write stuff to runner. This is always triggered AFTER forgeLink or when appending subchain results
                onReportReplay += _runner.OnReplayReport;
                onRunNotify += _runner.OnRunnerNotify;

                _runner.SetReady(this);
            }

            // else is .SubChain()
        }

        public override async Task<IChaineable> Forge(IChaineable previous)
        {
            await runStep(previous);

            return _next != null ? await _next.Forge(this) : this;
        }

        // All runners MUST check if they are in possession of the ChainRunner (IsRunner) and... (<step-type-check>)
        public override bool CanBeForged(IChaineable previous)
            => IsRunning && previous == null ? _commands.Count > 0 : !previous.IsMultiSocket;

        public async Task<ChainResult> ExecuteChainAsync(bool withFinalMessage = false, bool withReplay = false, CommandSettings? finalMsgSettings = null)
        {
            notify($"{nameof(ExecuteChainAsync)} >> BEGINNING CHAIN EXECUTION");

            var firstStep = getFirstStep(current: this);

            if (!firstStep.IsReady())
            {
                notify($"{nameof(ExecuteChainAsync)} :: {nameof(firstStep.IsReady)} >> CHAIN CRASH >> FIRST STEP NOT READY", LogLevel.Critical);

                throw new InvalidOperationException($"{nameof(SingleThrowStep)} >> {nameof(ExecuteChainAsync)} >> {nameof(firstStep.IsReady)} >> FIRST STEP IS NOT READY :: ABORTING CHAIN");
            }

            var finalStep = await firstStep.Forge(null);

            notify($"{nameof(ExecuteChainAsync)} >> CHAIN FINISHED");

            if (!finalStep.Outputs.Any())
            {
                notify($"{nameof(ExecuteChainAsync)} >> CHAIN ERROR >> OUTPUT RESULTS GENERATED", LogLevel.Critical);

                throw new InvalidOperationException($"{nameof(SingleThrowStep)} >> {nameof(ExecuteChainAsync)} >> CHAIN ERROR >> OUTPUT RESULTS GENERATED");
            }

            if (string.IsNullOrEmpty(finalStep.Outputs.First().SerializedResult))
            {
                notify($"{nameof(ExecuteChainAsync)} >> CHAIN ERROR >> NO SERIALIZED RESULT FOUND", LogLevel.Critical);

                throw new InvalidOperationException($"{nameof(SingleThrowStep)} >> {nameof(ExecuteChainAsync)} >> CHAIN ERROR >> NO SERIALIZED RESULT FOUND");
            }

            var finalMessage = new ChatMessage(ChatRole.Assistant.ToString(), string.Empty);

            ChainRunner finalRunner = null;
            if (withFinalMessage)
            {
                try
                {
                    notify($"{nameof(ExecuteChainAsync)} >> REQUESTING CHAIN FINAL MESSAGE");

                    var finalInstruction = getDefaultFinalInstruction();

                    var finalizer = ExpandTo<MessagePromptCommand, ChatMessage>(
                        instruction: finalInstruction.SystemMessage,
                        finalMsgSettings ?? finalStep.Runner.DefaultSettings,
                        new StepSettings(new PromptCommandRequest(
                            message: finalInstruction.Prompt,
                            guidanceMessage: string.IsNullOrEmpty(finalStep.Runner.FwdSystemMessage) ? string.Empty : $"## USER PREFERENCES: {finalStep.Runner.FwdSystemMessage}\n")
                        )
                    );

                    var jsonMessage = await finalizer.Forge(finalStep);

                    notify($"{nameof(ExecuteChainAsync)} >> ON FINAL MESSAGE GENERATED >> CHAIN FINISHED");

                    finalMessage = jsonMessage.GetOutputAs<ChatMessage>();

                    finalRunner = jsonMessage.Drop();
                }
                catch(Exception ex)
                {
                    notify($"{nameof(ExecuteChainAsync)} >> FINAL MESSAGE KO >> EXCEPTION: {ex.Message}", LogLevel.Critical);
                    notify($"{nameof(ExecuteChainAsync)} >> FINAL MESSAGE ABORT >> RETURNING CHAIN RESULT: {ex.Message}", LogLevel.Warning);
                    finalMessage.Content = "AN ERROR HAS OCCURED WHILE GENERATING THE FINAL MESSAGE";
                    finalRunner = finalStep.Drop();
                }
            }

            else finalRunner = finalStep.Drop();

            return new ChainResult(withReplay ? finalRunner.GetReplays() : [], result: finalStep.Outputs.First().ResultObject(), finalStep.Outputs.First().JsonSchema, chainInput: firstStep.Input, stepsLog: finalRunner.RunnedInstructions, processedResult: finalMessage);
        }

        private Instruction getDefaultFinalInstruction()
            => new Instruction("Review and analyze carefully the previous output and return the content in text format as stated in your instruction",@"Your task is to analyze the provided context data from the previous outputs and return a 'user-friendly' text version of it in order to keep chatting with the user about the output results.
Try to preserve the provided content information unless instructed to give the final result a specific mood, tone or whatever extra instruction you are commanded to apply over the results");

    }
}
