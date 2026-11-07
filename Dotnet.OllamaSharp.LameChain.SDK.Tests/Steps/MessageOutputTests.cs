using System.Text.Json;
using System.Text.Json.Schema;
using FluentAssertions;
using Moq;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using DotnetLlamaSharp.Domain.Services.Inference;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Response;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects.Outputs;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Steps;
using ChatMessage = Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.ChatMessage;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Steps
{
    // Covers the MessagePromptCommand exception case in ChainStep.forgeLink: the chain stores only the
    // message text (not the serialized ChatMessage with its "role") so the next step gets less noise,
    // and ChainLink.ResultObject rebuilds the ChatMessage when the result is requested.
    public class MessageOutputTests
    {
        private const string Answer = "Kingston is the capital of Jamaica.";

        private readonly Mock<IOllamaInferenceService> _mockOllama = new();

        public MessageOutputTests()
        {
            // A plain PromptCommandRequest (not a ChatCommandRequest) makes MessagePromptCommand use GeneratePrompt.
            _mockOllama.Setup(o => o.GeneratePrompt(It.IsAny<GenerateRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new Message(ChatRole.Assistant, Answer));
        }

        private MessagePromptCommand MessageCommand() => new MessagePromptCommand(_mockOllama.Object, "Answer the user question");

        private static ChainRunner MakeRunner() => new ChainRunner("test user input", new CommandSettings("test-model"), null, "test-intent");

        private Mock<IJsoneable> StructuredCommand<T>(T response) where T : class
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var promptResult = new JsonPromptResult("# INSTRUCTION: test instruction", response, typeof(T), JsonSerializer.Serialize(response, options),
                JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(T)), options);

            var mock = new Mock<IJsoneable>();
            mock.Setup(x => x.JsonPrompt(It.IsAny<PromptCommandRequest>(), It.IsAny<CommandSettings?>(), It.IsAny<bool>(), It.IsAny<string?>()))
                .ReturnsAsync(promptResult);
            mock.Setup(x => x.CommandSettings).Returns((CommandSettings?)null);
            mock.Setup(x => x.BorrowLlama).Returns(_mockOllama.Object);
            return mock;
        }

        #region ChainStep.forgeLink Tests

        [Fact]
        public async Task Forge_WithMessagePromptCommand_StoresOnlyTheMessageContentAsSerializedResult()
        {
            // Arrange
            var step = new SingleThrowStep(new StepSettings(MessageCommand(), new PromptCommandRequest("What is the capital of Jamaica?")), MakeRunner());

            // Act
            await step.Forge(null!);

            // Assert
            var link = step.Outputs.Single();
            link.SerializedResult.Should().Be(Answer);
            link.SerializedResult.Should().NotContain("role");
            link.SerializedType.Should().Be(typeof(ChatMessage));
        }

        [Fact]
        public async Task Forge_WithMessagePromptCommand_ReportsOnlyTheMessageContentInTheForgeLog()
        {
            // Arrange
            var runner = MakeRunner();
            var step = new SingleThrowStep(new StepSettings(MessageCommand(), new PromptCommandRequest("What is the capital of Jamaica?")), runner);

            // Act
            await step.Forge(null!);

            // Assert
            runner.ForgedLogs.Should().ContainSingle()
                .Which.JsonResult.Should().Be(Answer);
        }

        [Fact]
        public async Task Forge_AfterMessagePromptCommandStep_NextStepReceivesThePlainTextWithoutTheChatMessageJson()
        {
            // Arrange
            var step1 = new SingleThrowStep(new StepSettings(MessageCommand(), new PromptCommandRequest("What is the capital of Jamaica?")), MakeRunner());
            var step2 = new SingleThrowStep(new StepSettings(StructuredCommand(new BooleanResponse { Answer = true }).Object, new PromptCommandRequest("Is the answer correct?")));
            step1.Link(step2, isForward: true, isTwoWay: true);

            // Act
            await step1.Forge(null!);

            // Assert
            step2.Request.GuidanceMessage.Should().Contain(Answer)
                .And.NotContain("\"role\"")
                .And.NotContain("\"content\"");
        }

        [Fact]
        public async Task GetOutputAs_WithMessagePromptCommandStep_RebuildsAnAssistantChatMessage()
        {
            // Arrange
            var step = new SingleThrowStep(new StepSettings(MessageCommand(), new PromptCommandRequest("What is the capital of Jamaica?")), MakeRunner());
            await step.Forge(null!);

            // Act
            var message = step.GetOutputAs<ChatMessage>();

            // Assert
            message.Role.Should().Be(ChatRole.Assistant.ToString());
            message.Content.Should().Be(Answer);
        }

        [Fact]
        public async Task Forge_WithStructuredCommand_KeepsTheRawJsonAsSerializedResult()
        {
            // Arrange: regression guard - the exception case must only apply to MessagePromptCommand.
            var step = new SingleThrowStep(new StepSettings(StructuredCommand(new BooleanResponse { Answer = true }).Object, new PromptCommandRequest("Is it true?")), MakeRunner());

            // Act
            await step.Forge(null!);

            // Assert
            var link = step.Outputs.Single();
            JsonDocument.Parse(link.SerializedResult).RootElement.GetProperty("answer").GetBoolean().Should().BeTrue();
            step.GetOutputAs<BooleanResponse>().Answer.Should().BeTrue();
        }

        #endregion

        #region ChainLink.ResultObject Tests

        [Fact]
        public void ResultObject_WithChatMessageType_WrapsThePlainTextInAnAssistantChatMessage()
        {
            // Arrange
            var link = BuildLink(Answer, typeof(ChatMessage));

            // Act
            var result = link.ResultObject();

            // Assert
            var message = result.Should().BeOfType<ChatMessage>().Subject;
            message.Role.Should().Be(ChatRole.Assistant.ToString());
            message.Content.Should().Be(Answer);
        }

        [Fact]
        public void ResultObject_WithChatMessageTypeAndTextThatIsNotJson_DoesNotTryToDeserializeIt()
        {
            // Arrange: plain LLM text is no longer JSON, so it must never reach JsonSerializer.
            var link = BuildLink("{ not json, just braces", typeof(ChatMessage));

            // Act
            var result = link.ResultObject();

            // Assert
            ((ChatMessage)result).Content.Should().Be("{ not json, just braces");
        }

        [Fact]
        public void ResultObject_WithStructuredType_DeserializesTheJson()
        {
            // Arrange
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var link = BuildLink(JsonSerializer.Serialize(new BooleanResponse { Answer = true }, options), typeof(BooleanResponse), options);

            // Act
            var result = link.ResultObject();

            // Assert
            result.Should().BeOfType<BooleanResponse>().Which.Answer.Should().BeTrue();
        }

        [Fact]
        public void ResultObject_WithStructuredTypeAndJsonNull_ThrowsInvalidOperationException()
        {
            // Arrange
            var link = BuildLink("null", typeof(BooleanResponse));

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => link.ResultObject());
        }

        #endregion

        #region Helper Methods

        private static ChainLink BuildLink(string serializedResult, Type type, JsonSerializerOptions? options = null)
            => new ChainLink(Guid.NewGuid(), "# INSTRUCTION: test", serializedResult, JsonSerializerOptions.Default.GetJsonSchemaAsNode(type), type,
                deserializerOptions: options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web));

        #endregion
    }
}
