using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Evaluators;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using System.Text.Json;
using System.Text.Json.Schema;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Validators
{
    public class JsonOutputValidationCommand<TModel> : DbPromptCommand<ReasonedBoolResponse>
    {
        public JsonOutputValidationCommand() : base() { }
        public JsonOutputValidationCommand(IOllamaInferenceService ollama) : base(ollama) { }
        //USAGE WITH DEFAULT INSTRUCTION
        public JsonOutputValidationCommand(IOllamaInferenceService ollama, string? guidanceMessage = null, CommandSettings? settings = null) : base(ollama, guidanceMessage, settings) { }
        //USAGE WITH DB RETRIEVED INSTRUCTION
        public JsonOutputValidationCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, messageSourceName, messageName, retrieverLambda, guidanceMessage, settings) { }

        public override async Task<ReasonedBoolResponse> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<JsonValidationRequest<TModel>>(request);

            var promptRequest = (JsonValidationRequest<TModel>)request;

            var systemMessage = await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend);

            systemMessage = systemMessage
                   .Replace("<<PROMPT>>", $"- input: {promptRequest.ValidatedPrompt}\n- output: {promptRequest.RawOutput}\n- instruction: {promptRequest.SystemMessage}")
                   .Replace("<<SCHEMA>>", JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(TModel)).ToJsonString());

            return promptRequest.UseChatEndpoint ?
                await _ollama.CommandPrompt<ReasonedBoolResponse>(request.ToOllamaChat(systemMessage, _settings)) :
                await _ollama.CommandPrompt<ReasonedBoolResponse>(request.ToOllamaGenerate(systemMessage, _settings));
        }

        public override Task<ReasonedBoolResponse> PromptSync(PromptCommandRequest request)
        {
            if (request.GetType() != typeof(JsonValidationRequest<TModel>))
                throw new InvalidOperationException($"{nameof(JsonOutputValidationCommand<TModel>)} >> INVALID REQUEST TYPE ({request.GetType().Name}) IS NOT OF TYPE {nameof(JsonValidationRequest<TModel>)}");

            var promptRequest = (JsonValidationRequest<TModel>)request;

            var systemMessage = getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend).Result;

            systemMessage = systemMessage
                   .Replace("<<PROMPT>>", $"- input: {promptRequest.ValidatedPrompt}\n- output: {promptRequest.RawOutput}\n- instruction: {promptRequest.SystemMessage}")
                   .Replace("<<SCHEMA>>", JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(TModel)).ToJsonString());

            return promptRequest.UseChatEndpoint ?
                _ollama.CommandPrompt<ReasonedBoolResponse>(request.ToOllamaChat(systemMessage, _settings)) :
                _ollama.CommandPrompt<ReasonedBoolResponse>(request.ToOllamaGenerate(systemMessage, _settings));
        }

        protected override string getDefaultInstruction() => @" You are a JSON output validator. Your task is to review the '<validable-content>' section and evaluate the provided 'input', 'output' and 'instruction' to determine if the
the provided 'output' content is correct and consistent with the 'input' and the 'instruction' that generated it, and also validate if it is compliant with the VALIDATED OUTPUT SCHEMA.

You must output YOUR response in JSON format according to this fields:

- Answer: boolean value to indicate 'OUTPUT IS VALID' or 'OUTPUT IS NOT VALID' in content and format according to the result of your deliberation.
- Justification: a brief yet accurate text explaining the reason of your boolean answer.

# RULES: take into account this rules when generating your final response:

- Ensure that your 'answer' boolean represents 'VALID OUTPUT CONTENT' or 'INVALID OUTPUT CONTENT (analyze validable content and answer 'true' for valid or 'false' for invalid.'
- Ensure that the provided 'output', is strictly compliant with the 'intruction' that generated it in terms of content (analyze if the content is what the user expected, returning the right number of items etc).
- Ensure that YOUR output is compliant with YOUR provided JSON schema. 

## IMPORTANT: Your task is to review and validate the provided 'output' and answer 'true' if the content is valid and consistent or 'false' if the content is wrong.
## IMPORTANT: DO NOT answer to the previous 'input', YOUR boolean answer MUST express the result of YOUR VALIDATION.

<validable-content>

# PROMPT: 

<<PROMPT>>

# VALIDATED OUTPUT SCHEMA:

<<SCHEMA>>

</validable-content>
";

        // 0.2.0-alpha-002 
        //        protected override string getDefaultInstruction() => @" You are a JSON output validator. Your task is to review the '<validable-content>' section and evaluate the provided 'input', 'output' and 'instruction' to determine if the
        //the provided 'output' content is correct and consistent with the 'input' and the 'instruction' that generated it, and also validate if it is compliant with the VALIDATED OUTPUT SCHEMA.

        //You must output your response in JSON format according to this fields:

        //- Answer: boolean value to indicate 'OUTPUT IS VALID' or 'OUTPUT IS NOT VALID' in content and format according to the result of your deliberation.
        //- Justification: a brief yet accurate text explaining the reason of your boolean answer.

        //# IMPORTANT: follow this steps in order to generate your response:

        //> STEP 1: Analyze the provided 'input' and try to understand the intent to get an idea of what is the user expecting to receive.
        //> STEP 2: Review CAREFULLY the content of the 'instruction' that generated the provided 'output' and validate that the response content is absolutely compliant with every instruction rule and constraint.
        //> STEP 3: Analyze the provided VALIDATED OUTPUT SCHEMA to get a clear idea of what is the expected result in terms of format.
        //> STEP 4: Analyze the provided 'output' reason if it is valid in terms of content and consistent with the 'input' and 'instruction' intent.
        //> STEP 5: Use your conclussions of the previous steps to generate your response according to your response JSON schema.

        //# RULES: take into account this rules when generating your final response:

        //- Ensure your output is compliant with the requested schema for your validation. 
        //- Ensure that the provided 'output', is strictly compliant with the 'intruction' that generated it in terms of content (analyze if the content is what the user expected, returning the right number of items etc).
        //- Ensure that your 'answer' boolean represnts 'VALID OUTPUT CONTENT' or 'INVALID OUTPUT CONTENT'
        //- Ensure that the format of the validated 'output' is valid to be serialized to a C# class.

        //<validable-content>

        //# PROMPT: 

        //<<PROMPT>>

        //# VALIDATED OUTPUT SCHEMA:

        //<<SCHEMA>>

        //</validable-content>
        //";
    }
}
