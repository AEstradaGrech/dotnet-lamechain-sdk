namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands
{
    public class RagExpansionRequest : PromptCommandRequest
    {
        public RagExpansionRequest() : base () { }
        public RagExpansionRequest(string message, string? guidanceMessage, bool isGuidanceAppend, string? model = null) : base(message, guidanceMessage, isGuidanceAppend, model) { }

        public RagExpansionRequest(int expansions, string message, bool withFewShot = false, int maxExamples = 1, string? guidanceMessage = null, bool isGuidanceAppend = false, string? model = null) 
            : this(message, guidanceMessage, isGuidanceAppend, model) 
        {
            Results = expansions;
            MaxExamples = maxExamples;
            UsePrevAsExample = withFewShot;
        }
        public int Results { get; set; } = 1;
        public int MaxExamples { get; set; } = int.MaxValue;
        public bool UsePrevAsExample { get; set;  } // if true and Results > 1 every iteration will append the previous results as few shot examples
    }
}
