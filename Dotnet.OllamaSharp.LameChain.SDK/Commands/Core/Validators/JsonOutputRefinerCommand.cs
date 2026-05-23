using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Requests;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutput;
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

            var validationReq = (JsonRefineRequest<TRefined>)request;

            ScoredBoolResponse boolValidation = null;
            switch(validationReq.ValidationType)
            {
                case (EPromptValidation.REVIEW_ONLY):
                    return await reviewResponse(_ollama, validationReq);

                case (EPromptValidation.BOOL_AND_RETRY):
                    boolValidation = await validateResponse(_ollama, validationReq);

                    if (!boolValidation.Answer)
                        throw new InvalidDataException($"{nameof(JsonOutputRefinerCommand<TRefined>)} >> {nameof(validateResponse)} >> VALIDATION FAIL - REASON: {boolValidation.Justification} >> CONFIDENCE: {boolValidation.Score}");

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

            var validationReq = (JsonRefineRequest<TRefined>)request;

            ScoredBoolResponse boolValidation = null;
            switch (validationReq.ValidationType)
            {
                case (EPromptValidation.REVIEW_ONLY):
                    return reviewResponse(_ollama, validationReq);

                case (EPromptValidation.BOOL_AND_RETRY):
                    boolValidation = validateResponse(_ollama, validationReq).Result;

                    if (!boolValidation.Answer)
                        throw new InvalidDataException($"{nameof(JsonOutputRefinerCommand<TRefined>)} >> {nameof(validateResponse)} >> VALIDATION FAIL - REASON: {boolValidation.Justification} >> CONFIDENCE: {boolValidation.Score}");

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
                new JsonOutputReviewCommand<TRefined>(ollama, guidanceMessage, _settings):
                new JsonOutputReviewCommand<TRefined>(ollama, _dbSourceName, "json-review", _retrieverLambda, guidanceMessage, _settings);

            return command.PromptSync(toValidationRequest<TRefined>(request));
        }

        private Task<ScoredBoolResponse> validateResponse(IOllamaInferenceService ollama, JsonRefineRequest<TRefined> request, string? guidanceMessage = null)
        {
            var command = _retrieverLambda == null ?
                new JsonOutputValidationCommand<ScoredBoolResponse>(ollama, guidanceMessage, _settings) :
                new JsonOutputValidationCommand<ScoredBoolResponse>(ollama, _dbSourceName, "json-validate", _retrieverLambda, guidanceMessage, _settings);

            return command.PromptSync(toValidationRequest<ScoredBoolResponse>(request));
        }

        private Task<TRefined> validateAndReview(IOllamaInferenceService ollama, JsonRefineRequest<TRefined> request, string? guidanceMessage = null)
        {
            var validation = validateResponse(ollama, request).Result;

            if (validation.Answer) 
                return Task.FromResult(JsonSerializer.Deserialize<TRefined>(request.RawOutput));

            return reviewResponse(ollama, request, $"# WARNING: a previous reviewer has marked the response as INVALID. Take into account the reason to have a better understanding of the problem. Reason: {validation.Justification}");
        }
        private Task<TRefined> doubleBool(IOllamaInferenceService ollama, JsonRefineRequest<TRefined> request, string? guidanceMessage = null)
        {
            var review = validateAndReview(ollama, request, guidanceMessage).Result;

            request.RawOutput = JsonSerializer.Serialize<TRefined>(review);

            var validation = validateResponse(ollama, request).Result;

            if(!validation.Answer)
                throw new InvalidDataException($"{nameof(JsonOutputRefinerCommand<TRefined>)} >> {nameof(validateResponse)} >> VALIDATION FAIL - REASON: {validation.Justification} >> CONFIDENCE: {validation.Score}");

            return Task.FromResult(review);
        }

        JsonValidationRequest<TValResult> toValidationRequest<TValResult>(JsonRefineRequest<TRefined> request)
            => new JsonValidationRequest<TValResult> {
                Model = request.Model,
                Prompt = request.Prompt,
                SystemMessage = request.SystemMessage,
                RawOutput = request.RawOutput,
                Settings = request.Settings,
                ResponseExamples = request.ResponseExamples
            };
    }
}
