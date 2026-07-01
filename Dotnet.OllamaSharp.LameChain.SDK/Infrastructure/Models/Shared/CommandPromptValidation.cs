using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Validators;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared
{
    public class CommandPromptValidation<T> where T : class
    {
        public int Validations { get; set; }
        public EPromptValidation ValidationType { get; set; }
        public JsonOutputRefinerCommand<T> Validator { get; set; }
        public bool UseChatEndpoint { get; set; } = true;
    }
}
