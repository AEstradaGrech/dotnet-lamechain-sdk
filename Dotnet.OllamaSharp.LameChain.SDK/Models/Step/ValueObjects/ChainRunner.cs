using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects.Outputs;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects
{
    public class ChainRunner
    {
        public ChainRunner(string userInput, CommandSettings defaultSettings, string? finalSysMessage = null, string? intent /*AKA topic?*/= null)
        {
            UserPrompt = userInput.Trim();
            Intent = string.IsNullOrEmpty(intent.Trim()) ? null : intent;
            FwdSystemMessage = finalSysMessage;
            DefaultSettings = defaultSettings;

            Supporters = new Dictionary<Guid, Func<IChaineable>>();
            RunnedInstructions = new List<string>();
            _forgedSteps = new List<ForgeLog>();
            _replays = new List<ReplayLog>();
            _chainFeeds = new List<ForgeLog>();
        }

        public ChainRunner(ChainRunner cloned, bool cloneRunnedInstructions) : this(cloned.UserPrompt, cloned.DefaultSettings, cloned.FwdSystemMessage,cloned.Intent)
        {
            RunnedInstructions = cloneRunnedInstructions ? new List<string>(cloned.RunnedInstructions) : new List<string>();
            onReplayRequest = null;
            onSupportRequest = null;

            //Enable chain feeds for subchains
            //the pattern recursively does 'tryCheckThisForged - requestFromPrevChain' until the log is found in any cloned ChainRunner
            // WG: check mulithreading-shared-objects
            onChainFeedRequest += cloned.OnFeedRequest;

            cloned.onChainFeedResponse += OnFeedResponse;
        }
        public delegate void OnSupporterRequest(Guid runnedId);
        public event OnSupporterRequest onSupportRequest;

        public delegate void OnChainFeedResponse(ForgeLog feedLog);
        public event OnChainFeedResponse onChainFeedResponse;
        public delegate void OnChainFeedRequest(Guid runnerLogId);
        public event OnChainFeedRequest onChainFeedRequest;
        public delegate void OnReplay();
        public event OnReplay onReplayRequest;

        private IChaineable _current;
        private List<ForgeLog> _forgedSteps;
        private List<ReplayLog> _replays;
        private List<ForgeLog> _chainFeeds;
        public void OnSupporterResponse(IChaineable supporter)
        {
            // Runner.ThrowTo(supporter)
            // await supporter.Forge(Runner | Runner.Prev)
            // collect results -> feed to runner -> keep running
        }

        public void SetReady(IChaineable firstRunner)
        {
            if (_current != null) return;

            _current = firstRunner;

            onReplayRequest += _current.SendReplay;
        }

        // runner recievesThrow(previous)
        // _runner = previous.Drop()        <- pass the ChainRunner object to the next
        // _runner.OnDrop(this, previous)   <- sets runner as current and the previous as supporter (events & tracking)
        //      previous.FollowRunner()     <- subscribes to the support and replay (or any 'past-runner' events)
        // current.onRunnerNotify += OnRunnerNotification <- current can talk TO ChainRunner object
        // current.CatchThrowTimestamp = NOW; <- everything OK, start running
        // IsRunning = true                   <- has the _runner
        public void OnDropTo(IChaineable runner, IChaineable previous)
        {
            //This is the new runner holding the ChainRunner
            _current = runner;

            //If a previous runner (lost back in the chain) is required
            // the runner will trigger a multicast event with the id
            onSupportRequest += previous.OnRunnerSupport;
            // All previous are now supporters following the runner in case they are needed to
            //  - get more info about its outputs
            //  - replay its task (for validation-retry commands)
            // to do that they must respond to the support request
            // they register to a callback here and now the 'follow' the runner
            previous.FollowRunner(runner);
            
            // this might be triggered at the end of the chain to
            // get detailed info about the runned step
            onReplayRequest += previous.SendReplay; //WG: el último no se suscribe. CHECK

            // There is also a fast dictionary-delegate tracking all
            // the supporters (might be removed or set as the final access system better than the events system)
            Supporters.Add(previous.Id, previous.OnRunnerCall);
        }

        public string Intent { get; }
        public string UserPrompt { get; }
        public string? FwdSystemMessage { get; }
        public List<ForgeLog> ForgedLogs => _forgedSteps;
        
        public List<string> RunnedInstructions { get; private set; }
        public CommandSettings DefaultSettings { get; set; }
        public IChaineable Runner => _current;
        public Dictionary<Guid, Func<IChaineable>> Supporters { get; set; }

        public bool CanRun() => _current != null && !string.IsNullOrEmpty(UserPrompt) && DefaultSettings != null;

        public void OnFeedRequest(Guid id)
        {
            if (TryFindForged(id, out var log))
                onChainFeedResponse(log);


        }

        public void OnFeedResponse(ForgeLog feedLog)
        {
            if (_current != null) return;

            if (!_chainFeeds.Any(log => log.RunnerId == feedLog.RunnerId))
                _chainFeeds.Add(feedLog);
        }

        public void OnRunnerNotification(ForgeLog log, bool updateRunnersLog = true)
        {

            _forgedSteps.Add(log);

            if (updateRunnersLog)
                RunnedInstructions.AddRange(log.RunnersLog);
        }

        public List<ReplayLog> GetReplays()
        {
            if (_replays.Count == 0)
                onReplayRequest();

            return _replays.Count == 0 ? [] : 
                _replays.OrderBy(replay => replay.PassCatchTimestamp).ToList();
        }

        public void OnReplayReport(ReplayLog log)
        {
            if(!_replays.Any(replay => replay.RunnerId == log.RunnerId))
                _replays.Add(log);
        }

        /// <summary>
        /// Use this to clone and swap the ChainRunner in FIRST SUBRUNNERS
        /// By default they get a copy of what has happened so far. 
        /// Also, it is a SDK rule: FIRST SUBRUNNERs are differenced from the main FIRST RUNNER
        /// because the total number of instructions runned in the chain
        /// </summary>
        /// <param name="withRunnedInstructions"></param>
        /// <returns></returns>
        public ChainRunner Clone(bool withRunnedInstructions = true) 
            => Activator.CreateInstance(typeof(ChainRunner), this, withRunnedInstructions) as ChainRunner;

        
        public List<ChainLink> OutputsFromRunned(Guid id)
        {
            var results = new List<ChainLink>();

            if (TryCallSupporter(id, out var runned))
                results.AddRange(runned.Outputs);

            return results;
        }
        public bool TryCallSupporter(Guid id, out IChaineable supporter)
        {
            supporter = Supporters.ContainsKey(id) ? Supporters[id]() : null;

            return supporter != null;
        }
        // IChaineable handleSupporter(reported)

        public bool TryFindForged(Guid id, out ForgeLog forgedLog)
        {
            forgedLog = null;

            if (!_forgedSteps.Any(log => log.RunnerId == id))
            {
                if (TryGetChainFeed(id, out var feedLog))
                    forgedLog = feedLog;
            }
            else forgedLog = _forgedSteps.SingleOrDefault(log => log.RunnerId == id);            
            //else es una subcadena que necesita info (logs) de la principal...
            return forgedLog != null;
        }

        public bool TryGetChainFeed(Guid id, out ForgeLog feedLog)
        {
            feedLog = null;

            if (!_chainFeeds.Any(log => log.RunnerId == id))
                onChainFeedRequest(id);

            if(_chainFeeds.Any(log => log.RunnerId == id))
                feedLog = _chainFeeds.SingleOrDefault(log => log.RunnerId == id);

            return feedLog != null;
        }
    }
}
