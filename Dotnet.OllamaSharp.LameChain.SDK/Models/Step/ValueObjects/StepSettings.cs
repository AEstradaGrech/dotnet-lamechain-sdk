using Dotnet.OllamaSharp.LameChain.SDK.Command.Requests;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects
{                                              
    public class StepSettings
    {
        private const string _randomBoostTag = "RNDM_SET";
        private const string _boostersSectionTag = "## ADDITIONAL INFORMATION: "; 
        public StepSettings() 
        {
            ChainFeeds = new List<Guid>();
            Boosters = new List<KeyValuePair<string, List<string>>>();
        }

        public StepSettings(PromptCommandRequest request) : this()
        {
            CommandRequest = request;
        }

        public PromptCommandRequest CommandRequest { get; }
        // Runner Ids to feed the context from
        public List<Guid> ChainFeeds { get; set; } 
        // Additional sources of data to enhance the prompt, add few-shot examples or whatever This will be appended at the end of the system message as "#ADDITIONAL INFORMATION: <rebujito feed message to guide / interpret the feeds>
        public List<KeyValuePair<string,List<string>>> Boosters { get; set;  }  
        public StepSettings FeedFrom(Guid id)
        {
            if (!ChainFeeds.Contains(id))
                ChainFeeds.Add(id);

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
