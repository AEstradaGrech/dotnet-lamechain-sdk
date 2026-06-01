using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Evaluators;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using System.Text.Json;
using System.Text.Json.Schema;

//Reviews and Rewrites / corrects result
namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Validators
{
    public class JsonOutputReviewCommand<TReviewed> : DbPromptCommand<TReviewed> where TReviewed : class
    {
        public JsonOutputReviewCommand() : base() { }
        public JsonOutputReviewCommand(IOllamaInferenceService ollama) : base(ollama) {}
        public JsonOutputReviewCommand(IOllamaInferenceService ollama, string? systemMessage = null, CommandSettings? settings = null) : base(ollama, systemMessage, settings) { }
        public JsonOutputReviewCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, messageSourceName, messageName, retrieverLambda, guidanceMessage, settings) { }

        public override async Task<TReviewed> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<JsonValidationRequest<TReviewed>>(request);

            var promptRequest = (JsonValidationRequest<TReviewed>)request;

            var systemMessage = await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend);

            systemMessage = systemMessage.Replace("<<PROMPT>>", $"- input: {promptRequest.ValidatedPrompt}\n- output: {promptRequest.RawOutput}\n- instruction: {promptRequest.SystemMessage}").Replace("<<SCHEMA>>", JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(TReviewed)).ToJsonString());

            return await _ollama.CommandPrompt<TReviewed>(request.ToOllamaChat(systemMessage, _settings));
        }

        public override Task<TReviewed> PromptSync(PromptCommandRequest request)
        {
            validateInputRequest<JsonValidationRequest<TReviewed>>(request);

            var promptRequest = (JsonValidationRequest<TReviewed>)request;

            var systemMessage = getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend).Result;

            systemMessage = systemMessage.Replace("<<PROMPT>>", $"- input: {promptRequest.ValidatedPrompt}\n- output: {promptRequest.RawOutput}\n- instruction: {promptRequest.SystemMessage}").Replace("<<SCHEMA>>", JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(TReviewed)).ToJsonString());

            return promptRequest.UseChatEndpoint ?
                _ollama.CommandPrompt<TReviewed>(request.ToOllamaChat(systemMessage, _settings), validations: 0, type: EPromptValidation.REVIEW_ONLY, null, withJsonInfo: true) :
                _ollama.CommandPrompt<TReviewed>(request.ToOllamaGenerate(systemMessage, _settings), validations: 0, type: EPromptValidation.REVIEW_ONLY, null, withJsonInfo: true);
        }

        protected override string getDefaultInstruction() => @"You are a JSON output reviewer. Your task is to review the provided content inside the '<validable-content>' section and correct the content and the format whenever necessary.
Review the provided 'input' and 'instruction' then analyize what is wrong in the 'output' to return corrected version of it.

- Ensure your output is compliant with the VALID OUTPUT SCHEMA. 
- Ensure that the 'output' is strictly compliant with the intruction that generated it in terms of content (analyze if the content is correct and reliable, returns the right number of items, etc).
- Ensure that the format of the 'output' is valid to be serialized to a C# class.

<validable-content>

# PROMPT: 

<<PROMPT>>

# VALID OUTPUT SCHEMA:

<<SCHEMA>>

</validable-content>";
    }
}
