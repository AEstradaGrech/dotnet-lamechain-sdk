using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Evaluators;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using System.Text.Json;
using System.Text.Json.Schema;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Validators
{
    public class JsonOutputValidationCommand<TModel> : DbPromptCommand<ScoredBoolResponse>
    {
        public JsonOutputValidationCommand() : base() { }
        public JsonOutputValidationCommand(IOllamaInferenceService ollama) : base(ollama) { }
        //USAGE WITH DEFAULT INSTRUCTION
        public JsonOutputValidationCommand(IOllamaInferenceService ollama, string? guidanceMessage = null, CommandSettings? settings = null) : base(ollama, guidanceMessage, settings) { }
        //USAGE WITH DB RETRIEVED INSTRUCTION
        public JsonOutputValidationCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, messageSourceName, messageName, retrieverLambda, guidanceMessage, settings) { }

        public override async Task<ScoredBoolResponse> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<JsonValidationRequest<TModel>>(request);

            var promptRequest = (JsonValidationRequest<TModel>)request;

            var cmdRequest = await getGenerateRequest(request);

            cmdRequest.System = cmdRequest.System
                .Replace("<<PROMPT>>", $"- instruction: {promptRequest.SystemMessage}\n- input:{request.Prompt}")
                .Replace("<<RESPONSE>>", promptRequest.RawOutput)
                .Replace("<<SCHEMA>>", JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(TModel)).ToJsonString());
 
            return await _ollama.CommandPrompt<ScoredBoolResponse>(cmdRequest);
        }
        public override Task<ScoredBoolResponse> PromptSync(PromptCommandRequest request)
        {
            if (request.GetType() != typeof(JsonValidationRequest<TModel>))
                throw new InvalidOperationException($"{nameof(JsonOutputValidationCommand<TModel>)} >> INVALID REQUEST TYPE ({request.GetType().Name}) IS NOT OF TYPE {nameof(JsonValidationRequest<TModel>)}");

            var promptRequest = (JsonValidationRequest<TModel>)request;

            var cmdRequest = getGenerateRequest(request).Result;

            cmdRequest.System = cmdRequest.System
                .Replace("<<PROMPT>>", $"- instruction: {promptRequest.SystemMessage}\n- input:{request.Prompt}")
                .Replace("<<RESPONSE>>", promptRequest.RawOutput)
                .Replace("<<SCHEMA>>", JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(TModel)).ToJsonString());

            return _ollama.CommandPrompt<ScoredBoolResponse>(cmdRequest);
        }

        protected override string getDefaultInstruction() => @"
You are going to be provided a PROMPT containing an 'instruction' and an 'input', a JSON RESPONSE for that prompt and a EXPECTED OUTPUT SCHEMA for that response.
Your task is to validate the provided JSON RESPONSE and determine if it is compliant with the content of the provided PROMPT and the expected response according to the provided EXPECTED OUTPUT SCHEMA.

You must output your response in JSON format according to this fields:

- Answer: boolean value to indicate 'VALID' or 'NOT VALID' according to the result of your deliberation.
- Justification: a brief text (not more than 10 words) summarizing the reason of your boolean answer.
- Confidence: a float value ranging from 0.0 to 1.0 to indicate how sure you are about your answer.

# IMPORTANT: follow this steps in order to generate your response:

> STEP 1: Analyze the user request and try understand the intent to get an idea of what is the user expecting to receive.
> STEP 2: Review CAREFULLY the content of the instruction that generated the JSON RESPONSE and validate that the response content is absolutely compliant with every instruction rule and constraint.
> STEP 3: Analyze the provided EXPECTED OUTPUT SCHEMA to get a clear idea of what is the expected result in terms of format.
> STEP 4: Analyze the provided JSON RESPONSE that you must review and reason if it is valid in terms of content and consistent with the user intent and the provided PROMPT 'instruction'.
> STEP 5: Use your conclussions of the previous steps to generate your response according to the requested JSON schema.

# RULES: take into account this rules when generating your final response:

- Ensure your output is compliant with the requested schema for your validation. 
- Ensure that the JSON RESPONSES is strictly compliant with the intruction that generated it in terms of content (analyze if the content is what the user expected, returning the right number of items etc)
- Ensure that the format of the JSON RESPONSE is valid to be serialized to a C# class.

# PROMPT: 

<<PROMPT>>

# JSON RESPONSE:

<<RESPONSE>>

# EXPECTED OUTPUT SCHEMA:

<<SCHEMA>>
";
    }
}
