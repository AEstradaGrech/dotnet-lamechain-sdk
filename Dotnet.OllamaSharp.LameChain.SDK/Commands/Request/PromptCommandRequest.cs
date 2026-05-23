using OllamaSharp.Models;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Requests
{
    public class PromptCommandRequest
    {
        public PromptCommandRequest() { }
        public PromptCommandRequest(string message, RequestOptions? settings = null, string? model = null) 
        {
            Prompt = message;
            Settings = settings;
            Model = model;

        }
        public PromptCommandRequest(string message, string? guidanceMessage, RequestOptions? settings = null, string? model = null) : this(message, settings, model) { GuidanceMessage = guidanceMessage; }
        public string Prompt { get; set; }
        // an extra instruction appart of the _systemMessage stored on construction. Allows to insert data / guidance from events / LLM interactions that might have happened since the instantiation (a chained prompt, for example)
        public string? GuidanceMessage { get; set; } = null; 
        public string? Model { get; set; }

        //TODO: eliminar (van en CommandSettings.ToOllamaRequest())
        public RequestOptions? Settings { get; set; }
    }
}
