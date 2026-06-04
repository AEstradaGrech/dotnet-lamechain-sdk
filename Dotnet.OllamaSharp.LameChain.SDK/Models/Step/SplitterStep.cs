using Dotnet.OllamaSharp.LameChain.SDK.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects.Outputs;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Steps
{
    public class SplitterStep : ChainStep
    {
        protected List<StepInstruction> _pluggedInstructions;

        protected List<IChaineable> _branches = new List<IChaineable>();
        protected Dictionary<IChaineable, Guid> _forgedSubSteps;

        
        // This stores all the replays that are ALWAYS called because they are
        // required to build the _runner.RunnedInstructions and, in case the user
        // requests the Chain Replay, they are added to this Splitter.ReplayLog BranchLogs so
        // the data hierarchy is already configured
        protected Dictionary<Guid, List<ReplayLog>> _subChainReplays = null;
        public SplitterStep() : base() { }
        // Constructor for Tap (execute N commands with the PREV OUT) (foreach branch, forge(branch))
        public SplitterStep(List<StepInstruction> instructions, StepSettings request, string? feedFwdMessage) : base(request, feedFwdMessage)
        {
            _forgedSubSteps = new Dictionary<IChaineable, Guid>();
            _pluggedInstructions = instructions;
        }

        // Constructor for Split<TComm>([]) <- executes the command and SPLITS the result over the plugged subchains passing the splitFeedFwd (forge(this) then Branches)
       
        public SplitterStep(StepInstruction splittedCommand, List<StepInstruction> instructions) : base(splittedCommand.StepSettings, splittedCommand.FeedFwdInstruction)
        {
            _forgedSubSteps = new Dictionary<IChaineable, Guid>();
            _commands.Add(splittedCommand.Command);
            _pluggedInstructions = instructions;
        }

        // Constructor for .Pipe<TComm> <- executes the command ON EACH input (foreach PREV OUT forge(this))
        public SplitterStep(StepInstruction pipedCommand) : this(pipedCommand, []) { }
        
        // It is not the first step of a chain (prev != null)
        // The previous is a SPST
        // It has at least one command to run (should assert it has at least two probably otherwise is a bad / stupid configuration)
        public override bool CanBeForged(IChaineable previous)
            => IsRunning && previous != null && !previous.IsMultiSocket && (_pluggedInstructions.Count > 0 || _branches.Count > 0); // setup from instructions or from subChains

        public void Plug(IChaineable branch)
        {
            _branches.Add(branch);
        }

        public override async Task<IChaineable> Forge(IChaineable previous)
        {
            if (!hasCatchedThrow(previous))
                throw new InvalidOperationException($"{nameof(SplitterStep)} >> {nameof(Forge)} >> {nameof(hasCatchedThrow)} >> An error has occured while passing the runner. STEP CANNOT BE FORGED");

            if (previous.IsMultiSocket)
                throw new InvalidOperationException($"{nameof(SplitterStep)} >> BAD CHAIN CONFIGURATION >> PREVIOUS STEP IS MULTISOCKET >> A SplitterStep can only be connected from a SingleThrowStep");

            if (_next == null)
                throw new InvalidOperationException($"{nameof(SplitterStep)} >> BAD CHAIN CONFIGURATION >> A CHAIN cannot end up in a SplitterStep and the current has no NEXT assigned >> CHAIN CANNOT BE CLOSED");

            if(_commands.Count == 1)
            {
                // All commands boosted because it is the only combination that can't be covered with .Then() / .Tap() + .WithRebujito()
                // If is splitter, then the splitter Settings it is for the splitted command (boost
                var splitted = ThrowTo(swapRunner: false, _commands.First(), _stepSettings, _feedForwardInstruction);

                splitted.Link(previous, isForward: false, isTwoWay: false);
                
                var splittedOut = await splitted.Forge(previous);

                _runner = splittedOut.Drop();

                //splitter does not update the instruction log since it runs in the main chain
                if (_runner.TryFindForged(splittedOut.Id, out var log))
                {
                    _promptedInstruction = log.CommandInstruction;

                    // This is for logging / summarization purposes. MultiThrow steps have no 'forge' with GuidanceMessage
                    // and the result is taken from the Outputs.GuidanceMessage
                    Request.GuidanceMessage = splittedOut.Request.GuidanceMessage;
                    //_feedFwd is passed to splitted. splitted returns that msg or the las of its chain when it becomes the previous

                    _feedForwardInstruction = splittedOut.FeedForwardInstruction;
                }
                previous = splittedOut;
            }
            

            _pluggedInstructions.ForEach(i => _branches.Add(ThrowTo(swapRunner: true, i.Command, i.StepSettings, i.FeedFwdInstruction)));

            var chainResults = new List<IChaineable>();

            processBranchResults(await Task.WhenAll(_branches.Select(branch => Task.Run(() => {
                branch.Link(previous, isForward: false, isTwoWay: false);    
                return branch.Forge(previous); 
            }))));

            //It is a .TAP with no command for the main command (this)
            handleNonSplittedStepData(previous.Id);

            submitForgeLog();

            return await _next.Forge(this);
        }

        public IChaineable GetForgedSubStep(Guid stepId) => _forgedSubSteps.Keys.FirstOrDefault(step => step.Id == stepId);

        public IChaineable GetPluggedById(Guid stepId)
        {
            if (!_branches.Any(step => step.Id == stepId))
            {
                return _forgedSubSteps.Keys.Any(key => key.Id == stepId) ?
                    _branches.SingleOrDefault(branch => branch.Id == _forgedSubSteps.Select(kvp => kvp).Where(kvp => kvp.Key.Id == stepId).Select(x => x.Value).First()) :
                    null;
            }

            else return _branches.SingleOrDefault(step => step.Id == stepId);
        }

        // Como funciona ahora (20/05/2026) el tracking de instrucciones en subcadenas
        // El ChainRunner es unico y mantiene el arrastre de todas las instrucciones ejecutadas
        // Esto se clona en las sub cadenas para poder diferenciar FIRST RUNNER (chain) de FIRST SUBRUNNER (parallel subchain)
        // Entonces, cada subRunner tambien recibe el arrastre de instrucciones mas las de su branch, PERO
        // cada uno mantiene SU PROPIA INSTRUCCION. Cuando terminan las subcadenas NO se puede hacer append sin mas del RunnersLog
        // porque lleva copias del arrastre de la principal, asi que se tiene que hacer un
        // _subRunner.onReport <- y cada subrunner reporta su ReplayLog con SU instruccion y ya solo es hacer append
        // en el _instructionLog de ESTE runner (que indica si es SPLITTER | TAP | PIPE y las subcadenas ejecutadas AKA el principal)
        public void processBranchResults(IChaineable[] results)
        {
            _subChainReplays = new Dictionary<Guid, List<ReplayLog>>();
            results.ToList().ForEach(chaineable =>
            {
                var replays = chaineable.Runner.GetReplays();

                var firstStepId = replays.First().RunnerId;
                
                _forgedSubSteps.Add(chaineable, firstStepId);

                Outputs.AddRange(chaineable.Outputs);

                chaineable.Runner.ForgedLogs.ForEach(log => sendForgeLog(log, false));

                _instructionsLog.Add($"- SUB CHAIN {firstStepId}: ");
                _instructionsLog.AddRange(replays.SelectMany(rep => rep.RunnersLog));
                _instructionsLog.Add($"- END SUB CHAIN -");

                _subChainReplays.Add(chaineable.Id, replays);
            });
        }

        protected void handleNonSplittedStepData(Guid previousId)
        {
            var promptSb = new StringBuilder();
            var feedFwdSb = new StringBuilder();
            _branches.SelectMany(branch =>
                branch.Outputs.Select(link =>
                    new { link.Instruction, link.ForwardGuidance }))
                    .ToList()
                    .ForEach(item => {
                        promptSb.AppendLine(item.Instruction);
                        feedFwdSb.AppendLine(item.ForwardGuidance);
                    });

            _promptedInstruction += $"\n{promptSb.ToString().Trim()}";
            _feedForwardInstruction += $"\n{feedFwdSb.ToString().Trim()}";

            if (_runner.TryFindForged(previousId, out var log))
                Request.GuidanceMessage += guidanceMessageFrom(log.CommandInstruction.Replace(preInstructionTag, "").Trim(), log.JsonResult, log.JsonSchema, log.FeedForwardMessage);
        }

        protected override ReplayLog replayFromLog()
        {
            var report = base.replayFromLog();

            report.BranchLogs = _subChainReplays;

            return report;
        }
    }
}
