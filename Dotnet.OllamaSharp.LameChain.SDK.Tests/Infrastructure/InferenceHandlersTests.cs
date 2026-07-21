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
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using DotnetLlamaSharp.Infrastructure.Services.Inference;
using Microsoft.Extensions.Logging;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AIChatResponse = Microsoft.Extensions.AI.ChatResponse;
using AIChatOptions = Microsoft.Extensions.AI.ChatOptions;
using AIChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;
using AIChatRole = Microsoft.Extensions.AI.ChatRole;
using AIContent = Microsoft.Extensions.AI.AIContent;
using AITextContent = Microsoft.Extensions.AI.TextContent;
using AITextReasoningContent = Microsoft.Extensions.AI.TextReasoningContent;
using AnthropicEffort = Anthropic.Models.Messages.Effort;

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

        // Captured the moment the (overridden) GetLlmResponse runs - this is exactly the window
        // BaseHandler.handleFunctionCall sets _isSolvingTools = true for, right before recursing.
        public bool? IsSolvingToolsDuringGetLlmResponse { get; private set; }

        public TestBaseHandler(IServiceProvider serviceProvider, IConfiguration config, string provider, string cannedResponse = "canned-response", Action<string, string>? notifyAction = null)
            : base(serviceProvider, config, provider, notifyAction)
        {
            _cannedResponse = cannedResponse;
        }

        public override Task<string> GetLlmResponse(ChatRequest request, Dictionary<string, MethodInfo>? requestTools = null)
        {
            GetLlmResponseCallCount++;
            IsSolvingToolsDuringGetLlmResponse = IsSolvingTools;
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
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IOptions<OllamaSettings>))).Returns(Options.Create(new OllamaSettings { DefaultModel = "test-model" }));
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
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IOptions<GroqSettings>))).Returns(Options.Create(BuildTestGroqSettings()));
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
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IOptions<OllamaSettings>))).Returns(Options.Create(new OllamaSettings { DefaultModel = "test-model" }));
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
        public async Task HandleFunctionCall_WithNotifyActionConfigured_InvokesNotifyToolCallWithToolName()
        {
            // Arrange
            var toolsService = new TestToolsService();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IToolsService<TestToolsService>))).Returns(toolsService);
            var notifications = new List<(string Title, string Message)>();
            var handler = new TestBaseHandler(mockServiceProvider.Object, new Mock<IConfiguration>().Object, "test", "recursive-response",
                (title, message) => notifications.Add((title, message)));

            var toolMessage = BuildToolCallMessage("Echo", new Dictionary<string, object?> { ["input"] = JsonSerializer.SerializeToElement("hello") });
            var request = new ChatRequest { Model = "test-model", Messages = new List<Message> { new Message(ChatRole.User, "hi") } };
            var toolsLookup = new Dictionary<string, MethodInfo> { ["Echo"] = typeof(TestToolsService).GetMethod(nameof(TestToolsService.Echo))! };

            // Act
            await handler.InvokeHandleFunctionCall(request, toolMessage, toolsLookup);

            // Assert
            notifications.Should().ContainSingle(n => n.Message == "Echo");
        }

        [Fact]
        public async Task HandleFunctionCall_DuringRecursiveCall_IsSolvingToolsIsTrueThenResetToFalseAfter()
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
            await handler.InvokeHandleFunctionCall(request, toolMessage, toolsLookup);

            // Assert: true while the recursive GetLlmResponse call (post tool-execution) was running,
            // reset to false once handleFunctionCall returns.
            handler.IsSolvingToolsDuringGetLlmResponse.Should().BeTrue();
            handler.IsSolvingTools.Should().BeFalse();
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

        internal static GroqSettings BuildTestGroqSettings()
            => new GroqSettings
            {
                DefaultModel = "test-model",
                ToolModels = new List<string> { "test-model" },
                JsonModels = new List<string> { "test-model" },
                ReasoningModels = new List<string>(),
                Endpoints = new Dictionary<string, string>()
            };

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
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IOptions<OllamaSettings>))).Returns(Options.Create(new OllamaSettings { DefaultModel = "test-model" }));
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

        #region Thinking Notification Tests

        [Fact]
        public async Task GetLlmResponse_WithThinkingContent_NotifiesThinking()
        {
            // Arrange
            var notifications = new List<(string Title, string Message)>();
            var handler = new OllamaHandler(_mockServiceProvider.Object, _mockConfig.Object, (title, message) => notifications.Add((title, message)));
            var parts = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message(ChatRole.Assistant, "answer") { Thinking = "pondering..." } }
            };
            _mockClient.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>())).Returns(GetAsyncEnumerable(parts));
            var request = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") } };

            // Act
            var result = await handler.GetLlmResponse(request);

            // Assert
            result.Should().Be("answer");
            notifications.Should().Contain(n => n.Message == "pondering...");
        }

        [Fact]
        public async Task GetLlmResponse_WithNoThinkingContent_DoesNotNotifyThinking()
        {
            // Arrange
            var notifications = new List<(string Title, string Message)>();
            var handler = new OllamaHandler(_mockServiceProvider.Object, _mockConfig.Object, (title, message) => notifications.Add((title, message)));
            var parts = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message(ChatRole.Assistant, "answer") }
            };
            _mockClient.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>())).Returns(GetAsyncEnumerable(parts));
            var request = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") } };

            // Act
            var result = await handler.GetLlmResponse(request);

            // Assert
            result.Should().Be("answer");
            notifications.Should().NotContain(n => n.Title.Contains("THINKING"));
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
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IOptions<GroqSettings>))).Returns(Options.Create(BaseHandlerTests.BuildTestGroqSettings()));
            _mockConfig = new Mock<IConfiguration>();
        }

        private static void LogAction(string provider, string msg)
            => Console.WriteLine($"MOCK {provider} - {msg}");

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidServiceProvider_ResolvesGroqClient()
        {
            // Act
            var handler = new GroqHandler(_mockServiceProvider.Object, _mockConfig.Object, LogAction);

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

        #region Thinking Notification Tests

        [Fact]
        public async Task GetLlmResponse_WithThinkingContent_NotifiesThinking()
        {
            // Arrange
            var notifications = new List<(string Title, string Message)>();
            var handler = new GroqHandler(_mockServiceProvider.Object, _mockConfig.Object, (title, message) => notifications.Add((title, message)));
            var completion = new GroqChatCompletion
            {
                Choices = new List<GroqCompletionChoice>
                {
                    new GroqCompletionChoice { Message = new Message(ChatRole.Assistant, "answer") { Thinking = "pondering..." } }
                }
            };
            _mockClient.Setup(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>())).ReturnsAsync(completion);
            var request = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") }, Tools = new List<object>() };

            // Act
            var result = await handler.GetLlmResponse(request);

            // Assert
            result.Should().Be("answer");
            notifications.Should().Contain(n => n.Message == "pondering...");
        }

        [Fact]
        public async Task GetLlmResponse_WithNoThinkingContent_DoesNotNotifyThinking()
        {
            // Arrange
            var notifications = new List<(string Title, string Message)>();
            var handler = new GroqHandler(_mockServiceProvider.Object, _mockConfig.Object, (title, message) => notifications.Add((title, message)));
            var completion = new GroqChatCompletion
            {
                Choices = new List<GroqCompletionChoice> { new GroqCompletionChoice { Message = new Message(ChatRole.Assistant, "answer") } }
            };
            _mockClient.Setup(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>())).ReturnsAsync(completion);
            var request = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") }, Tools = new List<object>() };

            // Act
            var result = await handler.GetLlmResponse(request);

            // Assert
            result.Should().Be("answer");
            notifications.Should().NotContain(n => n.Title.Contains("THINKING"));
        }

        #endregion

        #region Structured Output Tests

        [Fact]
        public async Task GetLlmResponse_WithFormatSet_SwapsToJsonModelBeforeSendingRequest()
        {
            // Arrange: GroqHandler forces the model to the configured JsonModels entry whenever
            // structured output is requested (request.Format != null), regardless of the model
            // the caller originally asked for.
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IOptions<GroqSettings>)))
                .Returns(Options.Create(new GroqSettings { JsonModels = new List<string> { "json-capable-model" }, ToolModels = new List<string>(), ReasoningModels = new List<string>(), Endpoints = new Dictionary<string, string>() }));
            var handler = new GroqHandler(_mockServiceProvider.Object, _mockConfig.Object);

            GroqChatRequest? capturedRequest = null;
            var completion = new GroqChatCompletion { Choices = new List<GroqCompletionChoice> { new GroqCompletionChoice { Message = new Message(ChatRole.Assistant, "{}") } } };
            _mockClient.Setup(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>()))
                .Callback<GroqChatRequest>(r => capturedRequest = r)
                .ReturnsAsync(completion);

            var request = new ChatRequest
            {
                Model = "some-other-model",
                Messages = new List<Message> { new Message(ChatRole.User, "hi") },
                Tools = new List<object>(),
                Format = new { type = "object" }
            };

            // Act
            await handler.GetLlmResponse(request);

            // Assert
            capturedRequest.Should().NotBeNull();
            capturedRequest!.Model.Should().Be("json-capable-model");
        }

        [Fact]
        public async Task GetLlmResponse_WithFormatSetAndNoJsonModelsConfigured_SetsModelToNull()
        {
            // Arrange: characterizes existing behavior - if GroqSettings.JsonModels is empty,
            // structured-output requests silently get Model=null (FirstOrDefault on an empty list)
            // rather than falling back to the originally requested model.
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IOptions<GroqSettings>)))
                .Returns(Options.Create(new GroqSettings { JsonModels = new List<string>(), ToolModels = new List<string>(), ReasoningModels = new List<string>(), Endpoints = new Dictionary<string, string>() }));
            var handler = new GroqHandler(_mockServiceProvider.Object, _mockConfig.Object);

            GroqChatRequest? capturedRequest = null;
            var completion = new GroqChatCompletion { Choices = new List<GroqCompletionChoice> { new GroqCompletionChoice { Message = new Message(ChatRole.Assistant, "{}") } } };
            _mockClient.Setup(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>()))
                .Callback<GroqChatRequest>(r => capturedRequest = r)
                .ReturnsAsync(completion);

            var request = new ChatRequest
            {
                Model = "some-model",
                Messages = new List<Message> { new Message(ChatRole.User, "hi") },
                Tools = new List<object>(),
                Format = new { type = "object" }
            };

            // Act
            await handler.GetLlmResponse(request);

            // Assert
            capturedRequest!.Model.Should().BeNull();
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

        #region Reasoning Mapping Tests

        // AsGroqRequest maps ChatRequest.Think (an EReasoning value, passed as a string like "low"/
        // "medium"/"high"/"none") to Groq's IncludeReasoning/ReasoningEffort fields.

        [Fact]
        public void AsGroqRequest_WithThinkUnset_LeavesReasoningFieldsNull()
        {
            // Arrange
            var chatRequest = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") }, Tools = new List<object>() };

            // Act
            var groqRequest = chatRequest.AsGroqRequest();

            // Assert
            groqRequest.IncludeReasoning.Should().BeNull();
            groqRequest.ReasoningEffort.Should().BeNull();
        }

        [Fact]
        public void AsGroqRequest_WithThinkNone_DisablesIncludeReasoningButSendsFalseExplicitly()
        {
            // Arrange: "none" explicitly disables reasoning - IncludeReasoning is sent as `false`
            // (not omitted/null), while ReasoningEffort itself is nulled out.
            var chatRequest = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") }, Tools = new List<object>(), Think = "none" };

            // Act
            var groqRequest = chatRequest.AsGroqRequest();

            // Assert
            groqRequest.IncludeReasoning.Should().BeFalse();
            groqRequest.ReasoningEffort.Should().BeNull();
        }

        [Theory]
        [InlineData("low")]
        [InlineData("medium")]
        [InlineData("high")]
        public void AsGroqRequest_WithReasoningEffortLevel_EnablesIncludeReasoningAndPassesEffort(string think)
        {
            // Arrange
            var chatRequest = new ChatRequest { Messages = new List<Message> { new Message(ChatRole.User, "hi") }, Tools = new List<object>(), Think = think };

            // Act
            var groqRequest = chatRequest.AsGroqRequest();

            // Assert
            groqRequest.IncludeReasoning.Should().BeTrue();
            groqRequest.ReasoningEffort.Should().Be(think);
        }

        #endregion
    }

    // NOTE: scoped to ClaudeHandler's structured-output (json-fence stripping) and thinking-notification
    // behavior, per the "anthropic providers have also changed regarding json output" ask. The recursive
    // tool-call loop (ClaudeHandler.handleFunctionCall / mapRequestTools / OllamaTools.ToAIFunction) is
    // intentionally NOT covered here - it requires much heavier Microsoft.Extensions.AI fixtures
    // (FunctionCallContent round-tripping through ToOllamaMessage/ToChatMessages) than the other
    // provider's tool-loop tests, and is a bigger, separate lift.
    public class ClaudeHandlerTests
    {
        private readonly Mock<IClaudeClient> _mockClient;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<IConfiguration> _mockConfig;

        public ClaudeHandlerTests()
        {
            _mockClient = new Mock<IClaudeClient>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IClaudeClient))).Returns(_mockClient.Object);
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IOptions<ClaudeSettings>)))
                .Returns(Options.Create(new ClaudeSettings { ApiKey = "test-key", DefaultModel = "claude-sonnet-4-6" }));
            _mockConfig = new Mock<IConfiguration>();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidServiceProvider_ResolvesClaudeClient()
        {
            // Act
            var handler = new ClaudeHandler(_mockServiceProvider.Object, _mockConfig.Object);

            // Assert
            handler.Should().NotBeNull();
            handler.IsProvider("anthropic").Should().BeTrue();
        }

        [Fact]
        public void Constructor_WithMissingClaudeClientRegistration_ThrowsInvalidOperationException()
        {
            // Arrange
            var emptyProvider = new Mock<IServiceProvider>();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => new ClaudeHandler(emptyProvider.Object, _mockConfig.Object));
        }

        #endregion

        #region GetLlmResponse Tests

        [Fact]
        public async Task GetLlmResponse_WithPlainTextResponse_ReturnsText()
        {
            // Arrange
            var handler = new ClaudeHandler(_mockServiceProvider.Object, _mockConfig.Object);
            var responseMessage = new AIChatMessage(AIChatRole.Assistant, "Hello answer");
            var response = new AIChatResponse(responseMessage) { FinishReason = AIChatFinishReason.Stop };
            _mockClient.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<AIChatMessage>>(), It.IsAny<AIChatOptions>())).ReturnsAsync(response);
            var request = new ChatRequest { Model = "claude-sonnet-4-6", Messages = new List<Message> { new Message(ChatRole.User, "hi") } };

            // Act
            var result = await handler.GetLlmResponse(request);

            // Assert
            result.Should().Be("Hello answer");
        }

        [Fact]
        public async Task GetLlmResponse_WithEmptyMessages_ThrowsInvalidDataException()
        {
            // Arrange
            var handler = new ClaudeHandler(_mockServiceProvider.Object, _mockConfig.Object);
            var request = new ChatRequest { Model = "claude-sonnet-4-6", Messages = new List<Message>() };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidDataException>(() => handler.GetLlmResponse(request));
        }

        [Fact]
        public async Task GetLlmResponse_WithModelNotInSettingsModels_FallsBackToDefaultModel()
        {
            // Arrange: characterizes existing behavior - a request model that isn't one of the
            // hardcoded Anthropic model ids gets silently swapped to ClaudeSettings.DefaultModel.
            var handler = new ClaudeHandler(_mockServiceProvider.Object, _mockConfig.Object);
            AIChatOptions? capturedOptions = null;
            var responseMessage = new AIChatMessage(AIChatRole.Assistant, "answer");
            var response = new AIChatResponse(responseMessage) { FinishReason = AIChatFinishReason.Stop };
            _mockClient.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<AIChatMessage>>(), It.IsAny<AIChatOptions>()))
                .Callback<IEnumerable<AIChatMessage>, AIChatOptions>((_, options) => capturedOptions = options)
                .ReturnsAsync(response);
            var request = new ChatRequest { Model = "not-a-real-model", Messages = new List<Message> { new Message(ChatRole.User, "hi") } };

            // Act
            await handler.GetLlmResponse(request);

            // Assert
            capturedOptions!.ModelId.Should().Be("claude-sonnet-4-6");
        }

        #endregion

        #region Thinking Notification Tests

        [Fact]
        public async Task GetLlmResponse_WithReasoningContent_NotifiesThinking()
        {
            // Arrange
            var notifications = new List<(string Title, string Message)>();
            var handler = new ClaudeHandler(_mockServiceProvider.Object, _mockConfig.Object, (title, message) => notifications.Add((title, message)));
            var responseMessage = new AIChatMessage(AIChatRole.Assistant, new List<AIContent> { new AITextReasoningContent("pondering..."), new AITextContent("final answer") });
            var response = new AIChatResponse(responseMessage) { FinishReason = AIChatFinishReason.Stop };
            _mockClient.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<AIChatMessage>>(), It.IsAny<AIChatOptions>())).ReturnsAsync(response);
            var request = new ChatRequest { Model = "claude-sonnet-4-6", Messages = new List<Message> { new Message(ChatRole.User, "hi") } };

            // Act
            var result = await handler.GetLlmResponse(request);

            // Assert
            result.Should().Be("final answer");
            notifications.Should().Contain(n => n.Message == "pondering...");
        }

        #endregion

        #region Structured Output Tests

        [Fact]
        public async Task GetLlmResponse_WithFormatSetAndFencedJsonResponse_StripsCodeFences()
        {
            // Arrange
            var handler = new ClaudeHandler(_mockServiceProvider.Object, _mockConfig.Object);
            var responseMessage = new AIChatMessage(AIChatRole.Assistant, "```json\n{\"foo\":1}\n```");
            var response = new AIChatResponse(responseMessage) { FinishReason = AIChatFinishReason.Stop };
            _mockClient.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<AIChatMessage>>(), It.IsAny<AIChatOptions>())).ReturnsAsync(response);
            var request = new ChatRequest { Model = "claude-sonnet-4-6", Messages = new List<Message> { new Message(ChatRole.User, "hi") }, Format = new { type = "object" } };

            // Act
            var result = await handler.GetLlmResponse(request);

            // Assert
            result.Should().Be("{\"foo\":1}");
        }

        [Fact]
        public async Task GetLlmResponse_WithFormatSetButResponseNotFenced_ReturnsResponseUnchanged()
        {
            // Arrange: characterizes existing behavior - the code-fence stripping (and its Trim())
            // only runs when the response actually starts with "```json"; a structured-output
            // response that happens not to be fenced is returned completely raw, whitespace included.
            var handler = new ClaudeHandler(_mockServiceProvider.Object, _mockConfig.Object);
            var responseMessage = new AIChatMessage(AIChatRole.Assistant, "  {\"foo\":1}  ");
            var response = new AIChatResponse(responseMessage) { FinishReason = AIChatFinishReason.Stop };
            _mockClient.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<AIChatMessage>>(), It.IsAny<AIChatOptions>())).ReturnsAsync(response);
            var request = new ChatRequest { Model = "claude-sonnet-4-6", Messages = new List<Message> { new Message(ChatRole.User, "hi") }, Format = new { type = "object" } };

            // Act
            var result = await handler.GetLlmResponse(request);

            // Assert
            result.Should().Be("  {\"foo\":1}  ");
        }

        #endregion

        #region Reasoning Mapping Tests

        // These test OllamaRequestExtensions.ToClaudeChatClientRequest/ToAnthropicEffort directly -
        // pure mapping functions ClaudeHandler.GetLlmResponse depends on to turn ChatRequest.Think
        // (an EReasoning value passed as a string like "low"/"medium"/"high"/"none") into an
        // Anthropic reasoning effort.

        [Theory]
        [InlineData("low", EReasoningMinTokens.Low)]
        [InlineData("medium", EReasoningMinTokens.Medium)]
        [InlineData("high", EReasoningMinTokens.High)]
        public void ToClaudeChatClientRequest_WithReasoningEffortLevel_BumpsMaxOutputTokensAndNullsTopKTopP(string think, EReasoningMinTokens expectedMinTokens)
        {
            // Arrange
            var request = new ChatRequest
            {
                Model = "claude-sonnet-4-6",
                Messages = new List<Message> { new Message(ChatRole.User, "hi") },
                Think = think,
                Options = new RequestOptions { NumPredict = 100, TopK = 10, TopP = 0.9f }
            };

            // Act
            var options = request.ToClaudeChatClientRequest(tools: null);

            // Assert
            options.TopK.Should().BeNull();
            options.TopP.Should().BeNull();
            options.MaxOutputTokens.Should().Be((int)expectedMinTokens + 100);
        }

        [Fact]
        public void ToClaudeChatClientRequest_WithThinkNone_DoesNotAdjustTokensOrTopKTopP()
        {
            // Arrange
            var request = new ChatRequest
            {
                Model = "claude-sonnet-4-6",
                Messages = new List<Message> { new Message(ChatRole.User, "hi") },
                Think = "none",
                Options = new RequestOptions { NumPredict = 100, TopK = 10, TopP = 0.9f }
            };

            // Act
            var options = request.ToClaudeChatClientRequest(tools: null);

            // Assert
            options.TopK.Should().Be(10);
            options.TopP.Should().Be(0.9f);
            options.MaxOutputTokens.Should().Be(100);
        }

        [Theory]
        [InlineData("low", AnthropicEffort.Low)]
        [InlineData("medium", AnthropicEffort.Medium)]
        [InlineData("high", AnthropicEffort.High)]
        public void ToAnthropicEffort_WithReasoningLevel_MapsToEffortEnum(string think, AnthropicEffort expected)
        {
            // Arrange
            ThinkValue? value = think;

            // Act & Assert
            value.ToAnthropicEffort().Should().Be(expected);
        }

        [Fact]
        public void ToAnthropicEffort_WithThinkNone_ReturnsNull()
        {
            // Arrange
            ThinkValue? value = "none";

            // Act & Assert
            value.ToAnthropicEffort().Should().BeNull();
        }

        [Fact]
        public void ToAnthropicEffort_WithThinkUnset_ReturnsNull()
        {
            // Arrange
            ThinkValue? value = null;

            // Act & Assert
            value.ToAnthropicEffort().Should().BeNull();
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
        private readonly Mock<ILogger<OllamaInferenceService>> _mockLogger;
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
            _mockLogger = new Mock<ILogger<OllamaInferenceService>>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IOllamaApiClient))).Returns(_mockOllamaClient.Object);
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IGroqClient))).Returns(_mockGroqClient.Object);
            // Both concrete handler ctors now resolve their settings via BaseHandler.tryGetConfig<T>,
            // which requires IOptions<T> to be resolvable from the service provider (or it falls back
            // to IConfiguration.GetSection, which a bare mock can't satisfy).
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IOptions<OllamaSettings>))).Returns(_mockOptions.Object);
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IOptions<GroqSettings>))).Returns(Options.Create(BaseHandlerTests.BuildTestGroqSettings()));
            // Deliberately NOT registering IClaudeClient - ClaudeHandler is out of scope, and any
            // accidental attempt to resolve it should fail loudly rather than silently succeed.

            _service = new OllamaInferenceService(_mockOllamaClient.Object, _mockConfig.Object, _mockServiceProvider.Object, _mockOptions.Object, _mockLogger.Object);
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
        public async Task GeneratePrompt_WithNonOllamaProvider_SwapsHandlerAndRoutesThroughChatConversion()
        {
            // Arrange: GeneratePrompt now branches on a literal `provider != "ollama"` check (fixed -
            // it used to branch on "is the cached handler currently an OllamaHandler", which meant a
            // freshly constructed service ignored the requested provider entirely). Now requesting a
            // non-ollama provider correctly swaps the handler and routes through GenerateRequestToChat
            // + GetLlmResponse instead of the native Ollama generate endpoint.
            var request = new GenerateRequest { Prompt = "test prompt" };
            var completion = new GroqChatCompletion { Choices = new List<GroqCompletionChoice> { new GroqCompletionChoice { Message = new Message(ChatRole.Assistant, "groq answer") } } };
            _mockGroqClient.Setup(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>())).ReturnsAsync(completion);

            // Act
            var result = await _service.GeneratePrompt(request, "groq");

            // Assert
            result.Content.Should().Be("groq answer");
            _mockGroqClient.Verify(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>()), Times.Once);
            _mockOllamaClient.Verify(c => c.GenerateAsync(It.IsAny<GenerateRequest>()), Times.Never);
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
