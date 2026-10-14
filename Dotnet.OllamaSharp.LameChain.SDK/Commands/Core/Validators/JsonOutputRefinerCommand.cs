using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Evaluators;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Exceptions;
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

        //The refiner itself makes no use of the passed messageName, but the messaageSourceName is used for the subValidators (for now. There might be a refiner constructor accepting source-name KvP's in future releases)
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
                        throw new JsonOutputValidationException($"{nameof(JsonOutputRefinerCommand<TRefined>)} >> {nameof(Prompt)}",boolValidation.Justification);

                    else return JsonSerializer.Deserialize<TRefined>(validationReq.RawOutput);
                
                case (EPromptValidation.BOOL_AND_REVIEW):
                    return await validateAndReview(_ollama, validationReq);

                case (EPromptValidation.DOUBLE_BOOL):
                    return await doubleBool(_ollama, validationReq);

                default: return null;
            }
        }

        //public override Task<TRefined> PromptSync(PromptCommandRequest request)
        //{
        //    validateInputRequest<JsonRefineRequest<TRefined>>(request);

        //    if (string.IsNullOrEmpty(request.Model) && _settings != null)
        //        request.Model = string.IsNullOrEmpty(_settings.ValidatorModel) ? _settings.Model : _settings.ValidatorModel;

        //    var validationReq = (JsonRefineRequest<TRefined>)request;

        //    ReasonedBoolResponse boolValidation = null;
        //    switch (validationReq.ValidationType)
        //    {
        //        case (EPromptValidation.REVIEW_ONLY):
        //            return reviewResponse(_ollama, validationReq);

        //        case (EPromptValidation.BOOL_AND_RETRY):
        //            boolValidation = validateResponse(_ollama, validationReq).Result;

        //            if (!boolValidation.Answer)
        //                throw new JsonOutputValidationException($"{nameof(JsonOutputRefinerCommand<TRefined>)} >> {nameof(PromptSync)}", boolValidation.Justification);

        //            else return Task.FromResult(JsonSerializer.Deserialize<TRefined>(validationReq.RawOutput));

        //        case (EPromptValidation.BOOL_AND_REVIEW):
        //            return validateAndReview(_ollama, validationReq);

        //        case (EPromptValidation.DOUBLE_BOOL):
        //            return doubleBool(_ollama, validationReq);

        //        default: return null;
        //    }
        //}

        private async Task<TRefined> reviewResponse(IOllamaInferenceService ollama, JsonRefineRequest<TRefined> request, string? guidanceMessage = null)
        {
            var command = _retrieverLambda == null ?
                new JsonOutputReviewCommand<TRefined>(ollama, null, _settings):
                new JsonOutputReviewCommand<TRefined>(ollama, _dbSourceName, "json-review", _retrieverLambda, null, _settings);

            var reviewRequest = toValidationRequest<TRefined>(request);

            reviewRequest.GuidanceMessage = guidanceMessage;
            reviewRequest.ValidatedPrompt = request.ValidatedPrompt;
            
            return await command.Prompt(reviewRequest);
        }

        private async Task<ReasonedBoolResponse> validateResponse(IOllamaInferenceService ollama, JsonRefineRequest<TRefined> request, string? guidanceMessage = null)
        {
            var command = _retrieverLambda == null ?
                new JsonOutputValidationCommand<TRefined>(ollama, null, _settings) :
                new JsonOutputValidationCommand<TRefined>(ollama, _dbSourceName, "json-validate", _retrieverLambda, null, _settings);

            var validationRequest = toValidationRequest<TRefined>(request);

            validationRequest.GuidanceMessage = guidanceMessage;
            validationRequest.ValidatedPrompt = request.ValidatedPrompt;

            return await command.Prompt(validationRequest);
        }

        private async Task<TRefined> validateAndReview(IOllamaInferenceService ollama, JsonRefineRequest<TRefined> request, string? guidanceMessage = null)
        {
            var validation = await validateResponse(ollama, request);

            if (validation.Answer) 
                return JsonSerializer.Deserialize<TRefined>(request.RawOutput);

            return await reviewResponse(ollama, request, getJustificationGuidanceMessage(validation.Justification));
        }
        private async Task<TRefined> doubleBool(IOllamaInferenceService ollama, JsonRefineRequest<TRefined> request, string? guidanceMessage = null)
        {
            var review = await validateAndReview(ollama, request, guidanceMessage);

            request.RawOutput = JsonSerializer.Serialize<TRefined>(review);

            var validation = await validateResponse(ollama, request);

            if(!validation.Answer)
                throw new JsonOutputValidationException($"{nameof(JsonOutputRefinerCommand<TRefined>)} >> {nameof(doubleBool)}", validation.Justification);

            return review;
        }

        JsonValidationRequest<TValResult> toValidationRequest<TValResult>(JsonRefineRequest<TRefined> request)
            => new JsonValidationRequest<TValResult> {
                Model = !string.IsNullOrEmpty(request.Provider) && !string.IsNullOrEmpty(request.Model) ? $"{request.Provider}_{request.Model}" : _settings != null && !string.IsNullOrEmpty(_settings.ValidatorModel) ? _settings.ValidatorModel : string.Empty,
                Prompt = request.Prompt,
                SystemMessage = request.SystemMessage,
                RawOutput = request.RawOutput,
                ResponseExamples = request.ResponseExamples,
                UseChatEndpoint = request.UseChatEndpoint
            };

        private string getJustificationGuidanceMessage(string justification)
            => $"# WARNING: a previous reviewer has marked the response as INVALID. Take into account the reason to have a better understanding of the problem. INVALID REASON: {justification}";
    }
}
