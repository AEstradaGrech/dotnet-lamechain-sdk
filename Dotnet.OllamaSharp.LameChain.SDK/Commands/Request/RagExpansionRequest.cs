namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Requests
{
    public class RagExpansionRequest : PromptCommandRequest
    {
        public int Results { get; set; } = 1;
        public int MaxExamples { get; set; } = int.MaxValue;
        public bool UsePrevAsExample { get; set;  } // if true and Results > 1 every iteration will append the previous results as few shot examples
    }
}
