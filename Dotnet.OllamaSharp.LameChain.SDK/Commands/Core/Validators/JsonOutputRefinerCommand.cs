using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Evaluators;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using System.Text.Json;

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Validators
{
    public class JsonOutputRefinerCommand<TRefined> : DbPromptCommand<TRefined> where TRefined : class
    {
        public JsonOutputRefinerCommand() : base() { }
        public JsonOutputRefinerCommand(IOllamaInferenceService ollama) : base(ollama) { }
        public JsonOutputRefinerCommand(IOllamaInferenceService ollama, string? guidanceMessage = null, CommandSettings? settings = null) : base(ollama, guidanceMessage, settings) { }

        //The refiner itself makes no use of the passed messageName, but the messaageSourceName is used for the subValidators (for now. There might be a refiner constructor acceptin source-name KvP's in future releases)
        public JsonOutputRefinerCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null) 
            : base(ollama, messageSourceName, messageName, retrieverLambda, guidanceMessage, settings) { }

        public override async Task<TRefined> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<JsonRefineRequest<TRefined>>(request);

            if (string.IsNullOrEmpty(request.Model) && _settings != null)
                request.Model = string.IsNullOrEmpty(_settings.ValidatorModel) ? _settings.Model : _settings.ValidatorModel;

            var validationReq = (JsonRefineRequest<TRefined>)request;

            ReasonedBoolResponse boolValidation = null;
            switch(validationReq.ValidationType)
            {
                case (EPromptValidation.REVIEW_ONLY):
                    return await reviewResponse(_ollama, validationReq);

                case (EPromptValidation.BOOL_AND_RETRY):
                    boolValidation = await validateResponse(_ollama, validationReq);

                    if (!boolValidation.Answer)
                        throw new InvalidDataException($"{nameof(JsonOutputRefinerCommand<TRefined>)} >> {nameof(validateResponse)} >> VALIDATION FAIL - REASON: {boolValidation.Justification}");

                    else return JsonSerializer.Deserialize<TRefined>(validationReq.RawOutput);
                
                case (EPromptValidation.BOOL_AND_REVIEW):
                    return await validateAndReview(_ollama, validationReq);

                case (EPromptValidation.DOUBLE_BOOL):
                    return await doubleBool(_ollama, validationReq);

                default: return null;
            }
        }

        public override Task<TRefined> PromptSync(PromptCommandRequest request)
        {
            validateInputRequest<JsonRefineRequest<TRefined>>(request);

            if (string.IsNullOrEmpty(request.Model) && _settings != null)
                request.Model = string.IsNullOrEmpty(_settings.ValidatorModel) ? _settings.Model : _settings.ValidatorModel;

            var validationReq = (JsonRefineRequest<TRefined>)request;

            ReasonedBoolResponse boolValidation = null;
            switch (validationReq.ValidationType)
            {
                case (EPromptValidation.REVIEW_ONLY):
                    return reviewResponse(_ollama, validationReq);

                case (EPromptValidation.BOOL_AND_RETRY):
                    boolValidation = validateResponse(_ollama, validationReq).Result;

                    if (!boolValidation.Answer)
                        throw new InvalidDataException($"{nameof(JsonOutputRefinerCommand<TRefined>)} >> {nameof(validateResponse)} >> VALIDATION FAIL - REASON: {boolValidation.Justification}");

                    else return Task.FromResult(JsonSerializer.Deserialize<TRefined>(validationReq.RawOutput));

                case (EPromptValidation.BOOL_AND_REVIEW):
                    return validateAndReview(_ollama, validationReq);

                case (EPromptValidation.DOUBLE_BOOL):
                    return doubleBool(_ollama, validationReq);

                default: return null;
            }
        }

        private Task<TRefined> reviewResponse(IOllamaInferenceService ollama, JsonRefineRequest<TRefined> request, string? guidanceMessage = null)
        {
            var command = _retrieverLambda == null ?
                new JsonOutputReviewCommand<TRefined>(ollama, null, _settings):
                new JsonOutputReviewCommand<TRefined>(ollama, _dbSourceName, "json-review", _retrieverLambda, null, _settings);

            var reviewRequest = toValidationRequest<TRefined>(request);

            reviewRequest.GuidanceMessage = guidanceMessage;
            reviewRequest.ValidatedPrompt = request.ValidatedPrompt;
            reviewRequest.Prompt = $"Review the provided 'output' and return a corrected version. This is the original 'input' : {request.ValidatedPrompt}.";

            if (!string.IsNullOrEmpty(guidanceMessage))
                reviewRequest.Prompt += $"This is the detected problem: {guidanceMessage}";
            //reviewRequest.Prompt += "Correct the JSON RESPONSE content. The JSON RESPONSE output is incorrect, return a correct version. Review the provied 'instruction' and the 'input' and correct the JSON OUTPUT response. Review the INVALID REASON and return a corrected version of the JSON output. Fix the JSON OUTPUT content. Rewrite the content to match the 'input' intent accordingly to its 'instruction";
            return command.PromptSync(reviewRequest);
        }

        private Task<ReasonedBoolResponse> validateResponse(IOllamaInferenceService ollama, JsonRefineRequest<TRefined> request, string? guidanceMessage = null)
        {
            var command = _retrieverLambda == null ?
                new JsonOutputValidationCommand<ReasonedBoolResponse>(ollama, null, _settings) :
                new JsonOutputValidationCommand<ReasonedBoolResponse>(ollama, _dbSourceName, "json-validate", _retrieverLambda, null, _settings);

            var validationRequest = toValidationRequest<ReasonedBoolResponse>(request);

            validationRequest.GuidanceMessage = guidanceMessage;
            validationRequest.ValidatedPrompt = request.ValidatedPrompt;
            validationRequest.Prompt = "Is the provided output content VALID? (anwer true or false).";

            return command.PromptSync(validationRequest);
        }

        private Task<TRefined> validateAndReview(IOllamaInferenceService ollama, JsonRefineRequest<TRefined> request, string? guidanceMessage = null)
        {
            var validation = validateResponse(ollama, request).Result;

            if (validation.Answer) 
                return Task.FromResult(JsonSerializer.Deserialize<TRefined>(request.RawOutput));

            return reviewResponse(ollama, request, $"# WARNING: a previous reviewer has marked the response as INVALID. Take into account the reason to have a better understanding of the problem. INVALID REASON: {validation.Justification}");
        }
        private Task<TRefined> doubleBool(IOllamaInferenceService ollama, JsonRefineRequest<TRefined> request, string? guidanceMessage = null)
        {
            var review = validateAndReview(ollama, request, guidanceMessage).Result;

            request.RawOutput = JsonSerializer.Serialize<TRefined>(review);

            var validation = validateResponse(ollama, request).Result;

            if(!validation.Answer)
                throw new InvalidDataException($"{nameof(JsonOutputRefinerCommand<TRefined>)} >> {nameof(validateResponse)} >> VALIDATION FAIL - REASON: {validation.Justification}");

            return Task.FromResult(review);
        }

        JsonValidationRequest<TValResult> toValidationRequest<TValResult>(JsonRefineRequest<TRefined> request)
            => new JsonValidationRequest<TValResult> {
                Model = string.IsNullOrEmpty(request.Model) ? _settings.ValidatorModel : request.Model,
                Prompt = request.Prompt,
                SystemMessage = request.SystemMessage,
                RawOutput = request.RawOutput,
                ResponseExamples = request.ResponseExamples,
                UseChatEndpoint = request.UseChatEndpoint
            };
    }
}
