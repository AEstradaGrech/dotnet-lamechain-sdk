using FluentAssertions;
using Moq;
using DotnetLlamaSharp.Domain.Services.Inference;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.AtomicValues;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.AtomicValues;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Evaluators;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Evaluators;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Validators;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System.Reflection;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Commands
{
    public enum TestIntEnum { Default = 0, OptionA = 1, OptionB = 2 }
    public enum TestLongEnum : long { A = 1L, B = 2L }

    public class CommandTests
    {
        private readonly Mock<IOllamaInferenceService> _mockOllama;
        private readonly CommandSettings _settings;
        // Retriever used by commands that require the DB constructor to expose CommandSettings
        private readonly Func<string, string, Task<string>> _retriever;

        public CommandTests()
        {
            _mockOllama = new Mock<IOllamaInferenceService>();
            _settings = new CommandSettings("test-model");
            _retriever = (_, _) => Task.FromResult("Respond according to the provided JSON schema.");
        }

        #region BoolPromptCommand Tests

        [Fact]
        public async Task BoolPromptCommand_Prompt_HappyPath_ReturnsBoolFromResponse()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.CommandPrompt<BooleanResponse>(
                    It.IsAny<ChatRequest>(),
                    It.Is<CommandPromptValidation<BooleanResponse>>(v => v.Validations == _settings.CommandValidations && v.ValidationType == _settings.ValidationType),
                    "ollama",
                    It.IsAny<Dictionary<string, MethodInfo>>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(new BooleanResponse { Answer = true });

            var command = new BoolPromptCommand(_mockOllama.Object, "source", "message", _retriever, null, _settings);
            var request = new PromptCommandRequest("Is the sky blue?");

            // Act
            var result = await command.Prompt(request);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task BoolPromptCommand_Prompt_ServiceThrows_PropagatesException()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.CommandPrompt<BooleanResponse>(
                    It.IsAny<ChatRequest>(),
                    It.Is<CommandPromptValidation<BooleanResponse>>(v => v.Validations == _settings.CommandValidations && v.ValidationType == _settings.ValidationType),
                    "ollama",
                    It.IsAny<Dictionary<string, MethodInfo>>(),
                    It.IsAny<bool>()))
                .ThrowsAsync(new InvalidOperationException("LLM service failure"));

            var command = new BoolPromptCommand(_mockOllama.Object, "source", "message", _retriever, null, _settings);
            var request = new PromptCommandRequest("Is the sky blue?");

            // Act
            var act = async () => await command.Prompt(request);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("LLM service failure");
        }

        #endregion

        #region NumericPromptCommand Tests

        [Fact]
        public async Task NumericPromptCommand_Prompt_HappyPath_ReturnsFloatFromResponse()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.CommandPrompt<NumericResponse>(
                    It.IsAny<ChatRequest>(),
                    It.Is<CommandPromptValidation<NumericResponse>>(v => v.Validations == _settings.CommandValidations && v.ValidationType == _settings.ValidationType),
                    "ollama",
                    It.IsAny<Dictionary<string, MethodInfo>>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(new NumericResponse { Result = 42.5f });

            var command = new NumericPromptCommand(_mockOllama.Object, "source", "message", _retriever, null, _settings);
            var request = new PromptCommandRequest("What is 40 + 2.5?");

            // Act
            var result = await command.Prompt(request);

            // Assert
            result.Should().Be(42.5f);
        }

        [Fact]
        public async Task NumericPromptCommand_Prompt_ServiceThrows_PropagatesException()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.CommandPrompt<NumericResponse>(
                    It.IsAny<ChatRequest>(),
                    It.Is<CommandPromptValidation<NumericResponse>>(v => v.Validations == _settings.CommandValidations && v.ValidationType == _settings.ValidationType),
                    "ollama",
                    It.IsAny<Dictionary<string, MethodInfo>>(),
                    It.IsAny<bool>()))
                .ThrowsAsync(new InvalidOperationException("LLM service failure"));

            var command = new NumericPromptCommand(_mockOllama.Object, "source", "message", _retriever, null, _settings);
            var request = new PromptCommandRequest("What is 40 + 2.5?");

            // Act
            var act = async () => await command.Prompt(request);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region StringChoiceCommand Tests

        [Fact]
        public async Task StringChoiceCommand_Prompt_HappyPath_ReturnsSelectedChoice()
        {
            // Arrange
            var choices = new List<string> { "Apple", "Banana", "Cherry" };

            _mockOllama
                .Setup(o => o.CommandPrompt<StringChoiceResponse>(
                    It.IsAny<ChatRequest>(),
                    It.Is<CommandPromptValidation<StringChoiceResponse>>(v => v.Validations == _settings.CommandValidations && v.ValidationType == _settings.ValidationType),
                    "ollama",
                    It.IsAny<Dictionary<string, MethodInfo>>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(new StringChoiceResponse { Selected = "Banana" });

            var command = new StringChoiceCommand(_mockOllama.Object, "source", "message", _retriever, null, _settings);
            var request = new StringChoiceRequest(choices, "Which fruit is yellow?", (string?)null);

            // Act
            var result = await command.Prompt(request);

            // Assert
            result.Should().Be("Banana");
        }

        [Fact]
        public async Task StringChoiceCommand_Prompt_WrongRequestType_ThrowsInvalidOperationException()
        {
            // Arrange
            var command = new StringChoiceCommand(_mockOllama.Object, "source", "message", _retriever, null, _settings);
            var request = new PromptCommandRequest("Which fruit is yellow?");

            // Act
            var act = async () => await command.Prompt(request);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region MultiChoiceCommand Tests

        [Fact]
        public async Task MultiChoiceCommand_Prompt_HappyPath_ReturnsSelectedChoices()
        {
            // Arrange
            var choices = new List<string> { "Pizza", "Pasta", "Salad", "Soup" };

            _mockOllama
                .Setup(o => o.CommandPrompt<MultiChoiceResponse>(
                    It.IsAny<ChatRequest>(),
                    It.Is<CommandPromptValidation<MultiChoiceResponse>>(v => v.Validations == _settings.CommandValidations && v.ValidationType == _settings.ValidationType),
                    "ollama",
                    It.IsAny<Dictionary<string, MethodInfo>>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(new MultiChoiceResponse { Selected = ["Pizza", "Pasta"] });

            var command = new MultiChoiceCommand(_mockOllama.Object, null, _settings);
            var request = new MultiChoiceRequest(2, choices, "Pick the Italian dishes");

            // Act
            var result = await command.Prompt(request);

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain("Pizza").And.Contain("Pasta");
        }

        [Fact]
        public async Task MultiChoiceCommand_Prompt_WrongRequestType_ThrowsInvalidOperationException()
        {
            // Arrange
            var command = new MultiChoiceCommand(_mockOllama.Object, null, _settings);
            var request = new PromptCommandRequest("Pick the Italian dishes");

            // Act
            var act = async () => await command.Prompt(request);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region EnumPromptCommand Tests

        [Fact]
        public async Task EnumPromptCommand_Prompt_HappyPath_ReturnsCorrectEnumValue()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.CommandPrompt<IntegerChoiceResponse>(
                    It.IsAny<ChatRequest>(),
                    It.Is<CommandPromptValidation<IntegerChoiceResponse>>(v => v.Validations == _settings.CommandValidations && v.ValidationType == _settings.ValidationType),
                    "ollama",
                    It.IsAny<Dictionary<string, MethodInfo>>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(new IntegerChoiceResponse { Result = 1 });

            var command = new EnumPromptCommand<TestIntEnum>(_mockOllama.Object, "source", "message", _retriever, null, _settings);
            var request = new PromptCommandRequest("Select option A");

            // Act
            var result = await command.Prompt(request);

            // Assert
            result.Should().Be(TestIntEnum.OptionA);
        }

        [Fact]
        public async Task EnumPromptCommand_Prompt_NonIntUnderlyingType_ThrowsInvalidOperationException()
        {
            // Arrange — TestLongEnum has underlying type long, which is rejected
            var command = new EnumPromptCommand<TestLongEnum>(_mockOllama.Object, "source", "message", _retriever, null, _settings);
            var request = new PromptCommandRequest("Select an option");

            // Act
            var act = async () => await command.Prompt(request);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*invalid ENUM type*");
        }

        #endregion

        #region ReasonedBoolCommand Tests

        [Fact]
        public async Task ReasonedBoolCommand_Prompt_HappyPath_ReturnsAnswerWithJustification()
        {
            // Arrange
            var expected = new ReasonedBoolResponse { Answer = true, Justification = "Sky is blue due to Rayleigh scattering." };

            _mockOllama
                .Setup(o => o.CommandPrompt<ReasonedBoolResponse>(
                    It.IsAny<ChatRequest>(),
                    It.Is<CommandPromptValidation<ReasonedBoolResponse>>(v => v.Validations == _settings.CommandValidations && v.ValidationType == _settings.ValidationType),
                    "ollama",
                    It.IsAny<Dictionary<string, MethodInfo>>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(expected);

            var command = new ReasonedBoolCommand(_mockOllama.Object, null, _settings);
            var request = new PromptCommandRequest("Is the sky blue?");

            // Act
            var result = await command.Prompt(request);

            // Assert
            result.Answer.Should().BeTrue();
            result.Justification.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task ReasonedBoolCommand_Prompt_ServiceThrows_PropagatesException()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.CommandPrompt<ReasonedBoolResponse>(
                    It.IsAny<ChatRequest>(),
                    It.Is<CommandPromptValidation<ReasonedBoolResponse>>(v => v.Validations == _settings.CommandValidations && v.ValidationType == _settings.ValidationType),
                    "ollama",
                    It.IsAny<Dictionary<string, MethodInfo>>(),
                    It.IsAny<bool>()))
                .ThrowsAsync(new InvalidOperationException("LLM service failure"));

            var command = new ReasonedBoolCommand(_mockOllama.Object, null, _settings);
            var request = new PromptCommandRequest("Is the sky blue?");

            // Act
            var act = async () => await command.Prompt(request);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region ScoredBoolCommand Tests

        [Fact]
        public async Task ScoredBoolCommand_Prompt_HappyPath_ReturnsAnswerWithConfidenceScore()
        {
            // Arrange
            var expected = new ScoredBoolResponse { Answer = true, Score = 0.95f, Justification = "High confidence." };

            _mockOllama
                .Setup(o => o.CommandPrompt<ScoredBoolResponse>(
                    It.IsAny<ChatRequest>(),
                    It.Is<CommandPromptValidation<ScoredBoolResponse>>(v => v.Validations == _settings.CommandValidations && v.ValidationType == _settings.ValidationType),
                    "ollama",
                    It.IsAny<Dictionary<string, MethodInfo>>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(expected);

            var command = new ScoredBoolCommand(_mockOllama.Object, null, _settings);
            var request = new PromptCommandRequest("Is C# a strongly typed language?");

            // Act
            var result = await command.Prompt(request);

            // Assert
            result.Answer.Should().BeTrue();
            result.Score.Should().BeApproximately(0.95f, 0.001f);
            result.Justification.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task ScoredBoolCommand_Prompt_ServiceThrows_PropagatesException()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.CommandPrompt<ScoredBoolResponse>(
                    It.IsAny<ChatRequest>(),
                    It.Is<CommandPromptValidation<ScoredBoolResponse>>(v => v.Validations == _settings.CommandValidations && v.ValidationType == _settings.ValidationType),
                    "ollama",
                    It.IsAny<Dictionary<string, MethodInfo>>(),
                    It.IsAny<bool>()))
                .ThrowsAsync(new InvalidOperationException("LLM service failure"));

            var command = new ScoredBoolCommand(_mockOllama.Object, null, _settings);
            var request = new PromptCommandRequest("Is C# a strongly typed language?");

            // Act
            var act = async () => await command.Prompt(request);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region ReasonedScoreCommand Tests

        [Fact]
        public async Task ReasonedScoreCommand_Prompt_HappyPath_ReturnsScoreWithJustification()
        {
            // Arrange
            var expected = new ReasonedScoreResponse { Score = 0.8f, Justification = "Strong positive signal." };

            _mockOllama
                .Setup(o => o.CommandPrompt<ReasonedScoreResponse>(
                    It.IsAny<ChatRequest>(),
                    It.Is<CommandPromptValidation<ReasonedScoreResponse>>(v => v.Validations == _settings.CommandValidations && v.ValidationType == _settings.ValidationType),
                    "ollama",
                    It.IsAny<Dictionary<string, MethodInfo>>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(expected);

            var command = new ReasonedScoreCommand(_mockOllama.Object, null, _settings);
            var request = new PromptCommandRequest("Rate the quality of this text.");

            // Act
            var result = await command.Prompt(request);

            // Assert
            result.Score.Should().BeApproximately(0.8f, 0.001f);
            result.Justification.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task ReasonedScoreCommand_Prompt_ServiceThrows_PropagatesException()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.CommandPrompt<ReasonedScoreResponse>(
                    It.IsAny<ChatRequest>(),
                    It.Is<CommandPromptValidation<ReasonedScoreResponse>>(v => v.Validations == _settings.CommandValidations && v.ValidationType == _settings.ValidationType),
                    "ollama",
                    It.IsAny<Dictionary<string, MethodInfo>>(),
                    It.IsAny<bool>()))
                .ThrowsAsync(new InvalidOperationException("LLM service failure"));

            var command = new ReasonedScoreCommand(_mockOllama.Object, null, _settings);
            var request = new PromptCommandRequest("Rate the quality of this text.");

            // Act
            var act = async () => await command.Prompt(request);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region ScoredResponseCommand Tests

        [Fact]
        public async Task ScoredResponseCommand_Prompt_HappyPath_ReturnsConfidenceScore()
        {
            // Arrange
            var expected = new ScoredResponse { Score = 0.75f };

            _mockOllama
                .Setup(o => o.CommandPrompt<ScoredResponse>(
                    It.IsAny<ChatRequest>(),
                    It.Is<CommandPromptValidation<ScoredResponse>>(v => v.Validations == _settings.CommandValidations && v.ValidationType == _settings.ValidationType),
                    "ollama",
                    It.IsAny<Dictionary<string, MethodInfo>>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(expected);

            var command = new ScoredResponseCommand(_mockOllama.Object, null, _settings);
            var request = new PromptCommandRequest("Score the relevance of this response.");

            // Act
            var result = await command.Prompt(request);

            // Assert
            result.Score.Should().BeApproximately(0.75f, 0.001f);
        }

        [Fact]
        public async Task ScoredResponseCommand_Prompt_ServiceThrows_PropagatesException()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.CommandPrompt<ScoredResponse>(
                    It.IsAny<ChatRequest>(),
                    It.Is<CommandPromptValidation<ScoredResponse>>(v => v.Validations == _settings.CommandValidations && v.ValidationType == _settings.ValidationType),
                    "ollama",
                    It.IsAny<Dictionary<string, MethodInfo>>(),
                    It.IsAny<bool>()))
                .ThrowsAsync(new InvalidOperationException("LLM service failure"));

            var command = new ScoredResponseCommand(_mockOllama.Object, null, _settings);
            var request = new PromptCommandRequest("Score the relevance of this response.");

            // Act
            var act = async () => await command.Prompt(request);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region ScoredChoiceCommand Tests

        [Fact]
        public async Task ScoredChoiceCommand_Prompt_HappyPath_ReturnsChoiceWithScore()
        {
            // Arrange
            var choices = new List<string> { "Option A", "Option B", "Option C" };
            var expected = new ScoredStringChoice { Selected = "Option B", Score = 0.9f, Justification = "Best match for the intent." };

            _mockOllama
                .Setup(o => o.CommandPrompt<ScoredStringChoice>(
                    It.IsAny<ChatRequest>(),
                    It.Is<CommandPromptValidation<ScoredStringChoice>>(v => v.Validations == _settings.CommandValidations && v.ValidationType == _settings.ValidationType),
                    "ollama",
                    It.IsAny<Dictionary<string, MethodInfo>>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(expected);

            var command = new ScoredChoiceCommand(_mockOllama.Object, null, _settings);
            var request = new StringChoiceRequest(choices, "Which option is best for a fast response?", (string?)null);

            // Act
            var result = await command.Prompt(request);

            // Assert
            result.Selected.Should().Be("Option B");
            result.Score.Should().BeApproximately(0.9f, 0.001f);
        }

        [Fact]
        public async Task ScoredChoiceCommand_Prompt_WrongRequestType_ThrowsInvalidOperationException()
        {
            // Arrange
            var command = new ScoredChoiceCommand(_mockOllama.Object, null, _settings);
            var request = new PromptCommandRequest("Which option is best?");

            // Act
            var act = async () => await command.Prompt(request);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region MessagePromptCommand Tests

        [Fact]
        public async Task MessagePromptCommand_Prompt_WithChatRequest_ReturnsChatMessage()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.ChatPrompt(It.IsAny<ChatRequest>(), "ollama", It.IsAny<Dictionary<string, MethodInfo>>()))
                .ReturnsAsync(new Message { Role = ChatRole.Assistant, Content = "Hello, how can I help you?" });

            var command = new MessagePromptCommand(_mockOllama.Object, null, _settings);
            var request = new ChatCommandRequest("Hello!", (string?)null);

            // Act
            var result = await command.Prompt(request);

            // Assert
            result.Should().NotBeNull();
            result.Content.Should().Be("Hello, how can I help you?");
        }

        [Fact]
        public async Task MessagePromptCommand_Prompt_ServiceThrows_PropagatesException()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.ChatPrompt(It.IsAny<ChatRequest>(), "ollama", It.IsAny<Dictionary<string, MethodInfo>>()))
                .ThrowsAsync(new InvalidOperationException("Chat service unavailable"));

            var command = new MessagePromptCommand(_mockOllama.Object, null, _settings);
            var request = new ChatCommandRequest("Hello!", (string?)null);

            // Act
            var act = async () => await command.Prompt(request);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Chat service unavailable");
        }

        #endregion

        #region RagQueryCommand Tests

        [Fact]
        public async Task RagQueryCommand_Prompt_WithChatRequest_ReturnsChatMessage()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.ChatPrompt(It.IsAny<ChatRequest>(), "ollama", It.IsAny<Dictionary<string, MethodInfo>>()))
                .ReturnsAsync(new Message { Role = ChatRole.Assistant, Content = "Based on the provided data, the answer is X." });

            var command = new RagQueryCommand(_mockOllama.Object, null, _settings);
            var request = new ChatCommandRequest("What does the documentation say about X?", (string?)null);

            // Act
            var result = await command.Prompt(request);

            // Assert
            result.Should().NotBeNull();
            result.Content.Should().Contain("Based on the provided data");
        }

        [Fact]
        public async Task RagQueryCommand_Prompt_ServiceThrows_PropagatesException()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.ChatPrompt(It.IsAny<ChatRequest>(), "ollama", It.IsAny<Dictionary<string, MethodInfo>>()))
                .ThrowsAsync(new InvalidOperationException("RAG service unavailable"));

            var command = new RagQueryCommand(_mockOllama.Object, null, _settings);
            var request = new ChatCommandRequest("What does the documentation say about X?", (string?)null);

            // Act
            var act = async () => await command.Prompt(request);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region UserIntentCommand Tests

        [Fact]
        public async Task UserIntentCommand_Prompt_HappyPath_ReturnsIntentSummary()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.ChatPrompt(It.IsAny<ChatRequest>(), "ollama", It.IsAny<Dictionary<string, MethodInfo>>()))
                .ReturnsAsync(new Message { Role = ChatRole.Assistant, Content = "User wants to know the weather." });

            var command = new UserIntentCommand(_mockOllama.Object, null, _settings);
            var request = new PromptCommandRequest("What will the weather be like tomorrow?");

            // Act
            var result = await command.Prompt(request);

            // Assert
            result.Should().NotBeNull();
            result.Content.Should().Be("User wants to know the weather.");
        }

        [Fact]
        public async Task UserIntentCommand_Prompt_ServiceThrows_PropagatesException()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.ChatPrompt(It.IsAny<ChatRequest>(), "ollama", It.IsAny<Dictionary<string, MethodInfo>>()))
                .ThrowsAsync(new InvalidOperationException("Intent service unavailable"));

            var command = new UserIntentCommand(_mockOllama.Object, null, _settings);
            var request = new PromptCommandRequest("What will the weather be like tomorrow?");

            // Act
            var act = async () => await command.Prompt(request);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region RagExpansionCommand Tests

        [Fact]
        public async Task RagExpansionCommand_Prompt_HappyPath_ReturnsExpandedQueriesList()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.ChatPrompt(It.IsAny<ChatRequest>(), "ollama", It.IsAny<Dictionary<string, MethodInfo>>()))
                .ReturnsAsync(new Message { Role = ChatRole.Assistant, Content = "Expanded version of the query." });

            var command = new RagExpansionCommand(_mockOllama.Object, null, _settings);
            var request = new RagExpansionRequest(2, "What is machine learning?");

            // Act
            var result = await command.Prompt(request);

            // Assert
            result.Should().HaveCount(2);
            result.Should().AllSatisfy(s => s.Should().NotBeNullOrEmpty());
        }

        [Fact]
        public async Task RagExpansionCommand_Prompt_WrongRequestType_ThrowsInvalidOperationException()
        {
            // Arrange
            var command = new RagExpansionCommand(_mockOllama.Object, null, _settings);
            var request = new PromptCommandRequest("What is machine learning?");

            // Act
            var act = async () => await command.Prompt(request);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        #endregion

        #region QueryAugmentationCommand Tests

        [Fact]
        public async Task QueryAugmentationCommand_Prompt_HappyPath_ReturnsAugmentedQuery()
        {
            // Arrange
            _mockOllama
                .Setup(o => o.ChatPrompt(It.IsAny<ChatRequest>(), "ollama", It.IsAny<Dictionary<string, MethodInfo>>()))
                .ReturnsAsync(new Message { Role = ChatRole.Assistant, Content = "How does machine learning work under the hood?" });

            var command = new QueryAugmentationCommand(_mockOllama.Object, null, _settings);
            var request = new RagExpansionRequest(1, "What is machine learning?");

            // Act
            var result = await command.Prompt(request);

            // Assert
            result.Should().HaveCount(1);
            result[0].Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task QueryAugmentationCommand_Prompt_WrongRequestType_ThrowsInvalidOperationException()
        {
            // Arrange
            var command = new QueryAugmentationCommand(_mockOllama.Object, null, _settings);
            var request = new PromptCommandRequest("What is machine learning?");

            // Act
            var act = async () => await command.Prompt(request);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        #endregion
    }
}
