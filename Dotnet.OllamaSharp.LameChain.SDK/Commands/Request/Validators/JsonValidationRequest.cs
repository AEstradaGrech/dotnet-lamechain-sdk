using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Evaluators
{
    /// <summary>
    /// Model to review and correct JSON outputs
    /// Prompt = the validator user prompt (this is the actual instruction for the validator. Change it depending on the type of validation process you need)
    /// ValidatedPrompt = the user input that generated the RawOutput
    /// Guidance = guidance message to help the LLM (this is used in the RefinerCommand to pass the results of other validators)
    /// SystemMessage = the instruction that generated the RawOutput
    /// RawOutput = the output to review / validate
    /// 
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    public class JsonValidationRequest<TModel> : PromptCommandRequest
    {
        /// <summary>
        /// System message to validate (the instruction that generated the RawOutput)
        /// </summary>
        public string SystemMessage { get; set; }
        /// <summary>
        /// Output to validate
        /// </summary>
        public string RawOutput { get; set; }

        /// <summary>
        /// The user prompt that generated the RawOutput
        /// </summary>
        public string ValidatedPrompt { get; set; }

        public string? ResponseExamples { get; set; } // o DB (para domain commands) o Task<Message> GenerateFewShotExamplesCommand<TModel>()

        public bool UseChatEndpoint { get; set; }
    }
}
