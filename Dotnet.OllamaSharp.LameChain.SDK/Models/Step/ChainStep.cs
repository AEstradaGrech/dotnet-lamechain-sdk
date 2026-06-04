using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Evaluators;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects.Outputs;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Steps
{
    public abstract class ChainStep : IChaineable
    {
        public delegate void OnRunNotify(Guid runnerId, string message, LogLevel level);
        public event OnRunNotify onRunNotify;

        public delegate void OnFinishNotify(ForgeLog log, bool appendLogs = true); //communicate / persist data during execution
        public event OnFinishNotify onFinishNotify;

        public delegate void OnReportReplay(ReplayLog log);
        public event OnReportReplay onReportReplay;

        public delegate void OnSupportAcknowledge(IChaineable id);
        public event OnSupportAcknowledge onSupportIncoming;

        protected readonly Guid _id;
        protected readonly ForgeLog _forgeLog;
                                                                      
        protected IChaineable _next;
        protected IChaineable _prev;
        protected string preInstructionTag = "# INSTRUCTION: ";
        protected string? _promptedInstruction = null; // The prompted instruction for this step. Serves as a log and also to generate the feedForwardInstruction (provide context about prev step). Null if !Executed
        protected string? _feedForwardInstruction = null;
        protected StepSettings _stepSettings;
        protected List<string> _instructionsLog = new List<string>();
        protected List<IJsoneable> _commands = new List<IJsoneable>();
        protected List<ChainLink> _outputs = new List<ChainLink>();
        protected ChainRunner? _runner = null;
        protected DateTime _passCatchTimestamp;
        public Guid Id => _id;
        public IChaineable Next => _next;
        public IChaineable Previous => _prev;
        public List<IJsoneable> Commands => _commands;
        public List<ChainLink> Outputs => _outputs;
        public string Input => _stepSettings.CommandRequest.Prompt;
        public bool IsRunning => _runner != null;
        public ChainRunner? Runner => _runner;
        public PromptCommandRequest Request => _stepSettings.CommandRequest;
        public string? FeedForwardInstruction => _feedForwardInstruction;
        public string? PromptedInstruction => _promptedInstruction;
        public List<string> InstructionsLog => _instructionsLog;
        public bool IsMultiSocket => this.GetType() == typeof(SplitterStep) || this.GetType() == typeof(PipedStep);
        public bool IsForged => _outputs.Count > 0;
        // Hail the Trinary Boolean, 3 options for the price of 2!
        // (remember those 'Y / N / IDK' questions?)
        public bool IsChained(bool? isForwardCheck = true)
            => isForwardCheck == null ? _next != null || _prev != null : isForwardCheck.Value ? _next != null : _prev != null;

        public bool IsFirstStep() => _next != null && _prev == null;

        // It has a copy of the ChainRunner && IsChainedAsFirst() && HasMainInstructionsLog && HasNotRunnedYet
        public bool IsFirstSubstep()
            => IsReady() && _prev != null && _runner.RunnedInstructions.Count > 0 && _runner.ForgedLogs.Count == 0;
       
        //Checks that _cmds.Count > 0 & the input type of step to ensure that the pieces match (single from single, collector from multi or pipe, multi from single, pipe from multi or pipe
        public abstract bool CanBeForged(IChaineable previous);

        public void notify(string message, LogLevel level = LogLevel.Information)
        {
            if (onRunNotify != null)
                onRunNotify(_id, $"{GetType().Name} >> {DateTime.Now} >> {message}", level);
        }

        protected void onRunBegin(IChaineable previous)
        {
            notify($"{nameof(onRunBegin)}");

            if (!IsFirstStep() && !IsFirstSubstep())
            {
                if (!IsRunning && !hasCatchedThrow(previous))
                    throw new InvalidOperationException($"{nameof(SingleThrowStep)} >> {nameof(Forge)} >> {_commands.First().GetType().Name} >> {nameof(hasCatchedThrow)} >> AN ERROR HAS OCCURED WHILE PASSING THE CHAIN RUNNER FROM PREVIOUS STEP");
            }

            else _passCatchTimestamp = DateTime.Now;
        }

        protected virtual async Task runStep(IChaineable previous) 
        {
            onRunBegin(previous);

            notify($"{nameof(runStep)}");

            await forgeLink(previous);

            submitForgeLog();
        }
        //protected abstract Task forgeLink(IChaineable previous); // this makes the request and add the resulting link to the outputs list (allows multi-socket)
        public ChainStep() 
        { 
            _id = Guid.NewGuid();
            _forgeLog = new ForgeLog(_id, _feedForwardInstruction);
        }

        public ChainStep(StepSettings settings, string? feedForwardMessage = null) : this() 
        { 
            _stepSettings = settings ?? new StepSettings(); 
            _feedForwardInstruction = feedForwardMessage;
        }

        public void Link(IChaineable step, bool isForward, bool isTwoWay)
        {
            if (isForward)
                _next = step;

            else _prev = step;

            if (isTwoWay)
                step.Link(this, isForward: !isForward);
        }

        public IChaineable GetFirstStep() => _prev != null ? getFirstStep(_prev) : this;
        public IChaineable GetLastStep() => _next != null ? getLastStep(_next) : this;
        public Dictionary<Guid, List<ChainLink>> GrouppedOutputs()
        {
            var result = new Dictionary<Guid, List<ChainLink>>();

            _outputs.GroupBy(link => link.StepId)
                    .Select(group => new { group.Key, Value = group.ToList() })
                    .ToList()
                    .ForEach(group => result.Add(group.Key, group.Value));

            return result;
        }

        public ChainRunner Drop()
        {
            var reference = _runner;
            _runner = null;
            return reference;
        }
        
        public abstract Task<IChaineable> Forge(IChaineable previous);

        protected virtual void submitForgeLog()
        {
            if (!IsRunning) return;

            // the other forge log data is setted up on construction
            // or at runtime, when the value is generated (json result, prompted instruction...)
            // this is just a centralized way to submit the log to the ChainRunner object
            if (_prev != null)
                _forgeLog.PrevId = _prev.Id;
            
            if(_next != null)
                _forgeLog.NextId = _next.Id;

            _forgeLog.CommandInstruction = _promptedInstruction;
            _forgeLog.StepInstruction = _promptedInstruction + (string.IsNullOrEmpty(Request.GuidanceMessage) ? "" : $"\n{Request.GuidanceMessage}");

            _forgeLog.Prompt = Request.Prompt;
            _forgeLog.FeedForwardMessage = _feedForwardInstruction;
            _forgeLog.RunnersLog = _instructionsLog;
            
            onFinishNotify(_forgeLog);
        }

        protected void sendForgeLog(ForgeLog log, bool updateRunnersLog = true)
        {
            onFinishNotify(log, updateRunnersLog);
        }
        public SingleThrowStep ThrowTo(bool swapRunner, IJsoneable command, StepSettings recieverSettings, string? feedForwardInstruction = null)
        {
            if (!IsRunning)
                throw new InvalidOperationException($"{nameof(ChainStep)} >> {nameof(ThrowTo)} >> This method is to instantiate steps with a copy of the chain runner and the current caller is not the runner");

            var newRunner = swapRunner ? _runner.Clone() : _runner;
            // every step of this sub chain gets a new nullable runner with the previous log, but they subscribe to their own runner
            // this is to run chains in parallel. The last runner of each subChain has the report for the original Multithrow Step so they can be appended
            // to the original / main runner and deleted
            var reciever = Activator.CreateInstance(typeof(SingleThrowStep), new StepInstruction(command, recieverSettings, feedForwardInstruction), newRunner) as SingleThrowStep; // !!!!!!!!!!!!!! CLONE STEP SETTINGS

            return reciever;
        }
        public SingleThrowStep ExpandTo(IJsoneable command, StepSettings? stepSettings, string? feedForwardInstruction = null)
            => Activator.CreateInstance(typeof(SingleThrowStep), command, stepSettings, feedForwardInstruction) as SingleThrowStep;
        public SingleThrowStep ExpandTo<TCommand, TResult>(string instruction, CommandSettings commandSettings, StepSettings? settings, string? feedForwardInstruction = null) where TCommand : BasePromptCommand<TResult>, new()
            => ExpandTo(Activator.CreateInstance(typeof(TCommand), Commands.First().BorrowLlama, instruction, commandSettings) as IJsoneable, settings, feedForwardInstruction);

        public TStep ExpandTo<TStep>(IJsoneable command, StepSettings? request, string? feedForwardInstruction = null) where TStep : ChainStep
            => Activator.CreateInstance(typeof(TStep), command, request, feedForwardInstruction) as TStep;
        public TStep ExpandTo<TStep>(StepInstruction instruction) where TStep : ChainStep
            => Activator.CreateInstance(typeof(TStep), instruction) as TStep;

        public TStep ExpandTo<TStep, TCommand, TResult>(string instruction, CommandSettings commandSettings, StepSettings settings, string? feedForwardInstruction = null)
            where TCommand : BasePromptCommand<TResult>, new()
            where TStep : ChainStep
                => ExpandTo<TStep>(Activator.CreateInstance(typeof(TCommand), Commands.First().BorrowLlama, instruction, commandSettings) as IJsoneable, settings, feedForwardInstruction);

        public SplitterStep Plug(List<StepInstruction> instructions, StepSettings? request, string? splitterFeedFwd = null)
          => Activator.CreateInstance(typeof(SplitterStep), instructions, request, splitterFeedFwd) as SplitterStep;
        public SplitterStep SplitTo(StepInstruction splitted, List<StepInstruction> instructions)
          => Activator.CreateInstance(typeof(SplitterStep),  splitted, instructions) as SplitterStep;

        public StashedStep ToStash(StepInstruction instruction, bool isGreedy = false, bool isIsolated = true) // next can read the stash, the stash does not use previous output for its request
        {
            if (!instruction.Command.GetType().IsAssignableTo(typeof(SourceableCommand)))
                throw new InvalidOperationException($"{nameof(ChainStep)} >> {nameof(ToStash)} >> INVALID STEP CONFIGURATION >> The configured command type ({instruction.Command.GetType().Name}) is not a {typeof(SourceableCommand)} or any subclass of it");

            return Activator.CreateInstance(typeof(StashedStep), instruction.Command, instruction.StepSettings, isGreedy, isIsolated, instruction.FeedFwdInstruction) as StashedStep;
        }

        public ConditionalStep ToConditional(Expression<Func<bool>> condition, StepSettings stepSettings, string? feedFwd = null)
            => Activator.CreateInstance(typeof(ConditionalStep), condition, stepSettings, feedFwd) as ConditionalStep;

        public StoredStep<TStored> AsStore<TStored>(StepInstruction instruction) where TStored : class
           => Activator.CreateInstance(typeof(StoredStep<TStored>), instruction) as StoredStep<TStored>;

        public SmartConditionalStep ToSmartConditional(StepInstruction instruction)
        {
            if (instruction.Command.GetType() != typeof(ScoredBoolCommand) && !instruction.Command.GetType().IsSubclassOf(typeof(ScoredBoolCommand)))
                throw new InvalidDataException($"{nameof(SmartConditionalStep)} >> {instruction.Command.GetType().Name} >> A SmartConditionalStep command must be a ScoredBoolCommand or a subclass of it");

            return Activator.CreateInstance(typeof(SmartConditionalStep), instruction.Command, instruction.StepSettings, instruction.FeedFwdInstruction) as SmartConditionalStep;
        }


        public TDeserialized GetOutputAs<TDeserialized>() where TDeserialized : class
            => IsForged ? JsonSerializer.Deserialize<TDeserialized>(Outputs.First().SerializedResult,getSerializerOptions()) ??
                throw new InvalidOperationException($"Failed to deserialize JSON to type {typeof(TDeserialized).Name}") :
                throw new InvalidOperationException("The chain step has not been forged and has no output value");

        private JsonSerializerOptions getSerializerOptions()
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

            return options;
        }
        protected IChaineable getFirstStep(IChaineable current)
            => current.IsFirstStep() ? current : getFirstStep(current.Previous);
        protected IChaineable getLastStep(IChaineable current)
            => current.Next == null ? current : getLastStep(current.Next);
        protected void checkCanForge(IChaineable previous)
        {
            if (!CanBeForged(previous))
                throw new InvalidOperationException($"{nameof(SingleThrowStep)} >> {nameof(Forge)} >> An error has occured while forging the chain link. STEP CANNOT BE FORGED");

            if(!IsRunning)
                throw new InvalidOperationException($"{nameof(SingleThrowStep)} >> {nameof(Forge)} >> The current step has no runner object configured. STEP CANNOT BE FORGED");
        }

        protected string getStepGuidanceMessage(IChaineable previous)
        {
            var sb = new StringBuilder();

            if (!IsFirstStep() && previous.IsForged)
            {
                if (previous.GetType() == typeof(StashedStep) && ((StashedStep)previous).IsGreedy) return string.Empty;
                
                sb.AppendLine()
                  .AppendLine(!_stepSettings.WithFullContext ? 
                    previous.Outputs.First().SerializedResult :
                    guidanceMessageFrom(
                        previous.Outputs.First().Instruction.Replace(preInstructionTag, "").Trim(),
                        previous.Outputs.First().SerializedResult,
                        _stepSettings.WithPrevSchema ? previous.Outputs.First().SchemaForMessage() : null,
                        previous.Outputs.First().ForwardGuidance)
                  );
            }

            return sb.ToString();
        }
        
        protected async Task forgeLink(IChaineable previous, int idx = 0)
        {
            if (idx >= _commands.Count) return;

            Request.Prompt = IsFirstStep() && _runner.RunnedInstructions.Count == 0 ? 
                _runner.UserPrompt : !string.IsNullOrEmpty(Request.Prompt.Trim()) ? 
                Request.Prompt :
                "Complete the specified instruction. Do not chat with the user, just output the requested data as described.";

            setupRequestContext(previous);

            notify($"{nameof(forgeLink)} >> FORGING CHAIN LINK");
            // JsonPrompt
            var jsonResult = await _commands[idx].JsonPrompt(Request, _commands[idx].CommandSettings == null ? _runner.DefaultSettings : null, returnFullInstruction: false, preInstruction: preInstructionTag); //Skip GuidanceMessage, return only instruction for this step

            _promptedInstruction = jsonResult.Instruction;

            if(!string.IsNullOrEmpty(_promptedInstruction))
                _instructionsLog.Add(_promptedInstruction);

            notify($"{nameof(forgeLink)} >> LINK FORGED >> SYSTEM MESSAGE: \n{_promptedInstruction}");

            var link = new ChainLink(_id, _promptedInstruction, jsonResult.RawJson, JsonSerializerOptions.Default.GetJsonSchemaAsNode(jsonResult.Type), jsonResult.Type, feedForwardMessage: _feedForwardInstruction);

            _outputs.Add(link);

            _forgeLog.ForgeTimestamp = DateTime.Now;
            _forgeLog.JsonResult = jsonResult.RawJson;
            _forgeLog.JsonSchema = link.SchemaForMessage();
        }

        // Override as many methods you need to skip some message sections (for example, if you don't need the previous context)
        protected void setupRequestContext(IChaineable previous)
        {
            var sb = new StringBuilder();

            appendPreviousContext(sb, previous);

            appendNestedFeeds(sb);

            appendBoosters(sb);

            Request.GuidanceMessage += $"\n{sb.ToString()}".Trim();
        }

        /// <summary>
        /// Adds the context from the previous step. OVERRIDE to SKIP or process differently
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="previous"></param>
        protected virtual void appendPreviousContext(StringBuilder sb, IChaineable previous)
        {
            if (!IsFirstStep() && GetType() != typeof(SplitterStep) && !GetType().IsAssignableTo(typeof(SplitterStep)))
                Request.GuidanceMessage += getContextMessageHeader();

            if(previous != null)
                Request.GuidanceMessage += string.IsNullOrEmpty(Request.GuidanceMessage) ? getStepGuidanceMessage(previous) : $"\n{getStepGuidanceMessage(previous)}";
        }

        /// <summary>
        /// Adds the context from the previous steps configured in the feed. OVERRIDE to SKIP or process differently
        /// </summary>
        /// <param name="sb"></param>
        protected virtual void appendFeeds(StringBuilder sb)
        {
            _stepSettings.ChainFeeds.ForEach(runnerId => sb.Append(getPreviousContextFromLog(runnerId(), _stepSettings.WithFullContext, _stepSettings.WithPrevSchema)));
        }

        /// <summary>
        /// Add the context to specific SubchainSteps or to specific Step commands (passed in the main command request and processed in the command 'Prompt(request)' as needed)
        /// OVERRIDE to SKIP or treat differently;
        /// </summary>
        /// <param name="sb"></param>
        protected virtual void appendNestedFeeds(StringBuilder sb)
        {
            _stepSettings.NestFeeds.Keys.ToList().ForEach(key => {
                var split = key.Split("-");
                if (split.Length > 1)
                {
                    bool isForStep = split.Length == 3;
                    //STEP format = StepGuid-cmdtagForRepeats-STEP (differentiate from Default & store target ID
                    //CMD format (default) CMDNAME-TagForReapeats (no id, many nested commands of the same type

                    if (isForStep)
                    {
                        var feedId = new Guid(split[0]);

                        _stepSettings.NestFeeds[key].ForEach(id => sb.AppendLine(getPreviousContextFromLog(feedId, _stepSettings.WithFullContext, _stepSettings.WithPrevSchema)));
                    }
                    else
                    {
                        var reqSb = new StringBuilder();

                        _stepSettings.NestFeeds[key].ForEach(id => reqSb.AppendLine(getPreviousContextFromLog(id(), _stepSettings.WithFullContext, _stepSettings.WithPrevSchema)));

                        Request.NestedGuidances.Add(key, reqSb.ToString());
                    }
                }
                else
                {
                    // Try pass for CMD 0
                }
            });
        }
        /// <summary>
        /// Add the text context from the configured boosts. OVERRIDE to SKIP or process differently
        /// </summary>
        /// <param name="sb"></param>
        protected virtual void appendBoosters(StringBuilder sb)
        {
            if (_stepSettings.Boosters.Count > 0)
                sb.AppendLine()
                  .AppendLine(_stepSettings.BoostersFeedText());
        }

        private string getPreviousContextFromLog(Guid runnerId, bool withFullContext = true, bool withPrevSchema = true)
        {
            if (!IsRunning) return string.Empty;

            var sb = new StringBuilder();

            if (_runner.TryFindForged(runnerId, out var forged))
                sb.AppendLine()
                  .AppendLine(!withFullContext ?
                    forged.JsonResult :
                    guidanceMessageFrom(
                        forged.CommandInstruction.Replace(preInstructionTag, "").Trim(),
                        forged.JsonResult,
                        withPrevSchema ? forged.JsonSchema : null,
                        forged.FeedForwardMessage)
                    );

            return sb.ToString().Trim();
        }

        public string getContextMessageHeader()
            => !IsRunning ? string.Empty : @$"
# CONTEXT: This section contains relevant information about previous instruction. Use the provided data as a guidance to complete your own instruction. Use each section 
description (wrapped in parenthesis) to understand what does it represent and how can it help you to complete your task but, REMEMBER: your instruction is ALWAYS your priority.

## IMPORTANT: follow this rules in order to generate your response:

- Analyze the previous output and any guidance message from the previous worker to get a better idea of the context and will help you with your task.
- Do not copy the output format, use it as contextual information but your response MUST BE compliant with your provided JSON schema.
- Review all the contextual information as a guidance but do not let the previous output format influence your response format (reason about the previous output content, but do not copy the format, follow your JSON schema)

{(!string.IsNullOrEmpty(_runner.Intent) ? $"> CHAIN INTENT (this is the overall goal of the whole chain in a categoric manner. Use it to have a very general idea about the task that the chain is trying to complete): {_runner.Intent}\n" : string.Empty)}
> USER INPUT (this is the actual user request. Use it to understand what the user is trying to do in general terms): {_runner.UserPrompt}";

        protected string guidanceMessageFrom(string instruction, string serialized, string jsonSchema, string? feededInstruction = null)
         => @$"{(string.IsNullOrEmpty(instruction.Trim()) ? string.Empty : $"> PREVIOUS INSTRUCTION (use this to have a clear idea of what was exactly the previous worker task and understand its output):\n{instruction.Trim()}")}

> PREVIOUS OUTPUT (this is the raw json output of the previous instruction):

{serialized}

{(string.IsNullOrEmpty(jsonSchema) ? string.Empty : $"> PREVIOUS OUTPUT SCHEMA (use this to understand the previous output json model):\n\n{jsonSchema}")}

{(string.IsNullOrEmpty(feededInstruction) ? string.Empty : $"> GUIDANCE MESSAGE FROM PREVIOUS WORKER (this states what the previous worker expects you to do with his output data): {feededInstruction}")}";

        public bool hasCatchedThrow(IChaineable previous)
        {
            _runner = previous.Drop();

            _runner.OnDropTo(this, previous);

            onRunNotify += _runner.OnRunnerNotify;
            onFinishNotify += _runner.OnRunnerFinished;
            
            checkCanForge(previous);

            //'what is the play about!'

            //By default splitted steps do not execute commands in their chain
            // (they trigger subchains of SingleThrowSteps)

            _passCatchTimestamp = DateTime.Now;

            notify($"{nameof(hasCatchedThrow)} >> CATCHED AT {_passCatchTimestamp}");

            return IsRunning;
        }

        protected virtual ReplayLog replayFromLog() => new ReplayLog(_forgeLog);
        // override in Multi-Socket to include subchain results
        public void SendReplay()
        {
            //build report
            var report = replayFromLog();
            
            report.RunnersLog = _instructionsLog; //cada miembro de la subchain hace append de SU instruction. si es sub-sub chain, contiene el log ya formateado (porque se hace report de pieza main) 
                                                                                                      //ej: sub-> [- do X] [ - do Y] [-TAP\n-SUBCHAIN:\n-Do Z\n-SUBCHAIN:\n-Do ETC] - [- do ..]
            report.FeededMessage = Request.GuidanceMessage;
            report.PassCatchTimestamp = _passCatchTimestamp;

            onReportReplay(report);
        }

        // 19-05-2026 --> esto realmente rompe el patron rugby (info al runner y solo un runner)
        //                si tengo que llamar a esto en algun sitio es que lo estoy haciendo mal
        public IChaineable OnRunnerCall() => this;

        public void OnRunnerSupport(Guid id)
        {
            if (_id == id)
                onSupportIncoming(this);
        }

        public void FollowRunner(IChaineable current)
        {
            if (!current.IsRunning) return;

            onSupportIncoming += current.Runner.OnSupporterResponse;
            onReportReplay += current.Runner.OnReplayReport;

            notify($"{nameof(FollowRunner)} >> FOLLOWING {current.Id}");
        }

        public bool IsReady() => IsRunning && _stepSettings != null && Request != null && _runner.CanRun();

        public void BoostWith(List<string> feeds, string? feedMessage) => _stepSettings.WithDataBoost(feedMessage, feeds);

        public void WithChainFeeds(List<Func<Guid>> stepIds)
        {
            stepIds.ForEach(id => _stepSettings.FeedFrom(id));
        }

        public Guid GetRunnerId() => _id;
        public Guid WhoIsPrevious() => _prev != null ? _prev.Id : _id;
        public Guid WhoIsNext() => _next != null ? _next.Id : _id;
    }
}
