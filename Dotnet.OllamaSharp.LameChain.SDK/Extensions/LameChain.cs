using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Response;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Steps;
using Microsoft.Extensions.Logging;

namespace Dotnet.OllamaSharp.LameChain.SDK.Extensions
{
    public static class LameChain
    {
        public static SingleThrowStep StartWith(StepInstruction firstInstruction, CommandSettings defaultSettings, string? finalSysMessage = null, string? chainIntent = null)
            => Activator.CreateInstance(typeof(SingleThrowStep), firstInstruction, 
                new ChainRunner(firstInstruction.StepSettings.CommandRequest.Prompt, defaultSettings, finalSysMessage, chainIntent)) 
                as SingleThrowStep;

        public static TStep StartWith<TStep>(StepInstruction firstInstruction, CommandSettings defaultSettings, string? finalSysMessage = null, string? chainIntent = null) where TStep : SingleThrowStep
            => Activator.CreateInstance(typeof(TStep), firstInstruction,
                new ChainRunner(firstInstruction.StepSettings.CommandRequest.Prompt, defaultSettings, finalSysMessage, chainIntent))
                as TStep;
        public static TStep SubChainWith<TStep>(params object?[]? args) where TStep : SingleThrowStep // null will use the Main ChainRunner's Defaultsettings
            => Activator.CreateInstance(typeof(TStep), args) as TStep;
        public static SingleThrowStep Then(this SingleThrowStep step, IJsoneable command, StepSettings request, string? feedFwdInstruction = null)
        {
            /*
                BASIC API:
                    - Then(cmd) = 1 x inpt / 1 x opt --> Appends every prev link as it is to the sysmsg (not cxt-len safe)
                    - Tap([]) = splits the chain by plugging-in N commands that will run in parallel and generates N output links
                    - Split(cmd, [cmds]) = similar to Tap, but runs first a desired command then fans-out the result to the plugged commands and generates 1 x Plugged Output
                    - Pipe(cmd) = OnForge, executes the command on each prev out link. It is a SplitterStep extension. Generates 1 x prevOut.Num LINKS
                    - Join(cmd) = OnForge, generates a single output using the prevLinks and a Joining Command. This is a 'controlled' merge
                    - Feed<TJoiner>([cmds]) = OnForge executes every cmd with the prevOutput and generates 1 SINGLE output with it. Is a 1 step Split & Join
                    - Feed<TJoiner>(piped, [cmds]) = OnForge executes every cmd with the prevOutput, executes the piped cmd on every splitter cmd and joins the result with the TJoiner generating 1 SINGLE output with it. Is a 1 step Split & Pipe & Join
                    - Loop(times: N, cmd) do N times the input command with the prev output
                    - Branch(A, B, decisorCmd) --> on runtime evaluates decision and forges(A) or (B) SWAPPING the step AND CONTINUEING
                   
                                 ------B`4 ---
                                |  ,-- B`2 ---|
                               FEED --- B`3 - THEN --(b)--        
                                |              |         |
                       |- B  -- B` --- B" -----|         |
                    A  -  C  -- C` --- C" ---- D --(a)-- E -- OPT 
                 START   TAP  PIPE   PIPE    JOIN   THEN          
             */

            var nextStep = step.ExpandTo(command, request, feedFwdInstruction);

            step.Link(nextStep, isForward: true, isTwoWay: true);

            return nextStep;
        }

        public static ChainStep ThenExecute(this SingleThrowStep step, out ChainResult result, bool withFinalMessage = true)
        {
            result = step.ExecuteChainAsync(withFinalMessage)
                .ConfigureAwait(continueOnCapturedContext: false)
                .GetAwaiter()
                .GetResult();

            return step;
        }

        // Expose ChainSteps to FeedAlsoFrom([Guid]) to pass chain results past the
        // Next step
        public static SingleThrowStep ExposeThisId(this SingleThrowStep step, out Func<Guid> id)
        {
            id = step.GetRunnerId;

            return step;
        }
        public static SplitterStep ExposeThisId(this SplitterStep step, out Func<Guid> id)
        {
            id = step.GetRunnerId;

            return step;
        }

        public static SingleThrowStep ExposeThisStep(this SingleThrowStep step, out SingleThrowStep exposed)
        {
            exposed = step;

            return step;
        }
        public static SplitterStep ExposeThisStep(this SplitterStep step, out SplitterStep exposed)
        {
            exposed = step;

            return step;
        }

        /// <summary>
        /// Splits the chain in N branches, 1 x instruction
        /// Executes every command using the same SINGLE PREV OUTPUT, passing a unique feedForwardMessage for each plugged instruction.
        /// GENERATES 1 x PLUGGED INSTRUCTION OUTPUT LINKS
        /// </summary>
        /// <param name="step"></param>
        /// <param name="instructions"></param>
        /// <param name="plugSettings">Common settings to be used (if any) for all plugged commands instead of each instruction.Settings. Works like: inst.Settings ?? plug.Settings ?? _runner.DefaultSettings (from reqDto)</param>
        /// <returns></returns>
        public static SplitterStep Tap(this SingleThrowStep step, List<StepInstruction> instructions, StepSettings? plugSettings = null)
        {
            var split = step.Plug(instructions, plugSettings);

            step.Link(split, isForward: true, isTwoWay: true);

            return split;
        }

        public static SplitterStep SplitThrough(this SingleThrowStep step, StepInstruction splitted, List<StepInstruction> instructions)
        {
            var split = step.SplitTo(splitted, instructions);

            step.Link(split, isForward: true, isTwoWay: true);

            return split;
        }

        /// <summary>
        /// Feeds a specified command with the list of passed instructions
        /// that will execute their command using the SINGLE PREV OUTPUT LINK
        /// to produce N outputs to be added to the feeded command system message
        /// </summary>
        /// <param name="step"></param>
        /// <param name="instruction"></param>
        /// <param name="instructions"></param>
        /// <param name="stepRequest"></param>
        /// <returns></returns>
        public static SingleThrowStep Feed(this SingleThrowStep step, StepInstruction instruction, List<StepInstruction> instructions, StepSettings? stepRequest = null)
        {
            var split = step.Plug(instructions, stepRequest);

            step.Link(split, isForward: true, isTwoWay: true);

            var feeded = split.ExpandTo<JunctionStep>(instruction.Command, stepRequest, instruction.FeedFwdInstruction);

            split.Link(feeded, isForward: true, isTwoWay: true);

            return feeded;
        }

        /// <summary>
        /// Runs the piped command for each PREVIOUS OUTPUT LINK, using the same feedForward message for all the results
        /// GENERATES 1 OUTPUT LINK x PREVIOUS OUTPUT LINK
        /// </summary>
        /// <param name="step"></param>
        /// <param name="command"></param>
        /// <param name="pipeFeedFwd"></param>
        /// <returns></returns>
        public static PipedStep Pipe(this SplitterStep step, IJsoneable command, StepSettings pipedSettings, string? pipeFeedFwd = null)
        {
            var split = step.ExpandTo<PipedStep>(new StepInstruction(command, pipedSettings, pipeFeedFwd));

            step.Link(split, isForward: true, isTwoWay: true);

            return split;
        }
        
        public static SingleThrowStep Join( this SplitterStep step, StepInstruction instruction)
        {
            var next = step.ExpandTo<JunctionStep>(instruction.Command, instruction.StepSettings, instruction.FeedFwdInstruction);
            
            step.Link(next, isForward: true, isTwoWay: true);
            
            return next;
        }

        /// <summary>
        /// Injects a random / uncontrolled amount of context data (with optional guidance) in the system message of 
        /// the step at runtime
        /// 
        /// SingleThrow Chain Adapter
        /// </summary>
        /// <param name="step"></param>
        /// <param name="sources"></param>
        /// <param name="guidance"></param>
        /// <returns></returns>
        public static SingleThrowStep WithRebujito(this SingleThrowStep step, List<string> sources, string? guidance = null, int? feedDose = null)
        {
            if (feedDose != null)
                step.BoostWith(sources.Select(source => feedDose != null && feedDose > 0 && source.Length > feedDose ? source.Substring(0, feedDose.Value) : source).ToList(), guidance);

            else step.BoostWith(sources, guidance);

            return step;
        }

        /// <summary>
        /// Injects a random / uncontrolled amount of context data (with optional guidance) in the system message of 
        /// the step at runtime
        /// 
        /// Splitter Chain Adapter
        /// </summary>
        /// <param name="step"></param>
        /// <param name="sources"></param>
        /// <param name="guidance"></param>
        /// <returns></returns>
        public static SplitterStep WithRebujito(this SplitterStep step, List<string> sources, string? guidance = null, int? feedDose = null)
        {
            if(feedDose != null)
                step.BoostWith(sources.Select(source => feedDose != null && feedDose > 0 && source.Length > feedDose ? source.Substring(0, feedDose.Value) : source).ToList(), guidance);

            else step.BoostWith(sources, guidance);
            
            return step;
        }

        public static ConditionalStep ThenIf(this SingleThrowStep step, StepInstruction evaluator, StepInstruction thenInstruction, out SingleThrowStep trueBranch, bool isGreedy = false, bool isIsolated = true)
        {
            var conditional = step.AsConditional(evaluator);

            trueBranch = step.ExpandTo(thenInstruction.Command, thenInstruction.StepSettings, thenInstruction.FeedFwdInstruction);

            conditional.IfTrueThen(trueBranch);

            return conditional;
        }

        public static ConditionalStep StashIf(this SingleThrowStep step, StepInstruction evaluator, StepInstruction stashInstruction, bool isGreedy = false, bool isIsolated = true)
        {
            var conditional = step.AsConditional(evaluator);

            var trueBranch = step.ToStash(stashInstruction, isGreedy, isIsolated);

            conditional.IfTrueThen(trueBranch);

            return conditional;
        }

        public static TStep ForwardFirstType<TStep>(this ChainStep step) where TStep : ChainStep
        {
            var firstStep = step.GetFirstStep();

            if (firstStep.GetType() != typeof(TStep))
                throw new InvalidOperationException($"{nameof(ForwardFirstType)} >> INVALID CHAIN CONFIGURATION >> TYPE OF FIRST STEP IS {firstStep.GetType().Name}");

            return firstStep as TStep;
        }
        public static ConditionalStep StashIf(this SingleThrowStep step, StepInstruction evaluator, StashedStep trueBranch)
        {
            var conditional = step.AsConditional(evaluator);

            conditional.IfTrueThen(trueBranch);

            step.Link(conditional, isForward: true, isTwoWay: true);

            return conditional;
        }

        public static StashedStep Stash(this SingleThrowStep step, StepInstruction stashInstruction, out Func<Guid> stashId, bool isGreedy = false, bool isIsolated = true)
        {
            var stash = step.ToStash(stashInstruction, isGreedy, isIsolated);

            step.Link(stash, isForward: true, isTwoWay: true);

            stashId = stash.GetRunnerId;

            return stash;
        }

        public static StashedStep Stash(this SplitterStep step, StepInstruction stashInstruction, out Guid stashId)
        {
            var stash = step.ToStash(stashInstruction);

            step.Link(stash, isForward: true, isTwoWay: true);

            stashId = stash.Id;

            return stash;
        }

        public static SingleThrowStep ChainFeedsFrom(this SingleThrowStep step, List<Func<Guid>> steps, string? guidance = null)
        {
            step.WithChainFeeds(steps);
            return step;
        }

        public static SplitterStep ChainFeedsFrom(this SplitterStep step, List<Func<Guid>> steps, string? guidance = null)
        {
            step.WithChainFeeds(steps);
            return step;
        }

        //public static SingleThrowStep UseBroadcaster(this SingleThrowStep step, Action<Guid, string, LogLevel> broadcaster)
        //{
        //    if(step.IsRunning)
        //    {
        //        step.Runner.onBroadcast += broadcaster.;
        //    }

        //    return step;
        //}
        public static SingleThrowStep UseBroadcaster(this SingleThrowStep step, Action<Guid, string, LogLevel> broadcaster)
        {
            if (step.IsRunning)
            {
                step.Runner.onBroadcast += broadcaster;
            }

            return step;
        }
        public static async Task<ChainResult> ThenExecuteAsync(this SingleThrowStep step, bool withFinalMessage = false, bool withReplay = false, CommandSettings finalMsgSettings = null)
            => await step.ExecuteChainAsync(withFinalMessage, withReplay, finalMsgSettings);

        public static ChainStep Then<TCommand, TResult>(this ChainStep step, string? instruction, CommandSettings commandSettings, StepSettings? stepRequest = null, string? feedFwdInstruction = null) 
            where TCommand : BasePromptCommand<TResult>, new() 
            where TResult : class
        {
            var nextStep = step.ExpandTo<TCommand, TResult>(instruction, commandSettings, stepRequest);

            step.Link(nextStep, isForward: true, isTwoWay: true);

            return nextStep as ChainStep;
        }

    }
}
