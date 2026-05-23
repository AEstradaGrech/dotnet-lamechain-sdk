using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Requests
{
    public class JsonRefineRequest<TRefined> : JsonValidationRequest<TRefined> where TRefined : class
    {
        public TRefined Result { get; set; }
        public EPromptValidation ValidationType { get; set; }
    }
}
