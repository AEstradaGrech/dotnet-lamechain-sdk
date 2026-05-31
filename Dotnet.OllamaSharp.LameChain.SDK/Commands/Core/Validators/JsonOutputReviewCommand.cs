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

            systemMessage = systemMessage.Replace("<<PROMPT>>", $"- instruction: {promptRequest.SystemMessage}\n- input: {promptRequest.ValidatedPrompt}").Replace("<<RESPONSE>>", promptRequest.RawOutput).Replace("<<SCHEMA>>", JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(TReviewed)).ToJsonString());

            return await _ollama.CommandPrompt<TReviewed>(request.ToOllamaChat(systemMessage, _settings));
        }

        public override Task<TReviewed> PromptSync(PromptCommandRequest request)
        {
            validateInputRequest<JsonValidationRequest<TReviewed>>(request);

            var promptRequest = (JsonValidationRequest<TReviewed>)request;

            var systemMessage = getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend).Result;

            //systemMessage = systemMessage.Replace("<<PROMPT>>", $">> INSTRUCTION TO VALIDATE: {promptRequest.SystemMessage}\n>> INPUT TO VALIDATE: {promptRequest.ValidatedPrompt}").Replace("<<RESPONSE>>", promptRequest.RawOutput).Replace("<<SCHEMA>>", JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(TReviewed)).ToJsonString());
            systemMessage = systemMessage.Replace("<<PROMPT>>", $"- input: {promptRequest.ValidatedPrompt}\n- output: {promptRequest.RawOutput}\n- instruction: {promptRequest.SystemMessage}").Replace("<<RESPONSE>>", promptRequest.RawOutput).Replace("<<SCHEMA>>", JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(TReviewed)).ToJsonString());

            return promptRequest.UseChatEndpoint ?
                _ollama.CommandPrompt<TReviewed>(request.ToOllamaChat(systemMessage, _settings), validations: 0, type: EPromptValidation.REVIEW_ONLY, null, withJsonInfo: true) :
                _ollama.CommandPrompt<TReviewed>(request.ToOllamaGenerate(systemMessage, _settings), validations: 0, type: EPromptValidation.REVIEW_ONLY, null, withJsonInfo: true);
        }

        protected override string getDefaultInstruction() => @"You are a JSON output reviewer. Your task is to review the provided content inside the '<validable-content>' section an correct the content and the format whenever necessary.
Review the provided 'input' and 'instruction' then analyize what is wrong in the 'output' to return corrected version of it.

- Ensure your output is compliant with the VALID OUTPUT SCHEMA. 
- Ensure that the 'output' is strictly compliant with the intruction that generated it in terms of content (analyze if the content is correct and reliable, returns the right number of items, etc).
- Ensure that the format of the'output' is valid to be serialized to a C# class.

<validable-content>

# PROMPT: 

<<PROMPT>>

# VALID OUTPUT SCHEMA:

<<SCHEMA>>

</validable-content>";


        //protected override string getDefaultInstruction() => @"
        //You are going to be provided a PROMPT containing an 'instruction' and an 'input', a JSON RESPONSE for that prompt and a EXPECTED OUTPUT SCHEMA for that response.
        //Your task is to validate the provided JSON RESPONSE and determine if it is compliant with the content of the provided PROMPT and the expected response according to the provided EXPECTED OUTPUT SCHEMA.

        //Analyze the content that is inside the '<validable-content>' section and correct the JSON RESPONSE content in case it is wrong or INVALID according to the user query intent or the requested format.
        //Review carefully the 'INSTRUCTION TO VALIDATE' and the 'INPUT TO VALIDATE' and take into account any provided information about what is wrong in the JSON RESPONSE to return a corrected version of the output
        //that is compliant with the user intent and any specified output requirement related to it.

        //# IMPORTANT: follow this steps in order to generate your response:

        //> STEP 1: Analyze carefully the content to validate that is enclosed within the '<validable-content>' '</validable-content'> tags.
        //> STEP 2: Reason if the provided JSON RESPONSE content is consistent with the user query.
        //> STEP 3: Deliberate if the provided JSON RESPONSE content is valid or it should be corrected
        //> STEP 4: Taking into account your reasonaments from the previous steps, return the original response without any modification IF the content is correct or return a corrected version in case the original content is wrong or invalid.

        //# RULES: take into account this rules when generating your final response:

        //- Ensure your output is compliant with the EXPECTED OUTPUT SCHEMA. 
        //- Ensure that the JSON RESPONSE is strictly compliant with the intruction that generated it in terms of content (analyze if the content is what the user expected, returns the right number of items, etc).
        //- Ensure that the format of the JSON RESPONSE is valid to be serialized to a C# class.

        //<validable-content>

        //# PROMPT: 

        //<<PROMPT>>

        //# JSON RESPONSE:

        //<<RESPONSE>>

        //# EXPECTED OUTPUT SCHEMA:

        //<<SCHEMA>>

        //</validable-content>";

        //protected override string getDefaultInstruction() => @"
        //   You are going to be provided  a PROMPT containing an 'instruction + input', a JSON RESPONSE for that prompt and a EXPECTED OUTPUT SCHEMA for that response to validate.
        //   Your task is to review the output, compare it with the expected response and return a corrected version of the JSON RESPONSE
        //   or the original response if you consider it is correct in terms of content and format.

        //   # IMPORTANT: follow this steps in order to generate your response:

        //   > STEP 1: Analyze the user request and try understand the intent to get an idea of what is the user expecting to receive.
        //   > STEP 2: Review CAREFULLY the content of the 'instruction' that generated the JSON RESPONSE and validate that the response content is absolutely compliant with every instruction rule and constraint.
        //   > STEP 3: Analyze the provided EXPECTED OUTPUT SCHEMA to get a clear idea of what is the expected result in terms of format.
        //   > STEP 4: Analyze the provided JSON RESPONSE that you must review and reason if it is valid in terms of content and consistent with the user intent.
        //   > STEP 5: Take into account your analysis from the previous steps to reason and deliberate if the JSON RESPONSE is compliant with the user intent and the EXPECTED OUTPUT SCHEMA and the content is not wrong.
        //   > STEP 6: With the result of your deliberations of STEP 4 and STEP 5, return the JSON RESPONSE if you think is compliant and the content correct, or a corrected version of it.

        //   # RULES: take into account this rules when generating your final response:

        //   - Ensure your output is compliant with the requested schema for your validation. 
        //   - Ensure that the JSON RESPONSES is strictly compliant with the intruction that generated it in terms of content (analyze if the content is what the user expected, returning the right number of items etc)
        //   - Ensure that the format of the JSON RESPONSE is valid to be serialized to a C# class.

        //   # PROMPT: 

        //   <<PROMPT>>

        //   # JSON RESPONSE:

        //   <<RESPONSE>>

        //   # EXPECTED OUTPUT SCHEMA:

        //   <<SCHEMA>>
        //   //";

        //        protected override string getDefaultInstruction() => @"
        //You are going to be provided a USER PROMPT, a JSON RESPONSE for that prompt and a EXPECTED OUTPUT SCHEMA for that response.
        //Your task is to review the output, compare it with the expected response and return a corrected version of the JSON RESPONSE
        //or the original version if you consider it is fine.

        //# IMPORTANT: follow this steps in order to generate your response:

        //> STEP 1: Analyze the user request and try understand the intent to get an idea of what is the user expecting to receive.
        //> STEP 2: Analyze the provided EXPECTED OUTPUT SCHEMA to get a clear idea of what is the expected result in terms of format.
        //> STEP 3: Analyze the provided JSON RESPONSE that you must review and reason if it is valid in terms of content and consistent with the user intent.
        //> STEP 4: Take into account your analysis from the previous steps to reason and deliberate if the JSON RESPONSE is compliant with the user intent and the EXPECTED OUTPUT SCHEMA
        //> STEP 5: With the result of your deliberation of STEP 4, return the JSON RESPONSE if you think is compliant or a corrected version of it.

        //# RULES: take into account this rules when generating your final response:

        //- Ensure your output is compliant with the requested schema for your validation. 
        //- Ensure that the format of the JSON RESPONSE is valid to be serialized to a C# class.

        //# USER PROMPT: 

        //<<PROMPT>>

        //# JSON RESPONSE:

        //<<RESPONSE>>

        //# EXPECTED OUTPUT SCHEMA:

        //<<SCHEMA>>
        //";
    }
}
