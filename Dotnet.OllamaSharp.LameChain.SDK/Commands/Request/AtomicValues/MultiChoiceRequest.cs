using OllamaSharp.Models;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.AtomicValues
{
    public class MultiChoiceRequest : StringChoiceRequest
    {
        public MultiChoiceRequest() : base() { }
        public MultiChoiceRequest(int selections, List<string> choices, string message, string? guidance = null, string? model = null) : base(choices, message, guidance, model)
        {
            MaxSelections = selections;
        }
        public int MaxSelections { get; set; }
    }
}
