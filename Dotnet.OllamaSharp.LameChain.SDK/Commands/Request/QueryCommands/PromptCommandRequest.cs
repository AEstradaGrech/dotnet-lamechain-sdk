namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands
{
    public class PromptCommandRequest
    {
        public PromptCommandRequest() { }
        public PromptCommandRequest(string message, string? model = null) 
        {
            Prompt = message;
            Model = model;
        }

        public PromptCommandRequest(string message, string? guidanceMessage, string? model = null) : this(message, model) { GuidanceMessage = guidanceMessage; }
        public string Prompt { get; set; }
        // an extra instruction appart of the _systemMessage stored on construction. Allows to insert data / guidance from events / LLM interactions that might have happened since the instantiation (a chained prompt, for example)
        public string? GuidanceMessage { get; set; } = null; 
        public string? Model { get; set; }

        public Dictionary<string, string> NestedGuidances = new Dictionary<string, string>();// FOR CHAIN SUPPORT --> Step reads its NestedFeeds list -> if feed is tagged as CMD then it creates a request.GuidanceMessage from the feed and adds it here with the subCommandName&Tag to use it

    }
}
