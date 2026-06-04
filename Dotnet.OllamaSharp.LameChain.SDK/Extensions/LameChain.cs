using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Response;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Steps;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

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

        /// <summary>
        /// Allows to parallelize multiple subchains (#WIP)
        /// </summary>
        /// <param name="step"></param>
        /// <param name="subchains"></param>
        /// <param name="plugSettings"></param>
        /// <returns></returns>
        public static SplitterStep Tap(this SingleThrowStep step, List<SingleThrowStep> subchains, StepSettings? plugSettings = null)
        {
            var split = step.Plug([], plugSettings);

            subchains.ForEach(chain => split.Plug(chain));

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

        public static TStep ForwardFirstType<TStep>(this ChainStep step) where TStep : ChainStep
        {
            var firstStep = step.GetFirstStep();

            if (firstStep.GetType() != typeof(TStep))
                throw new InvalidOperationException($"{nameof(ForwardFirstType)} >> INVALID CHAIN CONFIGURATION >> TYPE OF FIRST STEP IS {firstStep.GetType().Name}");

            return firstStep as TStep;
        }


        /// <summary>
        /// Executes ONE SingleThrowStep if a SmartCondition (LLM evaluation) is met
        /// </summary>
        /// <typeparam name="TStep"></typeparam>
        /// <param name="step"></param>
        /// <param name="evaluator"></param>
        /// <param name="trueInstruction"></param>
        /// <returns></returns>
        public static SmartConditionalStep ThenIf<TStep>(this SingleThrowStep step, StepInstruction evaluator, StepInstruction trueInstruction) where TStep : SingleThrowStep
        {
            var conditional = step.ToSmartConditional(evaluator);
           
            var trueBranch = step.ExpandTo<TStep>(trueInstruction);

            conditional.IfTrueThen(trueBranch);

            step.Link(conditional, isForward: true, isTwoWay: true);

            return conditional;
        }

        /// <summary>
        /// Executes a Subchain if an LLM evaluation condition is met
        /// </summary>
        /// <param name="step"></param>
        /// <param name="evaluator"></param>
        /// <param name="trueBranch"></param>
        /// <returns></returns>
        public static SmartConditionalStep ThenIf(this SingleThrowStep step, StepInstruction evaluator, SingleThrowStep trueBranch)
        {
            var conditional = step.ToSmartConditional(evaluator);

            conditional.IfTrueThen(trueBranch);

            step.Link(conditional, isForward: true, isTwoWay: true);

            return conditional;
        }

        /// <summary>
        /// Allows to execute ONE single throw step of any type if the Func condition is met
        /// </summary>
        /// <typeparam name="TStep"></typeparam>
        /// <param name="step"></param>
        /// <param name="condition"></param>
        /// <param name="conditionSettings"></param>
        /// <param name="trueInstruction"></param>
        /// <param name="conditionFeedFwd"></param>
        /// <returns></returns>
        public static ConditionalStep ThenIf<TStep>(this SingleThrowStep step, Expression<Func<bool>> condition, StepSettings conditionSettings, StepInstruction trueInstruction, string? conditionFeedFwd = null) where TStep : SingleThrowStep
        {
            var conditional = step.ToConditional(condition, conditionSettings, conditionFeedFwd);

            ChainStep trueBranch = step.ExpandTo<TStep>(trueInstruction); 

            conditional.IfTrueThen(trueBranch);

            step.Link(conditional, isForward: true, isTwoWay: true);

            return conditional;
        }

        /// <summary>
        /// Allows to execute a Subchain if the Func condition is met
        /// </summary>
        /// <param name="step"></param>
        /// <param name="condition"></param>
        /// <param name="conditionSettings"></param>
        /// <param name="trueBranch"></param>
        /// <param name="conditionFeedFwd"></param>
        /// <returns></returns>
        public static ConditionalStep ThenIf(this SingleThrowStep step, Expression<Func<bool>> condition, StepSettings conditionSettings, SingleThrowStep trueBranch, string? conditionFeedFwd = null)
        {
            var conditional = step.ToConditional(condition, conditionSettings, conditionFeedFwd);

            if(trueBranch != null)
                conditional.IfTrueThen(trueBranch);

            step.Link(conditional, isForward: true, isTwoWay: true);

            return conditional;
        }

        /// <summary>
        /// Executes a Stored Step with Storeable Command to persist an object of type TStored (which should be the type of the PREVIOUS OUTPUT to store
        /// </summary>
        /// <typeparam name="TStored"></typeparam>
        /// <param name="step"></param>
        /// <param name="storeInstruction"></param>
        /// <returns></returns>
        public static SingleThrowStep Store<TStored>(this SingleThrowStep step, StepInstruction storeInstruction) where TStored : class
        {
            var store = step.AsStore<TStored>(storeInstruction);

            step.Link(store, isForward: true, isTwoWay: true);

            return store;
        }

        /// <summary>
        /// Executes ONE Stored Step with Storeable Command to persist an object of type TStored (which should be the type of the PREVIOUS OUTPUT to store
        /// if the Func condition is met
        /// </summary>
        /// <typeparam name="TPrev"></typeparam>
        /// <param name="step"></param>
        /// <param name="condition"></param>
        /// <param name="conditionSettings"></param>
        /// <param name="trueInstruction"></param>
        /// <param name="conditionFeedFwd"></param>
        /// <returns></returns>
        public static ConditionalStep StoreIf<TPrev>(this SingleThrowStep step, Expression<Func<bool>> condition, StepSettings conditionSettings, StepInstruction trueInstruction, string? conditionFeedFwd = null) where TPrev : class
        {
            var conditional = step.ToConditional(condition, conditionSettings, conditionFeedFwd);

            var trueBranch = step.AsStore<TPrev>(trueInstruction);

            conditional.IfTrueThen(trueBranch);

            step.Link(conditional, isForward: true, isTwoWay: true);

            return conditional;
        }

        /// <summary>
        /// Executes a Subchain that must start with a StoredStep of type TPrev (then can be chained with any type of step. This ensures that the 'current' chain result is stored before triggering the subchain)
        /// </summary>
        /// <typeparam name="TPrev"></typeparam>
        /// <param name="step"></param>
        /// <param name="condition"></param>
        /// <param name="conditionSettings"></param>
        /// <param name="trueBranch"></param>
        /// <param name="conditionFeedFwd"></param>
        /// <returns></returns>
        public static ConditionalStep StoreIf<TPrev>(this SingleThrowStep step, Expression<Func<bool>> condition, StepSettings conditionSettings, StoredStep<TPrev> trueBranch, string? conditionFeedFwd = null) where TPrev : class
        {
            var conditional = step.ToConditional(condition, conditionSettings, conditionFeedFwd);

            if (trueBranch != null)
                conditional.IfTrueThen(trueBranch);

            step.Link(conditional, isForward: true, isTwoWay: true);

            return conditional;
        }

        /// <summary>
        /// Executes ONE StashedStep and a SourceableCommand to create a data source that can be reused during the chain execution if a LLM evaluation is met
        /// </summary>
        /// <param name="step"></param>
        /// <param name="evaluator"></param>
        /// <param name="stashInstruction"></param>
        /// <param name="isGreedy"></param>
        /// <param name="isIsolated"></param>
        /// <returns></returns>
        public static SmartConditionalStep StashIf(this SingleThrowStep step, StepInstruction evaluator, StepInstruction stashInstruction, bool isGreedy = false, bool isIsolated = true)
        {
            var conditional = step.ToSmartConditional(evaluator);

            var trueBranch = step.ToStash(stashInstruction, isGreedy, isIsolated);

            conditional.IfTrueThen(trueBranch);

            step.Link(conditional, isForward: true, isTwoWay: true);

            return conditional;
        }

        /// <summary>
        /// Executes a Subchain that must begin with a StashedStep and a SourceableCommand to create a data source that can be reused during the chain execution if a LLM evaluation is met
        /// </summary>
        /// <param name="step"></param>
        /// <param name="evaluator"></param>
        /// <param name="trueBranch"></param>
        /// <returns></returns>
        public static SmartConditionalStep StashIf(this SingleThrowStep step, StepInstruction evaluator, StashedStep trueBranch)
        {
            var conditional = step.ToSmartConditional(evaluator);

            conditional.IfTrueThen(trueBranch);

            step.Link(conditional, isForward: true, isTwoWay: true);

            return conditional;
        }

        /// <summary>
        /// Executes a StashedStep and a SourceableCommand to create a data source that can be reused during chain execution, allowing to configure the stash settings and outputting the ID to configure it as a feed)
        /// </summary>
        /// <param name="step"></param>
        /// <param name="stashInstruction"></param>
        /// <param name="stashId"></param>
        /// <param name="isGreedy"></param>
        /// <param name="isIsolated"></param>
        /// <returns></returns>
        public static StashedStep Stash(this SingleThrowStep step, StepInstruction stashInstruction, out Func<Guid> stashId, bool isGreedy = false, bool isIsolated = true)
        {
            var stash = step.ToStash(stashInstruction, isGreedy, isIsolated);

            step.Link(stash, isForward: true, isTwoWay: true);

            stashId = stash.GetRunnerId;

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

        public static SingleThrowStep UseBroadcaster(this SingleThrowStep step, Action<Guid, string, LogLevel> broadcaster)
        {
            if (step.IsRunning)
                step.Runner.onBroadcast += broadcaster;
            
            return step;
        }
       
        public static async Task<ChainResult> ThenExecuteAsync(this SingleThrowStep step, bool withFinalMessage = false, bool withReplay = false, CommandSettings finalMsgSettings = null)
            => await step.ExecuteChainAsync(withFinalMessage, withReplay, finalMsgSettings);

        //public static ChainStep Then<TCommand, TResult>(this ChainStep step, string? instruction, CommandSettings commandSettings, StepSettings? stepRequest = null, string? feedFwdInstruction = null) 
        //    where TCommand : BasePromptCommand<TResult>, new() 
        //    where TResult : class
        //{
        //    var nextStep = step.ExpandTo<TCommand, TResult>(instruction, commandSettings, stepRequest);

        //    step.Link(nextStep, isForward: true, isTwoWay: true);

        //    return nextStep as ChainStep;
        //}

    }
}
