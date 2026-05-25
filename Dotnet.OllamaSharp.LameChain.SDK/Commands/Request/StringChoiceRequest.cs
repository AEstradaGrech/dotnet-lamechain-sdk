using OllamaSharp.Models;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Requests
{
    public class StringChoiceRequest : PromptCommandRequest
    {
        public StringChoiceRequest() { }
        public StringChoiceRequest(List<string> choices, string message, string? model = null) : base(message, model)
        {
            Choices = choices;
        }
        public List<string> Choices { get; set; }
    }
}
