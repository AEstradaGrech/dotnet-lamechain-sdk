namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands
{
    public class SmartQueryRequest : VectorSearchRequest
    {
        public List<string> CollectionChoices { get; set; }
        public int MaxReturnedChoices { get; set; }
        public SmartQueryRequest(string prompt, List<string> collectionChoices, int maxChoices, int resultsPerChoice, string? guidanceMessage = null, int dimensions = 512, string? model = null, Dictionary<string, object> filters = null) 
            : base(index: string.Empty, prompt, model, dimensions, resultsPerChoice, filters)
        {
            GuidanceMessage = guidanceMessage;
            CollectionChoices = collectionChoices;
            MaxReturnedChoices = maxChoices;
        }
    }
}
