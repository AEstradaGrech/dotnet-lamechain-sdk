using FluentAssertions;
using Moq;
using DotnetLlamaSharp.Domain.Services.Inference;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Validators;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Evaluators;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Exceptions;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System.Text.Json;
using System.Text.Json.Schema;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Commands
{
    public class ValidatedTestModel
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>
    /// Validator commands build the prompt sent to the LLM from the JsonValidationRequest fields:
    ///     - ValidatedPrompt = the user input that generated the RawOutput
    ///     - SystemMessage   = the instruction that generated the RawOutput
    ///     - RawOutput       = the output under validation
    /// These tests assert the content that actually reaches the LLM, so a wrong field mapping
    /// (e.g. reading request.Prompt instead of ValidatedPrompt) breaks the build instead of silently degrading the validator.
    /// </summary>
    public class ValidatorCommandTests
    {
        private const string UserInput = "List exactly 3 fruits";
        private const string Instruction = "You return fruit lists as JSON";
        private const string RawOutput = "{\"Name\":\"fruits\",\"Count\":3}";
        private const string StaleValidatorPrompt = "STALE-VALIDATOR-PROMPT";
        private static readonly string ExpectedSchema = JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(ValidatedTestModel)).ToJsonString();

        private readonly Mock<IOllamaInferenceService> _mockOllama;

        public ValidatorCommandTests()
        {
            _mockOllama = new Mock<IOllamaInferenceService>();
        }

        #region Helpers

        private static JsonValidationRequest<ValidatedTestModel> validationRequest(bool useChatEndpoint = true)
            => new JsonValidationRequest<ValidatedTestModel> {
                Prompt = StaleValidatorPrompt,
                ValidatedPrompt = UserInput,
                SystemMessage = Instruction,
                RawOutput = RawOutput,
                UseChatEndpoint = useChatEndpoint
            };

        private static JsonRefineRequest<ValidatedTestModel> refineRequest(EPromptValidation type)
            => new JsonRefineRequest<ValidatedTestModel> {
                ValidatedPrompt = UserInput,
                SystemMessage = Instruction,
                RawOutput = RawOutput,
                ValidationType = type,
                UseChatEndpoint = true
            };

        private static string userMessage(ChatRequest request) => request.Messages!.Single(m => m.Role == ChatRole.User).Content!;
        private static string systemMessage(ChatRequest request) => request.Messages!.Single(m => m.Role == ChatRole.System).Content!;

        private void setupBoolValidation(List<ChatRequest> captured, params ReasonedBoolResponse[] responses)
        {
            var queue = new Queue<ReasonedBoolResponse>(responses);

            _mockOllama
                .Setup(o => o.CommandPrompt<ReasonedBoolResponse>(
                    It.IsAny<ChatRequest>(),
                    It.IsAny<CommandPromptValidation<ReasonedBoolResponse>>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, ToolBinding>>(),
                    It.IsAny<bool>()))
                .Callback<ChatRequest, CommandPromptValidation<ReasonedBoolResponse>?, string, Dictionary<string, ToolBinding>?, bool>((req, _, _, _, _) => captured.Add(req))
                .ReturnsAsync(() => queue.Dequeue());
        }

        private void setupReview(List<ChatRequest> captured, ValidatedTestModel? reviewed)
        {
            _mockOllama
                .Setup(o => o.CommandPrompt<ValidatedTestModel>(
                    It.IsAny<ChatRequest>(),
                    It.IsAny<CommandPromptValidation<ValidatedTestModel>>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, ToolBinding>>(),
                    It.IsAny<bool>()))
                .Callback<ChatRequest, CommandPromptValidation<ValidatedTestModel>?, string, Dictionary<string, ToolBinding>?, bool>((req, _, _, _, _) => captured.Add(req))
                .ReturnsAsync(reviewed);
        }

        private void verifyReviewCalls(Times times)
            => _mockOllama.Verify(o => o.CommandPrompt<ValidatedTestModel>(
                    It.IsAny<ChatRequest>(),
                    It.IsAny<CommandPromptValidation<ValidatedTestModel>>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, ToolBinding>>(),
                    It.IsAny<bool>()), times);

        #endregion

        #region JsonOutputValidationCommand Tests

        [Fact]
        public async Task JsonOutputValidationCommand_Prompt_ChatEndpoint_UserMessageContainsValidatedPromptAsInput()
        {
            // Arrange
            var captured = new List<ChatRequest>();
            setupBoolValidation(captured, new ReasonedBoolResponse { Answer = true, Justification = "ok" });

            var command = new JsonOutputValidationCommand<ValidatedTestModel>(_mockOllama.Object, null, null);

            // Act
            await command.Prompt(validationRequest());

            // Assert
            var sentPrompt = userMessage(captured.Single());

            sentPrompt.Should().Contain($"Input: {UserInput}");
            sentPrompt.Should().NotContain(StaleValidatorPrompt);
        }

        [Fact]
        public async Task JsonOutputValidationCommand_Prompt_ChatEndpoint_UserMessageContainsInstructionOutputAndSchemaWithoutPlaceholders()
        {
            // Arrange
            var captured = new List<ChatRequest>();
            setupBoolValidation(captured, new ReasonedBoolResponse { Answer = true, Justification = "ok" });

            var command = new JsonOutputValidationCommand<ValidatedTestModel>(_mockOllama.Object, null, null);

            // Act
            await command.Prompt(validationRequest());

            // Assert
            var sentPrompt = userMessage(captured.Single());

            sentPrompt.Should().Contain(Instruction);
            sentPrompt.Should().Contain(RawOutput);
            sentPrompt.Should().Contain(ExpectedSchema);
            sentPrompt.Should().NotContain("<<");
            systemMessage(captured.Single()).Should().Contain("You are a generic output validator");
        }

        [Fact]
        public async Task JsonOutputValidationCommand_Prompt_ChatEndpoint_ReturnsServiceResponseWithoutNestedValidation()
        {
            // Arrange
            var expected = new ReasonedBoolResponse { Answer = false, Justification = "Returned 2 fruits instead of 3" };

            _mockOllama
                .Setup(o => o.CommandPrompt<ReasonedBoolResponse>(
                    It.IsAny<ChatRequest>(),
                    null,
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, ToolBinding>>(),
                    false))
                .ReturnsAsync(expected);

            var command = new JsonOutputValidationCommand<ValidatedTestModel>(_mockOllama.Object, null, null);

            // Act
            var result = await command.Prompt(validationRequest());

            // Assert
            result.Should().BeSameAs(expected);
        }

        [Fact]
        public async Task JsonOutputValidationCommand_Prompt_GenerateEndpoint_PromptContainsValidatedPromptAsInput()
        {
            // Arrange
            GenerateRequest? captured = null;

            _mockOllama
                .Setup(o => o.CommandPrompt<ReasonedBoolResponse>(
                    It.IsAny<GenerateRequest>(),
                    It.IsAny<CommandPromptValidation<ReasonedBoolResponse>>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>()))
                .Callback<GenerateRequest, CommandPromptValidation<ReasonedBoolResponse>?, string, bool>((req, _, _, _) => captured = req)
                .ReturnsAsync(new ReasonedBoolResponse { Answer = true, Justification = "ok" });

            var command = new JsonOutputValidationCommand<ValidatedTestModel>(_mockOllama.Object, null, null);

            // Act
            await command.Prompt(validationRequest(useChatEndpoint: false));

            // Assert
            captured.Should().NotBeNull();
            captured!.Prompt.Should().Contain($"Input: {UserInput}").And.Contain(RawOutput).And.Contain(Instruction);
            captured.Prompt.Should().NotContain(StaleValidatorPrompt);
            captured.System.Should().Contain("You are a generic output validator");
        }

        [Fact]
        public async Task JsonOutputValidationCommand_PromptSync_ChatEndpoint_UserMessageContainsValidatedPromptAsInput()
        {
            // Arrange
            var captured = new List<ChatRequest>();
            setupBoolValidation(captured, new ReasonedBoolResponse { Answer = true, Justification = "ok" });

            var command = new JsonOutputValidationCommand<ValidatedTestModel>(_mockOllama.Object, null, null);

            // Act
            await command.PromptSync(validationRequest());

            // Assert
            var sentPrompt = userMessage(captured.Single());

            sentPrompt.Should().Contain($"Input: {UserInput}");
            sentPrompt.Should().NotContain(StaleValidatorPrompt);
        }

        [Fact]
        public async Task JsonOutputValidationCommand_Prompt_WrongRequestType_ThrowsInvalidOperationException()
        {
            // Arrange
            var command = new JsonOutputValidationCommand<ValidatedTestModel>(_mockOllama.Object, null, null);

            // Act
            var act = async () => await command.Prompt(new PromptCommandRequest(UserInput));

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task JsonOutputValidationCommand_Prompt_ServiceThrows_PropagatesException()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.CommandPrompt<ReasonedBoolResponse>(
                    It.IsAny<ChatRequest>(),
                    It.IsAny<CommandPromptValidation<ReasonedBoolResponse>>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, ToolBinding>>(),
                    It.IsAny<bool>()))
                .ThrowsAsync(new InvalidOperationException("LLM service failure"));

            var command = new JsonOutputValidationCommand<ValidatedTestModel>(_mockOllama.Object, null, null);

            // Act
            var act = async () => await command.Prompt(validationRequest());

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("LLM service failure");
        }

        #endregion

        #region JsonOutputReviewCommand Tests

        [Fact]
        public async Task JsonOutputReviewCommand_Prompt_ChatEndpoint_UserMessageContainsInputOutputInstructionAndSchema()
        {
            // Arrange
            var captured = new List<ChatRequest>();
            setupReview(captured, new ValidatedTestModel { Name = "fruits", Count = 3 });

            var command = new JsonOutputReviewCommand<ValidatedTestModel>(_mockOllama.Object, null, null);

            // Act
            await command.Prompt(validationRequest());

            // Assert
            var sentPrompt = userMessage(captured.Single());

            sentPrompt.Should().Contain($"- input: {UserInput}");
            sentPrompt.Should().Contain($"- output: {RawOutput}");
            sentPrompt.Should().Contain($"- instruction: {Instruction}");
            sentPrompt.Should().Contain(ExpectedSchema);
            sentPrompt.Should().NotContain(StaleValidatorPrompt);
        }

        [Fact]
        public async Task JsonOutputReviewCommand_Prompt_ChatEndpoint_SystemMessageIsReviewerInstructionWithoutReviewedContent()
        {
            // Arrange
            var captured = new List<ChatRequest>();
            setupReview(captured, new ValidatedTestModel { Name = "fruits", Count = 3 });

            var command = new JsonOutputReviewCommand<ValidatedTestModel>(_mockOllama.Object, null, null);

            // Act
            await command.Prompt(validationRequest());

            // Assert
            var sentSystem = systemMessage(captured.Single());

            sentSystem.Should().Contain("You are a JSON output reviewer");
            sentSystem.Should().NotContain(UserInput).And.NotContain(RawOutput).And.NotContain("<<");
        }

        [Fact]
        public async Task JsonOutputReviewCommand_Prompt_WithGuidanceMessage_SystemMessageContainsGuidance()
        {
            // Arrange
            const string guidance = "A previous validator marked the output as INVALID";
            var captured = new List<ChatRequest>();
            setupReview(captured, new ValidatedTestModel { Name = "fruits", Count = 3 });

            var command = new JsonOutputReviewCommand<ValidatedTestModel>(_mockOllama.Object, null, null);
            var request = validationRequest();
            request.GuidanceMessage = guidance;

            // Act
            await command.Prompt(request);

            // Assert
            systemMessage(captured.Single()).Should().Contain(guidance);
            userMessage(captured.Single()).Should().NotContain(guidance);
        }

        [Fact]
        public async Task JsonOutputReviewCommand_PromptSync_ChatEndpoint_UserMessageContainsValidatedPromptAsInput()
        {
            // Arrange
            var captured = new List<ChatRequest>();
            setupReview(captured, new ValidatedTestModel { Name = "fruits", Count = 3 });

            var command = new JsonOutputReviewCommand<ValidatedTestModel>(_mockOllama.Object, null, null);

            // Act
            await command.PromptSync(validationRequest());

            // Assert
            var sentPrompt = userMessage(captured.Single());

            sentPrompt.Should().Contain($"- input: {UserInput}").And.Contain($"- output: {RawOutput}");
            sentPrompt.Should().NotContain(StaleValidatorPrompt);
        }

        [Fact]
        public async Task JsonOutputReviewCommand_Prompt_ChatEndpoint_ReturnsReviewedModelRequestingJsonInfo()
        {
            // Arrange
            var reviewed = new ValidatedTestModel { Name = "fruits", Count = 3 };

            _mockOllama
                .Setup(o => o.CommandPrompt<ValidatedTestModel>(
                    It.IsAny<ChatRequest>(),
                    null,
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, ToolBinding>>(),
                    true))
                .ReturnsAsync(reviewed);

            var command = new JsonOutputReviewCommand<ValidatedTestModel>(_mockOllama.Object, null, null);

            // Act
            var result = await command.Prompt(validationRequest());

            // Assert
            result.Should().BeSameAs(reviewed);
        }

        [Fact]
        public async Task JsonOutputReviewCommand_Prompt_GenerateEndpoint_PromptContainsValidatedPromptAsInput()
        {
            // Arrange
            GenerateRequest? captured = null;

            _mockOllama
                .Setup(o => o.CommandPrompt<ValidatedTestModel>(
                    It.IsAny<GenerateRequest>(),
                    It.IsAny<CommandPromptValidation<ValidatedTestModel>>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>()))
                .Callback<GenerateRequest, CommandPromptValidation<ValidatedTestModel>?, string, bool>((req, _, _, _) => captured = req)
                .ReturnsAsync(new ValidatedTestModel());

            var command = new JsonOutputReviewCommand<ValidatedTestModel>(_mockOllama.Object, null, null);

            // Act
            await command.Prompt(validationRequest(useChatEndpoint: false));

            // Assert
            captured.Should().NotBeNull();
            captured!.Prompt.Should().Contain($"- input: {UserInput}").And.Contain($"- output: {RawOutput}").And.Contain(ExpectedSchema);
            captured.System.Should().Contain("You are a JSON output reviewer").And.NotContain(RawOutput);
        }

        [Fact]
        public async Task JsonOutputReviewCommand_Prompt_WrongRequestType_ThrowsInvalidOperationException()
        {
            // Arrange
            var command = new JsonOutputReviewCommand<ValidatedTestModel>(_mockOllama.Object, null, null);

            // Act
            var act = async () => await command.Prompt(new PromptCommandRequest(UserInput));

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region JsonOutputRefinerCommand Tests

        [Fact]
        public async Task JsonOutputRefinerCommand_Prompt_BoolValidation_ValidatorReceivesOriginalUserInput()
        {
            // Arrange
            var captured = new List<ChatRequest>();
            setupBoolValidation(captured, new ReasonedBoolResponse { Answer = true, Justification = "ok" });

            var command = new JsonOutputRefinerCommand<ValidatedTestModel>(_mockOllama.Object);

            // Act
            await command.Prompt(refineRequest(EPromptValidation.BOOL_AND_RETRY));

            // Assert
            var sentPrompt = userMessage(captured.Single());

            sentPrompt.Should().Contain($"Input: {UserInput}");
            sentPrompt.Should().Contain(Instruction).And.Contain(RawOutput);
        }

        [Fact]
        public async Task JsonOutputRefinerCommand_Prompt_BoolAndRetryValid_ReturnsDeserializedRawOutput()
        {
            // Arrange
            setupBoolValidation(new List<ChatRequest>(), new ReasonedBoolResponse { Answer = true, Justification = "ok" });

            var command = new JsonOutputRefinerCommand<ValidatedTestModel>(_mockOllama.Object);

            // Act
            var result = await command.Prompt(refineRequest(EPromptValidation.BOOL_AND_RETRY));

            // Assert
            result.Should().BeEquivalentTo(new ValidatedTestModel { Name = "fruits", Count = 3 });
            verifyReviewCalls(Times.Never());
        }

        [Fact]
        public async Task JsonOutputRefinerCommand_Prompt_BoolAndRetryInvalid_ThrowsJsonOutputValidationExceptionWithJustification()
        {
            // Arrange
            setupBoolValidation(new List<ChatRequest>(), new ReasonedBoolResponse { Answer = false, Justification = "Returned 2 fruits instead of 3" });

            var command = new JsonOutputRefinerCommand<ValidatedTestModel>(_mockOllama.Object);

            // Act
            var act = async () => await command.Prompt(refineRequest(EPromptValidation.BOOL_AND_RETRY));

            // Assert
            await act.Should().ThrowAsync<JsonOutputValidationException>().WithMessage("*Returned 2 fruits instead of 3*");
        }

        [Fact]
        public async Task JsonOutputRefinerCommand_Prompt_ReviewOnly_ReturnsReviewedModelWithoutBoolValidation()
        {
            // Arrange
            var reviewed = new ValidatedTestModel { Name = "reviewed", Count = 3 };
            var captured = new List<ChatRequest>();
            setupReview(captured, reviewed);

            var command = new JsonOutputRefinerCommand<ValidatedTestModel>(_mockOllama.Object);

            // Act
            var result = await command.Prompt(refineRequest(EPromptValidation.REVIEW_ONLY));

            // Assert
            result.Should().BeSameAs(reviewed);
            userMessage(captured.Single()).Should().Contain($"- input: {UserInput}");
            _mockOllama.Verify(o => o.CommandPrompt<ReasonedBoolResponse>(
                It.IsAny<ChatRequest>(),
                It.IsAny<CommandPromptValidation<ReasonedBoolResponse>>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, ToolBinding>>(),
                It.IsAny<bool>()), Times.Never());
        }

        [Fact]
        public async Task JsonOutputRefinerCommand_Prompt_BoolAndReviewValid_ReturnsRawOutputWithoutReview()
        {
            // Arrange
            setupBoolValidation(new List<ChatRequest>(), new ReasonedBoolResponse { Answer = true, Justification = "ok" });

            var command = new JsonOutputRefinerCommand<ValidatedTestModel>(_mockOllama.Object);

            // Act
            var result = await command.Prompt(refineRequest(EPromptValidation.BOOL_AND_REVIEW));

            // Assert
            result.Should().BeEquivalentTo(new ValidatedTestModel { Name = "fruits", Count = 3 });
            verifyReviewCalls(Times.Never());
        }

        [Fact]
        public async Task JsonOutputRefinerCommand_Prompt_BoolAndReviewInvalid_ReviewerReceivesJustificationAndReturnsReviewedModel()
        {
            // Arrange
            const string justification = "Returned 2 fruits instead of 3";
            var reviewed = new ValidatedTestModel { Name = "reviewed", Count = 3 };
            var reviewCaptured = new List<ChatRequest>();

            setupBoolValidation(new List<ChatRequest>(), new ReasonedBoolResponse { Answer = false, Justification = justification });
            setupReview(reviewCaptured, reviewed);

            var command = new JsonOutputRefinerCommand<ValidatedTestModel>(_mockOllama.Object);

            // Act
            var result = await command.Prompt(refineRequest(EPromptValidation.BOOL_AND_REVIEW));

            // Assert
            result.Should().BeSameAs(reviewed);
            systemMessage(reviewCaptured.Single()).Should().Contain(justification);
            userMessage(reviewCaptured.Single()).Should().Contain($"- input: {UserInput}");
        }

        [Theory]
        [InlineData(EPromptValidation.REVIEW_ONLY)]
        [InlineData(EPromptValidation.BOOL_AND_REVIEW)]
        [InlineData(EPromptValidation.DOUBLE_BOOL)]
        public async Task JsonOutputRefinerCommand_Prompt_ReviewReturnsNull_ThrowsJsonOutputValidationException(EPromptValidation validationType)
        {
            // Arrange
            setupBoolValidation(new List<ChatRequest>(), new ReasonedBoolResponse { Answer = false, Justification = "Returned 2 fruits instead of 3" });
            setupReview(new List<ChatRequest>(), null);

            var command = new JsonOutputRefinerCommand<ValidatedTestModel>(_mockOllama.Object);

            // Act
            var act = async () => await command.Prompt(refineRequest(validationType));

            // Assert
            await act.Should().ThrowAsync<JsonOutputValidationException>().WithMessage("*reviewed response is null*");
        }

        [Fact]
        public async Task JsonOutputRefinerCommand_Prompt_DoubleBoolReviewReturnsNull_DoesNotRunSecondValidation()
        {
            // Arrange
            var boolCaptured = new List<ChatRequest>();
            setupBoolValidation(boolCaptured, new ReasonedBoolResponse { Answer = false, Justification = "Returned 2 fruits instead of 3" });
            setupReview(new List<ChatRequest>(), null);

            var command = new JsonOutputRefinerCommand<ValidatedTestModel>(_mockOllama.Object);

            // Act
            var act = async () => await command.Prompt(refineRequest(EPromptValidation.DOUBLE_BOOL));

            // Assert
            await act.Should().ThrowAsync<JsonOutputValidationException>();
            boolCaptured.Should().ContainSingle();
        }

        [Fact]
        public async Task JsonOutputRefinerCommand_Prompt_DoubleBoolReviewedOutputValid_ReturnsReviewedModelValidatedTwice()
        {
            // Arrange
            var reviewed = new ValidatedTestModel { Name = "reviewed", Count = 3 };
            var boolCaptured = new List<ChatRequest>();

            setupBoolValidation(boolCaptured,
                new ReasonedBoolResponse { Answer = false, Justification = "Returned 2 fruits instead of 3" },
                new ReasonedBoolResponse { Answer = true, Justification = "ok" });
            setupReview(new List<ChatRequest>(), reviewed);

            var command = new JsonOutputRefinerCommand<ValidatedTestModel>(_mockOllama.Object);

            // Act
            var result = await command.Prompt(refineRequest(EPromptValidation.DOUBLE_BOOL));

            // Assert
            result.Should().BeSameAs(reviewed);
            boolCaptured.Should().HaveCount(2);
            userMessage(boolCaptured[1]).Should().Contain(JsonSerializer.Serialize(reviewed)).And.Contain($"Input: {UserInput}");
        }

        [Fact]
        public async Task JsonOutputRefinerCommand_Prompt_DoubleBoolReviewedOutputInvalid_ThrowsJsonOutputValidationException()
        {
            // Arrange
            setupBoolValidation(new List<ChatRequest>(),
                new ReasonedBoolResponse { Answer = false, Justification = "Returned 2 fruits instead of 3" },
                new ReasonedBoolResponse { Answer = false, Justification = "Still wrong" });
            setupReview(new List<ChatRequest>(), new ValidatedTestModel { Name = "reviewed", Count = 2 });

            var command = new JsonOutputRefinerCommand<ValidatedTestModel>(_mockOllama.Object);

            // Act
            var act = async () => await command.Prompt(refineRequest(EPromptValidation.DOUBLE_BOOL));

            // Assert
            await act.Should().ThrowAsync<JsonOutputValidationException>().WithMessage("*Still wrong*");
        }

        [Fact]
        public async Task JsonOutputRefinerCommand_Prompt_SettingsWithValidatorModel_SubValidatorUsesValidatorModel()
        {
            // Arrange
            var captured = new List<ChatRequest>();
            setupBoolValidation(captured, new ReasonedBoolResponse { Answer = true, Justification = "ok" });

            var settings = new CommandSettings("main-model") { ValidatorModel = "validator-model" };
            var command = new JsonOutputRefinerCommand<ValidatedTestModel>(_mockOllama.Object, null, settings);

            // Act
            await command.Prompt(refineRequest(EPromptValidation.BOOL_AND_RETRY));

            // Assert
            captured.Single().Model.Should().Be("validator-model");
        }

        [Fact]
        public async Task JsonOutputRefinerCommand_Prompt_WrongRequestType_ThrowsInvalidOperationException()
        {
            // Arrange
            var command = new JsonOutputRefinerCommand<ValidatedTestModel>(_mockOllama.Object);

            // Act
            var act = async () => await command.Prompt(validationRequest());

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        #endregion
    }
}
