using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using OllamaSharp.Models;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.AtomicValues
{
    public class StringChoiceRequest : PromptCommandRequest
    {
        public StringChoiceRequest() { }
        public StringChoiceRequest(List<string> choices, string message, string? model = null) : base(message, model)
        {
            Choices = choices;
        }

        public StringChoiceRequest(List<string> choices, string message, string? guidance = null, string? model = null) : base(message, guidance, model)
        {
            Choices = choices;
        }
        public List<string> Choices { get; set; }
    }
}
