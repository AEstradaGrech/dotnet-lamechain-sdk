using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects
{                                              
    public class StepSettings
    {
        private const string _randomBoostTag = "RNDM_SET";
        private const string _boostersSectionTag = "## ADDITIONAL INFORMATION: ";
        public IJsoneable Command { get; }
        public bool WithFullContext { get; private set; }
        public bool WithPrevSchema { get; private set; }
        public string? ForwardMessage { get; private set; }
        public StepSettings() 
        {
            ChainFeeds = new List<Func<Guid>>();
            NestFeeds = new Dictionary<string, List<Func<Guid>>>();
            Boosters = new List<KeyValuePair<string, List<string>>>();
            CommandRequest = new PromptCommandRequest();
            ForwardMessage = null;
            WithFullContext = true;
            WithPrevSchema = false;
        }

        public StepSettings(PromptCommandRequest request, string? feedFwd = null, bool withFullContext = true, bool withPrevSchema = false) : this()
        {
            CommandRequest = request;
            ForwardMessage = feedFwd;
            WithFullContext = withFullContext;
            WithPrevSchema = withPrevSchema;
        }

        public StepSettings(IJsoneable command, string? feedFwd = null, string? requestPrompt = null, bool isGuidanceAppend = false, bool withFullContext = true, bool withPrevSchema = false) : this()
        {
            CommandRequest = new PromptCommandRequest(requestPrompt ?? string.Empty, isGuidanceAppend);
            Command = command;
            ForwardMessage = feedFwd;
            WithFullContext = withFullContext;
            WithPrevSchema = withPrevSchema;
        }

        public StepSettings(IJsoneable command, PromptCommandRequest request, string? feedFwd = null, bool withFullContext = true, bool withPrevSchema = false) : this()
        {
            CommandRequest = request;
            Command = command;
            ForwardMessage = feedFwd;
            WithFullContext = withFullContext;
            WithPrevSchema = withPrevSchema;
        }

        public StepSettings(StepSettings cloned, bool cloneFeeds = true, bool cloneBoosters = true)
        {
            WithFullContext = cloned.WithFullContext;
            WithPrevSchema = cloned.WithPrevSchema;
            ForwardMessage = cloned.ForwardMessage;
            CommandRequest = cloned.CommandRequest.Clone();
            ChainFeeds = cloneFeeds ? new List<Func<Guid>>(cloned.ChainFeeds) : [];
            NestFeeds = cloneFeeds ? new Dictionary<string, List<Func<Guid>>>(cloned.NestFeeds) : new Dictionary<string, List<Func<Guid>>>();
            Boosters = cloneBoosters ? new List<KeyValuePair<string, List<string>>>(cloned.Boosters) : [];
        }

        public PromptCommandRequest CommandRequest { get; }
        // Runner Ids to feed the context from
        public List<Func<Guid>> ChainFeeds { get; set; } 
        public Dictionary<string, List<Func<Guid>>> NestFeeds { get; set; } // feed specific commands in a multicommand (executes nested commands)
        // Additional sources of data to enhance the prompt, add few-shot examples or whatever This will be appended at the end of the system message as "#ADDITIONAL INFORMATION: <rebujito feed message to guide / interpret the feeds>
        public List<KeyValuePair<string,List<string>>> Boosters { get; set;  }

        public StepSettings Clone(bool withFeeds = true, bool withBoosters = true) => new StepSettings(cloned: this, withFeeds, withBoosters);

        public StepSettings FeedFrom(Func<Guid> id)
        {
            if (!ChainFeeds.Contains(id))
                ChainFeeds.Add(id);

            return this;
        }

        public StepSettings WithNestedFeed(string commandName, List<Func<Guid>> feeds, bool isForStep)
        {
            var fullName = isForStep ? 
                $"{commandName}-{NestFeeds.Keys.Count(key => key.Contains(commandName))}-STEP" : 
                $"{commandName}-{NestFeeds.Keys.Count(key => key.Contains(commandName))}";
            
            if (!NestFeeds.ContainsKey(fullName))
                NestFeeds.Add(fullName, feeds);

            return this;
        }

        public StepSettings WithDataBoost(string? feedMsg, List<string> sources)
        {
            var key = feedMsg.Trim();

            if (string.IsNullOrEmpty(key))
                key = _randomBoostTag;

            //You can repeat an 'instruction' (feedMsg) with different sources too like
            // 'Analyze the stock data and do X, [stock data list from API-A]'
            // 'Analyze the stock data and do X, [stock data list from API-B]'
            Boosters.Add(new KeyValuePair<string, List<string>>(key, sources));

            return this;
        }

        public string BoostersFeedText()
        {
            if (Boosters.Count == 0) return string.Empty;
            
            var sb = new StringBuilder()
                .Append(_boostersSectionTag);

            Boosters.ForEach(kvp =>
            {
                if (kvp.Key != _randomBoostTag)
                {
                    sb.AppendLine()
                      .AppendLine(kvp.Key);
                }

                kvp.Value.ForEach(source => 
                    sb.AppendLine()
                      .AppendLine(source)
                );
            });

            return sb.ToString().Trim();
        }
    }
}
