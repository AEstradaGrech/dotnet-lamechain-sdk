using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Evaluators
{
    public class JsonValidationRequest<TModel> : PromptCommandRequest
    {
        public string SystemMessage { get; set; }
        public string RawOutput { get; set; }
        public string? ResponseExamples { get; set; } // o DB (para domain commands) o Task<Message> GenerateFewShotExamplesCommand<TModel>()
    }
}
