using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Requests;
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

            var cmdRequest = await getGenerateRequest(request);

            cmdRequest.System = cmdRequest.System.Replace("<<PROMPT>>", $"- instruction: {promptRequest.SystemMessage}\n- input:{request.Prompt}").Replace("<<RESPONSE>>", promptRequest.RawOutput).Replace("<<SCHEMA>>", JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(TReviewed)).ToJsonString());

            return await _ollama.CommandPrompt<TReviewed>(cmdRequest);
        }

        public override Task<TReviewed> PromptSync(PromptCommandRequest request)
        {
            validateInputRequest<JsonValidationRequest<TReviewed>>(request);

            var promptRequest = (JsonValidationRequest<TReviewed>)request;

            var cmdRequest = getGenerateRequest(request).Result;

            cmdRequest.System = cmdRequest.System.Replace("<<PROMPT>>", $"- instruction: {promptRequest.SystemMessage}\n- input:{request.Prompt}").Replace("<<RESPONSE>>", promptRequest.RawOutput).Replace("<<SCHEMA>>", JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(TReviewed)).ToJsonString());

            return _ollama.CommandPrompt<TReviewed>(cmdRequest);
        }

        protected override string getDefaultInstruction() => @"
You are going to be provided a USER PROMPT, a JSON RESPONSE for that prompt and a EXPECTED OUTPUT SCHEMA for that response.
Your task is to review the output, compare it with the expected response and return a corrected version of the JSON RESPONSE
or the original version if you consider it is fine.

# IMPORTANT: follow this steps in order to generate your response:

> STEP 1: Analyze the user request and try understand the intent to get an idea of what is the user expecting to receive.
> STEP 2: Analyze the provided EXPECTED OUTPUT SCHEMA to get a clear idea of what is the expected result in terms of format.
> STEP 3: Analyze the provided JSON RESPONSE that you must review and reason if it is valid in terms of content and consistent with the user intent.
> STEP 4: Take into account your analysis from the previous steps to reason and deliberate if the JSON RESPONSE is compliant with the user intent and the EXPECTED OUTPUT SCHEMA
> STEP 5: With the result of your deliberation of STEP 4, return the JSON RESPONSE if you think is compliant or a corrected version of it.

# RULES: take into account this rules when generating your final response:

- Ensure your output is compliant with the requested schema for your validation. 
- Ensure that the format of the JSON RESPONSE is valid to be serialized to a C# class.

# USER PROMPT: 

<<PROMPT>>

# JSON RESPONSE:

<<RESPONSE>>

# EXPECTED OUTPUT SCHEMA:

<<SCHEMA>>
";
    }
}
