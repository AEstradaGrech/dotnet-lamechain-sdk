namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands
{
    public class VectorSearchRequest : PromptCommandRequest
    {
        public VectorSearchRequest(string index, string query, string embedder, int dimensions, int results, Dictionary<string, object>? filters = null) :  base(message: query, model: embedder)
        {
            QueryIndex = index;
            Dimensions = dimensions;
            ReturnedResults = results;
            Filters = filters ?? new Dictionary<string, object>();
        }
        public string QueryIndex { get; set; }
        public int Dimensions { get; set; }
        public int ReturnedResults { get; set; }
        public Dictionary<string, object> Filters { get; set; }
    }
}
