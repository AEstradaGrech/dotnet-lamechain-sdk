using OllamaSharp.Models;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Requests
{
    public class MultiChoiceRequest : StringChoiceRequest
    {
        public MultiChoiceRequest() : base() { }
        public MultiChoiceRequest(int selections, List<string> choices, string message, string? model = null) : base(choices, message, model)
        {
            MaxSelections = selections;
        }
        public int MaxSelections { get; set; }
    }
}
