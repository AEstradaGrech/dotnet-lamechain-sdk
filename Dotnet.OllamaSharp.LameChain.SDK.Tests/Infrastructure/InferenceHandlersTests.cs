using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.InferenceHandlers;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Clients;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Request;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Response.GroqProvider;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Extensions.Model;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using DotnetLlamaSharp.Infrastructure.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Infrastructure
{
    // Test-only tool source. Two methods cover both branches of BaseHandler.getToolResult's
    // "invokeResult is Task" check (sync result vs Task<T> that must be awaited/unwrapped).
    public class TestToolsService : ToolsService<TestToolsService>
    {
        [Description("Echoes the input back")]
        public string Echo(string input) => input;

        [Description("Echoes the input back asynchronously")]
        public Task<string> EchoAsync(string input) => Task.FromResult(input);
    }

    // Minimal concrete BaseHandler used to test base-class behaviors (ctor validation, IsProvider,
    // IsOfType/AsType, UpdateHandler, isValid, and the recursive tool-call loop) in isolation from
    // any provider-specific HTTP client mocking.
    public class TestBaseHandler : BaseHandler
    {
        private readonly string _cannedResponse;
        public int GetLlmResponseCallCount { get; private set; }

        public TestBaseHandler(IServiceProvider serviceProvider, IConfiguration config, string provider, string cannedResponse = "canned-response")
            : base(serviceProvider, config, provider)
        {
            _cannedResponse = cannedResponse;
        }

        public override Task<string> GetLlmResponse(ChatRequest request, Dictionary<string, MethodInfo>? requestTools = null)
        {
            GetLlmResponseCallCount++;
            return Task.FromResult(_cannedResponse);
        }

        public bool InvokeIsValid() => isValid();

        public Task<string> InvokeHandleFunctionCall(ChatRequest request, Message message, Dictionary<string, MethodInfo>? toolsLookup)
            => handleFunctionCall(request, message, toolsLookup);

        public List<Message> InvokeGetToolResponseMessages(string toolName, Message toolRequestMessage, object? toolResult)
            => getToolResponseMessages(toolName, toolRequestMessage, toolResult);

        public Task<object?> InvokeGetToolResult(MethodInfo methodInfo, IDictionary<string, object> arguments)
            => getToolResult(methodInfo, arguments);
    }

    // Exposes GroqHandler's protected getToolResponseMessages override for direct testing of its
    // Groq-specific differentiation (copying ToolCalls onto the tool-response message).
    public class TestableGroqHandler : GroqHandler
    {
        public TestableGroqHandler(IServiceProvider serviceProvider, IConfiguration config) : base(serviceProvider, config) { }

        public List<Message> InvokeGetToolResponseMessages(string toolName, Message toolRequestMessage, object? toolResult)
            => getToolResponseMessages(toolName, toolRequestMessage, toolResult);
    }

    public class BaseHandlerTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidProvider_TrimsAndStoresProvider()
        {
            // Arrange
            var mockServiceProvider = new Mock<IServiceProvider>();
            var mockConfig = new Mock<IConfiguration>();

            // Act
            var handler = new TestBaseHandler(mockServiceProvider.Object, mockConfig.Object, "  ollama  ");

            // Assert
            handler.IsProvider("ollama").Should().BeTrue();
        }

        [Fact]
        public void Constructor_WithEmptyProvider_ThrowsInvalidDataException()
        {
            // Arrange
            var mockServiceProvider = new Mock<IServiceProvider>();
            var mockConfig = new Mock<IConfiguration>();

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => new TestBaseHandler(mockServiceProvider.Object, mockConfig.Object, ""));
        }

        [Fact]
        public void Constructor_WithWhitespaceOnlyProvider_ThrowsInvalidDataException()
        {
            // Arrange
            var mockServiceProvider = new Mock<IServiceProvider>();
            var mockConfig = new Mock<IConfiguration>();

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => new TestBaseHandler(mockServiceProvider.Object, mockConfig.Object, "   "));
        }

        #endregion

        #region IsProvider Tests

        [Fact]
        public void IsProvider_WithExactMatch_ReturnsTrue()
        {
            // Arrange
            var handler = new TestBaseHandler(new Mock<IServiceProvider>().Object, new Mock<IConfiguration>().Object, "ollama");

            // Act & Assert
            handler.IsProvider("ollama").Should().BeTrue();
        }

        [Fact]
        public void IsProvider_WithDifferentCasing_ReturnsFalse()
        {
            // Arrange
            var handler = new TestBaseHandler(new Mock<IServiceProvider>().Object, new Mock<IConfiguration>().Object, "ollama");

            // Act & Assert
            handler.IsProvider("Ollama").Should().BeFalse();
        }

        #endregion

        #region IsOfType And AsType Tests

        [Fact]
        public void IsOfType_WithMatchingConcreteType_ReturnsTrue()
        {
            // Arrange
            var handler = new TestBaseHandler(new Mock<IServiceProvider>().Object, new Mock<IConfiguration>().Object, "test");

            // Act & Assert
            handler.IsOfType<TestBaseHandler>().Should().BeTrue();
        }

        [Fact]
        public void IsOfType_WithNonMatchingType_ReturnsFalse()
        {
            // Arrange
            var handler = new TestBaseHandler(new Mock<IServiceProvider>().Object, new Mock<IConfiguration>().Object, "test");

            // Act & Assert
            handler.IsOfType<OllamaHandler>().Should().BeFalse();
        }

        [Fact]
        public void AsType_WithCorrectType_ReturnsSameInstanceCast()
        {
            // Arrange
            var handler = new TestBaseHandler(new Mock<IServiceProvider>().Object, new Mock<IConfiguration>().Object, "test");

            // Act
            var casted = handler.AsType<TestBaseHandler>();

            // Assert
            casted.Should().BeSameAs(handler);
        }

        [Fact]
        public void AsType_WithIncorrectType_ThrowsInvalidCastException()
        {
            // Arrange
            var handler = new TestBaseHandler(new Mock<IServiceProvider>().Object, new Mock<IConfiguration>().Object, "test");

            // Act & Assert
            Assert.Throws<InvalidCastException>(() => handler.AsType<OllamaHandler>());
        }

        #endregion

        #region UpdateHandler Tests

        [Fact]
        public void UpdateHandler_WithOllamaProvider_ReturnsNewOllamaHandlerInstance()
        {
            // Arrange
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IOllamaApiClient))).Returns(new Mock<IOllamaApiClient>().Object);
            var handler = new TestBaseHandler(mockServiceProvider.Object, new Mock<IConfiguration>().Object, "test");

            // Act
            var updated = handler.UpdateHandler("ollama");

            // Assert
            updated.IsOfType<OllamaHandler>().Should().BeTrue();
            ReferenceEquals(updated, handler).Should().BeFalse();
        }

        [Fact]
        public void UpdateHandler_WithGroqProvider_ReturnsNewGroqHandlerInstance()
        {
            // Arrange
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IGroqClient))).Returns(new Mock<IGroqClient>().Object);
            var handler = new TestBaseHandler(mockServiceProvider.Object, new Mock<IConfiguration>().Object, "test");

            // Act
            var updated = handler.UpdateHandler("groq");

            // Assert
            updated.IsOfType<GroqHandler>().Should().BeTrue();
        }

        [Fact]
        public void UpdateHandler_WithUnknownProvider_FallsBackToOllamaHandler()
        {
            // Arrange
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IOllamaApiClient))).Returns(new Mock<IOllamaApiClient>().Object);
            var handler = new TestBaseHandler(mockServiceProvider.Object, new Mock<IConfiguration>().Object, "test");

            // Act
            var updated = handler.UpdateHandler("bogus-provider");

            // Assert
            updated.IsOfType<OllamaHandler>().Should().BeTrue();
        }

        #endregion

        #region isValid Tests

        [Fact]
        public void IsValid_WithAllDependenciesPresent_ReturnsTrue()
        {
            // Arrange
            var handler = new TestBaseHandler(new Mock<IServiceProvider>().Object, new Mock<IConfiguration>().Object, "test");

            // Act & Assert
            handler.InvokeIsValid().Should().BeTrue();
        }

        #endregion

        #region Tool Call Loop Tests (canonical BaseHandler.handleFunctionCall coverage)

        [Fact]
        public async Task HandleFunctionCall_WithNullToolsLookup_ThrowsArgumentNullException()
        {
            // Arrange
            var handler = new TestBaseHandler(new Mock<IServiceProvider>().Object, new Mock<IConfiguration>().Object, "test");
            var message = BuildToolCallMessage("Echo", new Dictionary<string, object?>());
            var request = new ChatRequest { Messages = new List<Message>() };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => handler.InvokeHandleFunctionCall(request, message, null));
        }

        [Fact]
        public async Task HandleFunctionCall_WithUnknownToolName_ThrowsInvalidOperationException()
        {
            // Arrange
            var handler = new TestBaseHandler(new Mock<IServiceProvider>().Object, new Mock<IConfiguration>().Object, "test");
            var message = BuildToolCallMessage("UnknownTool", new Dictionary<string, object?>());
            var request = new ChatRequest { Messages = new List<Message>() };
            var toolsLookup = new Dictionary<string, MethodInfo>();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.InvokeHandleFunctionCall(request, message, toolsLookup));
        }

        [Fact]
        public async Task HandleFunctionCall_WithValidSyncTool_InvokesToolAndRecursesIntoGetLlmResponse()
        {
            // Arrange
            var toolsService = new TestToolsService();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IToolsService<TestToolsService>))).Returns(toolsService);
            var handler = new TestBaseHandler(mockServiceProvider.Object, new Mock<IConfiguration>().Object, "test", "recursive-response");

            var toolMessage = BuildToolCallMessage("Echo", new Dictionary<string, object?> { ["input"] = JsonSerializer.SerializeToElement("hello") });
            var request = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") } };
            var toolsLookup = new Dictionary<string, MethodInfo> { ["Echo"] = typeof(TestToolsService).GetMethod(nameof(TestToolsService.Echo))! };

            // Act
            var result = await handler.InvokeHandleFunctionCall(request, toolMessage, toolsLookup);

            // Assert
            result.Should().Be("recursive-response");
            handler.GetLlmResponseCallCount.Should().Be(1);
            var messages = request.Messages.ToList();
            messages.Should().HaveCount(3);
            messages[1].Should().BeSameAs(toolMessage);
            messages[2].Role.Should().Be(ChatRole.Tool);
            messages[2].ToolName.Should().Be("Echo");
            messages[2].Content.Should().Be(JsonSerializer.Serialize("hello"));
        }

        [Fact]
        public async Task HandleFunctionCall_WithAsyncTaskReturningTool_AwaitsAndUnwrapsResult()
        {
            // Arrange
            var toolsService = new TestToolsService();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IToolsService<TestToolsService>))).Returns(toolsService);
            var handler = new TestBaseHandler(mockServiceProvider.Object, new Mock<IConfiguration>().Object, "test", "recursive-response");

            var toolMessage = BuildToolCallMessage("EchoAsync", new Dictionary<string, object?> { ["input"] = JsonSerializer.SerializeToElement("hello") });
            var request = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") } };
            var toolsLookup = new Dictionary<string, MethodInfo> { ["EchoAsync"] = typeof(TestToolsService).GetMethod(nameof(TestToolsService.EchoAsync))! };

            // Act
            var result = await handler.InvokeHandleFunctionCall(request, toolMessage, toolsLookup);

            // Assert
            result.Should().Be("recursive-response");
            var messages = request.Messages.ToList();
            messages[2].Content.Should().Be(JsonSerializer.Serialize("hello"));
        }

        [Fact]
        public async Task GetToolResult_WithMissingRequiredArgument_ThrowsInvalidOperationException()
        {
            // Arrange: exercises the real (unmocked) OllamaTools.ParseToolCallArguments throw path.
            var toolsService = new TestToolsService();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IToolsService<TestToolsService>))).Returns(toolsService);
            var handler = new TestBaseHandler(mockServiceProvider.Object, new Mock<IConfiguration>().Object, "test");
            var methodInfo = typeof(TestToolsService).GetMethod(nameof(TestToolsService.Echo))!;
            var arguments = new Dictionary<string, object>(); // missing required "input"

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.InvokeGetToolResult(methodInfo, arguments));
            ex.Message.Should().Contain("Missing required argument");
        }

        [Fact]
        public void GetToolResponseMessages_Default_ReturnsRequestThenResponseMessageWithToolName()
        {
            // Arrange
            var handler = new TestBaseHandler(new Mock<IServiceProvider>().Object, new Mock<IConfiguration>().Object, "test");
            var requestMessage = new Message(ChatRole.Assistant, string.Empty);

            // Act
            var messages = handler.InvokeGetToolResponseMessages("Echo", requestMessage, "hello");

            // Assert
            messages.Should().HaveCount(2);
            messages[0].Should().BeSameAs(requestMessage);
            messages[1].Role.Should().Be(ChatRole.Tool);
            messages[1].ToolName.Should().Be("Echo");
            messages[1].Content.Should().Be(JsonSerializer.Serialize("hello"));
        }

        #endregion

        #region Helper Methods

        private static Message BuildToolCallMessage(string toolName, Dictionary<string, object?> arguments)
        {
            var toolCall = new Message.ToolCall
            {
                Id = "call_1",
                Function = new Message.Function { Name = toolName, Arguments = arguments }
            };
            return new Message(ChatRole.Assistant, string.Empty) { ToolCalls = new List<Message.ToolCall> { toolCall } };
        }

        #endregion
    }

    public class OllamaHandlerTests
    {
        private readonly Mock<IOllamaApiClient> _mockClient;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<IConfiguration> _mockConfig;

        public OllamaHandlerTests()
        {
            _mockClient = new Mock<IOllamaApiClient>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IOllamaApiClient))).Returns(_mockClient.Object);
            _mockConfig = new Mock<IConfiguration>();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidServiceProvider_ResolvesOllamaApiClient()
        {
            // Act
            var handler = new OllamaHandler(_mockServiceProvider.Object, _mockConfig.Object);

            // Assert
            handler.Should().NotBeNull();
            handler.IsProvider("ollama").Should().BeTrue();
        }

        [Fact]
        public void Constructor_WithMissingOllamaApiClientRegistration_ThrowsInvalidOperationException()
        {
            // Arrange
            var emptyProvider = new Mock<IServiceProvider>();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => new OllamaHandler(emptyProvider.Object, _mockConfig.Object));
        }

        #endregion

        #region GetLlmResponse Tests

        [Fact]
        public async Task GetLlmResponse_WithTextOnlyResponseParts_ReturnsAggregatedTrimmedContent()
        {
            // Arrange
            var handler = new OllamaHandler(_mockServiceProvider.Object, _mockConfig.Object);
            var parts = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message(ChatRole.Assistant, "Hello ") },
                new ChatResponseStream { Message = new Message(ChatRole.Assistant, "World") }
            };
            _mockClient.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>())).Returns(GetAsyncEnumerable(parts));
            var request = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") } };

            // Act
            var result = await handler.GetLlmResponse(request);

            // Assert
            result.Should().Be("Hello World");
        }

        [Fact]
        public async Task GetLlmResponse_WithNullAndEmptyContentParts_FiltersThemOut()
        {
            // Arrange
            var handler = new OllamaHandler(_mockServiceProvider.Object, _mockConfig.Object);
            var parts = new List<ChatResponseStream?>
            {
                null,
                new ChatResponseStream { Message = new Message(ChatRole.Assistant, "Valid") },
                new ChatResponseStream { Message = new Message(ChatRole.Assistant, "") },
                new ChatResponseStream { Message = new Message(ChatRole.Assistant, " ") },
                new ChatResponseStream { Message = new Message(ChatRole.Assistant, "Response") }
            };
            _mockClient.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>())).Returns(GetAsyncEnumerableNullable(parts));
            var request = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") } };

            // Act
            var result = await handler.GetLlmResponse(request);

            // Assert
            result.Should().Be("Valid Response");
        }

        [Fact]
        public async Task GetLlmResponse_WithToolCallInResponse_DelegatesToHandleFunctionCall()
        {
            // Arrange: proves OllamaHandler detects ToolCalls and delegates to the (inherited,
            // already-covered-in-BaseHandlerTests) handleFunctionCall loop; not re-verifying full
            // recursion mechanics here.
            var toolsService = new TestToolsService();
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IToolsService<TestToolsService>))).Returns(toolsService);
            var handler = new OllamaHandler(_mockServiceProvider.Object, _mockConfig.Object);

            var toolCall = new Message.ToolCall
            {
                Id = "call_1",
                Function = new Message.Function { Name = "Echo", Arguments = new Dictionary<string, object?> { ["input"] = JsonSerializer.SerializeToElement("hello") } }
            };
            var toolCallMessage = new Message(ChatRole.Assistant, string.Empty) { ToolCalls = new List<Message.ToolCall> { toolCall } };

            _mockClient.SetupSequence(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(new List<ChatResponseStream> { new ChatResponseStream { Message = toolCallMessage } }))
                .Returns(GetAsyncEnumerable(new List<ChatResponseStream> { new ChatResponseStream { Message = new Message(ChatRole.Assistant, "final answer") } }));

            var request = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") } };
            var toolsLookup = new Dictionary<string, MethodInfo> { ["Echo"] = typeof(TestToolsService).GetMethod(nameof(TestToolsService.Echo))! };

            // Act
            var result = await handler.GetLlmResponse(request, toolsLookup);

            // Assert
            result.Should().Be("final answer");
            _mockClient.Verify(c => c.ChatAsync(It.IsAny<ChatRequest>()), Times.Exactly(2));
        }

        [Fact]
        public async Task GetLlmResponse_WhenIsValidFalse_ThrowsInvalidOperationException()
        {
            // Arrange: null IConfiguration makes BaseHandler.isValid() false, while the ctor's
            // IOllamaApiClient resolution still succeeds so construction itself doesn't throw.
            var handler = new OllamaHandler(_mockServiceProvider.Object, null!);
            var request = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") } };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.GetLlmResponse(request));
        }

        #endregion

        #region GenerateLlmResponse Tests

        [Fact]
        public async Task GenerateLlmResponse_WithValidPromptOrSystem_ReturnsAggregatedTrimmedResponse()
        {
            // Arrange
            var handler = new OllamaHandler(_mockServiceProvider.Object, _mockConfig.Object);
            var request = new GenerateRequest { Prompt = "test prompt" };
            var parts = new List<GenerateResponseStream>
            {
                new GenerateResponseStream { Response = "Hello " },
                new GenerateResponseStream { Response = "World" }
            };
            _mockClient.Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>())).Returns(GetAsyncEnumerable(parts));

            // Act
            var result = await handler.GenerateLlmResponse(request);

            // Assert
            result.Should().Be("Hello World");
        }

        [Fact]
        public async Task GenerateLlmResponse_WithEmptySystemAndPrompt_ThrowsInvalidDataException()
        {
            // Arrange: this is the Ollama-only "GenerateRequest exception" path - no other provider
            // has a native generate endpoint, so this validation only exists here.
            var handler = new OllamaHandler(_mockServiceProvider.Object, _mockConfig.Object);
            var request = new GenerateRequest();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidDataException>(() => handler.GenerateLlmResponse(request));
        }

        #endregion

        #region Helper Methods

        private IAsyncEnumerable<T> GetAsyncEnumerable<T>(List<T> items) => GetAsyncEnumerableIterator(items);

        private async IAsyncEnumerable<T> GetAsyncEnumerableIterator<T>(List<T> items)
        {
            foreach (var item in items)
                yield return item;
        }

        private IAsyncEnumerable<T?> GetAsyncEnumerableNullable<T>(List<T?> items) => GetAsyncEnumerableNullableIterator(items);

        private async IAsyncEnumerable<T?> GetAsyncEnumerableNullableIterator<T>(List<T?> items)
        {
            foreach (var item in items)
                yield return item;
        }

        #endregion
    }

    public class GroqHandlerTests
    {
        private readonly Mock<IGroqClient> _mockClient;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<IConfiguration> _mockConfig;

        public GroqHandlerTests()
        {
            _mockClient = new Mock<IGroqClient>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IGroqClient))).Returns(_mockClient.Object);
            _mockConfig = new Mock<IConfiguration>();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidServiceProvider_ResolvesGroqClient()
        {
            // Act
            var handler = new GroqHandler(_mockServiceProvider.Object, _mockConfig.Object);

            // Assert
            handler.Should().NotBeNull();
            handler.IsProvider("groq").Should().BeTrue();
        }

        [Fact]
        public void Constructor_WithMissingGroqClientRegistration_ThrowsInvalidOperationException()
        {
            // Arrange
            var emptyProvider = new Mock<IServiceProvider>();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => new GroqHandler(emptyProvider.Object, _mockConfig.Object));
        }

        #endregion

        #region GetLlmResponse Tests

        [Fact]
        public async Task GetLlmResponse_WithChoicesAndNoToolCalls_ReturnsTrimmedContent()
        {
            // Arrange
            var handler = new GroqHandler(_mockServiceProvider.Object, _mockConfig.Object);
            var completion = new GroqChatCompletion
            {
                Choices = new List<GroqCompletionChoice>
                {
                    new GroqCompletionChoice { Message = new Message(ChatRole.Assistant, "  answer  ") }
                }
            };
            _mockClient.Setup(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>())).ReturnsAsync(completion);
            var request = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") }, Tools = new List<object>() };

            // Act
            var result = await handler.GetLlmResponse(request);

            // Assert
            result.Should().Be("answer");
        }

        [Fact]
        public async Task GetLlmResponse_WithEmptyChoices_ThrowsInvalidDataException()
        {
            // Arrange
            var handler = new GroqHandler(_mockServiceProvider.Object, _mockConfig.Object);
            var completion = new GroqChatCompletion { Choices = new List<GroqCompletionChoice>() };
            _mockClient.Setup(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>())).ReturnsAsync(completion);
            var request = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") }, Tools = new List<object>() };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidDataException>(() => handler.GetLlmResponse(request));
        }

        [Fact]
        public async Task GetLlmResponse_WithToolCallInResponse_DelegatesToHandleFunctionCall()
        {
            // Arrange
            var toolsService = new TestToolsService();
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IToolsService<TestToolsService>))).Returns(toolsService);
            var handler = new GroqHandler(_mockServiceProvider.Object, _mockConfig.Object);

            var toolCall = new Message.ToolCall
            {
                Id = "call_1",
                Function = new Message.Function { Name = "Echo", Arguments = new Dictionary<string, object?> { ["input"] = JsonSerializer.SerializeToElement("hello") } }
            };
            var toolCallMessage = new Message(ChatRole.Assistant, string.Empty) { ToolCalls = new List<Message.ToolCall> { toolCall } };

            var completionWithTool = new GroqChatCompletion { Choices = new List<GroqCompletionChoice> { new GroqCompletionChoice { Message = toolCallMessage } } };
            var completionFinal = new GroqChatCompletion { Choices = new List<GroqCompletionChoice> { new GroqCompletionChoice { Message = new Message(ChatRole.Assistant, "final answer") } } };

            _mockClient.SetupSequence(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>()))
                .ReturnsAsync(completionWithTool)
                .ReturnsAsync(completionFinal);

            var request = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") }, Tools = new List<object>() };
            var toolsLookup = new Dictionary<string, MethodInfo> { ["Echo"] = typeof(TestToolsService).GetMethod(nameof(TestToolsService.Echo))! };

            // Act
            var result = await handler.GetLlmResponse(request, toolsLookup);

            // Assert
            result.Should().Be("final answer");
            _mockClient.Verify(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>()), Times.Exactly(2));
        }

        #endregion

        #region getToolResponseMessages Override Tests (Groq-specific differentiation)

        [Fact]
        public void GetToolResponseMessages_Override_CopiesToolCallsOntoResponseMessage()
        {
            // Arrange: unlike BaseHandler's default (which leaves ToolCalls unset on the tool-response
            // message), Groq's wire format needs the tool_call_id, which GroqToolMessageConverter
            // extracts from ToolCalls on the response message - hence this override copies it over.
            var handler = new TestableGroqHandler(_mockServiceProvider.Object, _mockConfig.Object);
            var toolCalls = new List<Message.ToolCall> { new Message.ToolCall { Id = "call_1", Function = new Message.Function { Name = "Echo" } } };
            var requestMessage = new Message(ChatRole.Assistant, string.Empty) { ToolCalls = toolCalls };

            // Act
            var messages = handler.InvokeGetToolResponseMessages("Echo", requestMessage, "hello");

            // Assert
            messages.Should().HaveCount(2);
            messages[1].ToolCalls.Should().BeSameAs(toolCalls);
            messages[1].ToolName.Should().Be("Echo");
        }

        #endregion

        #region AsGroqRequest Characterization Tests

        // NOTE: these test Dotnet.OllamaSharp.LameChain.SDK.Extensions.Model.OllamaRequestExtensions.AsGroqRequest,
        // not GroqHandler itself, but it's the wire-format mapping GroqHandler.GetLlmResponse depends on,
        // and is scoped here per the "provider/tools mapping" ask rather than in a separate file.

        [Fact]
        public void AsGroqRequest_Characterization_MutatesInputRequestOptionsFrequencyPenaltyToZero()
        {
            // Documents an existing side-effect bug: `FrequencyPenalty = req.Options.FrequencyPenalty = 0`
            // mutates the caller's ChatRequest.Options as a side effect. This characterizes current
            // behavior, not a spec to preserve.
            // Arrange
            var options = new RequestOptions { FrequencyPenalty = 0.9f };
            var chatRequest = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") }, Options = options, Tools = new List<object>() };

            // Act
            var groqRequest = chatRequest.AsGroqRequest();

            // Assert
            groqRequest.FrequencyPenalty.Should().Be(0f);
            chatRequest.Options.FrequencyPenalty.Should().Be(0f);
        }

        [Fact]
        public void AsGroqRequest_WithNoMessages_ThrowsInvalidOperationException()
        {
            // Arrange
            var chatRequest = new ChatRequest { Messages = new List<Message>() };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => chatRequest.AsGroqRequest());
        }

        #endregion
    }

    // NOTE: OllamaInferenceServiceTests.cs predates the handler-abstraction refactor and only ever
    // exercises the "ollama" provider path (its tests pass today, but incidentally - the shared
    // handler happens to already be wired to the same mocked IOllamaApiClient). It provides no
    // coverage of IsProvider/IsOfType/UpdateHandler/tool-passing. This class specifically covers the
    // new provider-selection and tool-passing logic introduced by that refactor, deliberately kept
    // separate rather than folded into the existing (larger, unrelated) file.
    public class OllamaInferenceServiceProviderSelectionTests
    {
        private readonly Mock<IOllamaApiClient> _mockOllamaClient;
        private readonly Mock<IGroqClient> _mockGroqClient;
        private readonly Mock<IOptions<OllamaSettings>> _mockOptions;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly OllamaSettings _settings;
        private readonly OllamaInferenceService _service;

        public OllamaInferenceServiceProviderSelectionTests()
        {
            _mockOllamaClient = new Mock<IOllamaApiClient>();
            _mockGroqClient = new Mock<IGroqClient>();
            _settings = new OllamaSettings { DefaultModel = "test-model" };
            _mockOptions = new Mock<IOptions<OllamaSettings>>();
            _mockOptions.Setup(o => o.Value).Returns(_settings);
            _mockConfig = new Mock<IConfiguration>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IOllamaApiClient))).Returns(_mockOllamaClient.Object);
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IGroqClient))).Returns(_mockGroqClient.Object);
            // Deliberately NOT registering IClaudeClient - ClaudeHandler is out of scope, and any
            // accidental attempt to resolve it should fail loudly rather than silently succeed.

            _service = new OllamaInferenceService(_mockOllamaClient.Object, _mockConfig.Object, _mockServiceProvider.Object, _mockOptions.Object);
        }

        #region GeneratePrompt / IsOfType<OllamaHandler> Branch Tests

        [Fact]
        public async Task GeneratePrompt_HandlerIsOllamaHandler_UsesGenerateLlmResponsePath()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt" };
            _mockOllamaClient.Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerable(new List<GenerateResponseStream> { new GenerateResponseStream { Response = "answer" } }));

            // Act
            var result = await _service.GeneratePrompt(request, "ollama");

            // Assert
            result.Content.Should().Be("answer");
            _mockOllamaClient.Verify(c => c.GenerateAsync(It.IsAny<GenerateRequest>()), Times.Once);
            _mockOllamaClient.Verify(c => c.ChatAsync(It.IsAny<ChatRequest>()), Times.Never);
        }

        [Fact]
        public async Task GeneratePrompt_ProviderClaudeButHandlerStillOllamaHandler_IgnoresProviderAndUsesGenerateLlmResponsePath()
        {
            // Arrange: characterizes the asymmetry between the GenerateRequest overloads (which branch
            // on "is the cached handler currently an OllamaHandler") and the ChatRequest overloads
            // (which branch on "does the requested provider match the cached handler's provider name").
            // A freshly constructed service's handler is already an OllamaHandler, so it stays on the
            // native generate path regardless of the requested provider - IClaudeClient is never touched.
            var request = new GenerateRequest { Prompt = "test prompt" };
            _mockOllamaClient.Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerable(new List<GenerateResponseStream> { new GenerateResponseStream { Response = "answer" } }));

            // Act
            var result = await _service.GeneratePrompt(request, "claude");

            // Assert
            result.Content.Should().Be("answer");
            _mockOllamaClient.Verify(c => c.GenerateAsync(It.IsAny<GenerateRequest>()), Times.Once);
            _mockServiceProvider.Verify(sp => sp.GetService(typeof(IClaudeClient)), Times.Never);
        }

        #endregion

        #region ChatPrompt / IsProvider Branch + Tool-Passing Tests

        [Fact]
        public async Task ChatPrompt_ProviderDiffersFromCurrentHandler_SwapsHandlerAndReassignsField()
        {
            // Arrange
            var chatRequest = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") }, Tools = new List<object>() };
            var completion = new GroqChatCompletion { Choices = new List<GroqCompletionChoice> { new GroqCompletionChoice { Message = new Message(ChatRole.Assistant, "groq answer") } } };
            _mockGroqClient.Setup(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>())).ReturnsAsync(completion);

            // Act
            var result = await _service.ChatPrompt(chatRequest, "groq");

            // Assert
            result.Content.Should().Be("groq answer");
            _mockGroqClient.Verify(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>()), Times.Once);
        }

        [Fact]
        public async Task ChatPrompt_WithRequestTools_PassesToolDictionaryThroughToHandlerGetLlmResponse()
        {
            // Arrange: proves requestTools flows ChatPrompt -> handler.GetLlmResponse -> handleFunctionCall.
            var toolsService = new TestToolsService();
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IToolsService<TestToolsService>))).Returns(toolsService);

            var toolCall = new Message.ToolCall
            {
                Id = "call_1",
                Function = new Message.Function { Name = "Echo", Arguments = new Dictionary<string, object?> { ["input"] = JsonSerializer.SerializeToElement("hello") } }
            };
            var toolCallMessage = new Message(ChatRole.Assistant, string.Empty) { ToolCalls = new List<Message.ToolCall> { toolCall } };

            var completionWithTool = new GroqChatCompletion { Choices = new List<GroqCompletionChoice> { new GroqCompletionChoice { Message = toolCallMessage } } };
            var completionFinal = new GroqChatCompletion { Choices = new List<GroqCompletionChoice> { new GroqCompletionChoice { Message = new Message(ChatRole.Assistant, "final answer") } } };

            _mockGroqClient.SetupSequence(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>()))
                .ReturnsAsync(completionWithTool)
                .ReturnsAsync(completionFinal);

            var chatRequest = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") }, Tools = new List<object>() };
            var requestTools = new Dictionary<string, MethodInfo> { ["Echo"] = typeof(TestToolsService).GetMethod(nameof(TestToolsService.Echo))! };

            // Act
            var result = await _service.ChatPrompt(chatRequest, "groq", requestTools);

            // Assert
            result.Content.Should().Be("final answer");
            _mockGroqClient.Verify(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>()), Times.Exactly(2));
        }

        [Fact]
        public async Task ChatPrompt_ProviderMatchesCurrentHandler_DoesNotSwapHandler()
        {
            // Arrange
            var chatRequest = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") } };
            _mockOllamaClient.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(new List<ChatResponseStream> { new ChatResponseStream { Message = new Message(ChatRole.Assistant, "answer") } }));

            // Act
            var result = await _service.ChatPrompt(chatRequest, "ollama");

            // Assert
            result.Content.Should().Be("answer");
            _mockServiceProvider.Verify(sp => sp.GetService(typeof(IGroqClient)), Times.Never);
        }

        #endregion

        #region StructuredPrompt Regression Test

        [Fact]
        public async Task StructuredPrompt_WithDifferentProvider_HandlerSwapHasNoEffectDueToMissingReassignment()
        {
            // Arrange: characterizes an existing bug - StructuredPrompt does
            // `if (!_handler.IsProvider(provider)) _handler.UpdateHandler(provider);` without
            // reassigning the result back to `_handler`, so the swap has no observable effect.
            // This documents current (unintended) behavior, not a spec to preserve.
            _mockOllamaClient.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(new List<ChatResponseStream> { new ChatResponseStream { Message = new Message(ChatRole.Assistant, "{}") } }));

            // Act
            var result = await _service.StructuredPrompt<TestStructuredOutput>("prompt", "model", "groq");

            // Assert
            result.Should().NotBeNull();
            _mockOllamaClient.Verify(c => c.ChatAsync(It.IsAny<ChatRequest>()), Times.Once);
            _mockGroqClient.Verify(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>()), Times.Never);
        }

        #endregion

        #region Helper Methods

        private IAsyncEnumerable<T> GetAsyncEnumerable<T>(List<T> items) => GetAsyncEnumerableIterator(items);

        private async IAsyncEnumerable<T> GetAsyncEnumerableIterator<T>(List<T> items)
        {
            foreach (var item in items)
                yield return item;
        }

        #endregion
    }

    public class PromptCommandRequestProviderParsingTests
    {
        [Theory]
        [InlineData("claude/claude-3-opus", "claude", "claude-3-opus")]
        [InlineData("claude/foo/bar", "claude", "foo/bar")]
        [InlineData("llama3", "ollama", "llama3")]
        [InlineData(null, "ollama", "")]
        [InlineData("", "ollama", "")]
        public void SetModel_VariousInputs_ParsesProviderAndModelCorrectly(string? input, string expectedProvider, string expectedModel)
        {
            // Act
            var request = new PromptCommandRequest("prompt") { Model = input };

            // Assert
            request.Provider.Should().Be(expectedProvider);
            request.Model.Should().Be(expectedModel);
        }
    }
}
