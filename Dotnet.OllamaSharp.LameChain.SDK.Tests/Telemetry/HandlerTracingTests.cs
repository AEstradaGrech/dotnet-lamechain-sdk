using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.InferenceHandlers;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Clients;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Request;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Response.GroqProvider;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using Dotnet.OllamaSharp.LameChain.SDK.Tests.Infrastructure;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AIChatResponse = Microsoft.Extensions.AI.ChatResponse;
using AIChatOptions = Microsoft.Extensions.AI.ChatOptions;
using AIChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;
using AIChatRole = Microsoft.Extensions.AI.ChatRole;
using AIContent = Microsoft.Extensions.AI.AIContent;
using AIFunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using AIUsageDetails = Microsoft.Extensions.AI.UsageDetails;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Telemetry
{
    // Langfuse tracing inside the inference handlers: one "generation" activity per LLM call and one
    // tool activity per tool execution, all of them siblings under the agent root (the generation is
    // closed before the recursive tool loop, the tool before the next LLM call).
    [Collection(TelemetryCollection.Name)]
    public class HandlerTracingTests : IDisposable
    {
        private const string Generation = "generation";
        private const string ToolName = nameof(TestToolsService.Echo);

        private readonly ActivityCollector _collector = new();
        private readonly Mock<IServiceProvider> _serviceProvider = new();
        private readonly Mock<IConfiguration> _config = new();
        private readonly Mock<IOllamaApiClient> _ollamaClient = new();
        private readonly Mock<IGroqClient> _groqClient = new();
        private readonly Mock<IClaudeClient> _claudeClient = new();

        public HandlerTracingTests()
        {
            _serviceProvider.Setup(sp => sp.GetService(typeof(IOllamaApiClient))).Returns(_ollamaClient.Object);
            _serviceProvider.Setup(sp => sp.GetService(typeof(IOptions<OllamaSettings>))).Returns(Options.Create(new OllamaSettings { DefaultModel = "test-model" }));
            _serviceProvider.Setup(sp => sp.GetService(typeof(IGroqClient))).Returns(_groqClient.Object);
            _serviceProvider.Setup(sp => sp.GetService(typeof(IOptions<GroqSettings>))).Returns(Options.Create(BaseHandlerTests.BuildTestGroqSettings()));
            _serviceProvider.Setup(sp => sp.GetService(typeof(IClaudeClient))).Returns(_claudeClient.Object);
            _serviceProvider.Setup(sp => sp.GetService(typeof(IOptions<ClaudeSettings>)))
                .Returns(Options.Create(new ClaudeSettings { ApiKey = "test-key", DefaultModel = "claude-sonnet-4-6" }));
            _serviceProvider.Setup(sp => sp.GetService(typeof(IToolsService<TestToolsService>))).Returns(new TestToolsService());
        }

        public void Dispose() => _collector.Dispose();

        #region OllamaHandler

        [Fact]
        public async Task Ollama_GetLlmResponse_UnderAgentRoot_TracesGenerationWithModelInputOutputAndTokens()
        {
            // Arrange
            var handler = new OllamaHandler(_serviceProvider.Object, _config.Object);
            _ollamaClient.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>())).Returns(Stream(
                new ChatResponseStream { Message = new Message(ChatRole.Assistant, "Hello ") },
                DoneChunk("World", promptTokens: 12, outputTokens: 4)));

            // Act
            using (ActivityCollector.StartRoot())
                await handler.GetLlmResponse(UserRequest("llama3"));

            // Assert
            var generation = _collector.Single(Generation);
            generation.ParentSpanId.Should().Be(_collector.Root.SpanId);
            generation.Kind.Should().Be(ActivityKind.Client);
            generation.Tag("langfuse.observation.type").Should().Be("generation");
            generation.Tag("gen_ai.request.model").Should().Be("llama3");
            generation.StringTag("langfuse.observation.input").Should().Contain("what is the weather in Kingston?");
            generation.Tag("langfuse.observation.output").Should().Be("Hello World");
            generation.Tag("gen_ai.usage.input_tokens").Should().Be(12);
            generation.Tag("gen_ai.usage.output_tokens").Should().Be(4);
        }

        [Fact]
        public async Task Ollama_GetLlmResponse_WithToolCall_TracesGenerationToolAndGenerationAsSiblingsOfTheRoot()
        {
            // Arrange
            var handler = new OllamaHandler(_serviceProvider.Object, _config.Object);
            _ollamaClient.SetupSequence(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(Stream(new ChatResponseStream { Message = ToolCallMessage() }, DoneChunk(string.Empty, promptTokens: 20, outputTokens: 8)))
                .Returns(Stream(DoneChunk("final answer", promptTokens: 30, outputTokens: 6)));

            // Act
            string result;
            using (ActivityCollector.StartRoot())
                result = await handler.GetLlmResponse(UserRequest("llama3"), EchoTools());

            // Assert
            result.Should().Be("final answer");
            AssertGenerationToolGenerationSiblings();

            var (first, second) = Generations();
            first.StringTag("langfuse.observation.output").Should().Contain(ToolName);
            first.Tag("gen_ai.usage.input_tokens").Should().Be(20);
            first.Tag("gen_ai.usage.output_tokens").Should().Be(8);
            second.Tag("langfuse.observation.output").Should().Be("final answer");
            second.Tag("gen_ai.usage.input_tokens").Should().Be(30);
            // The second generation sees the tool turn in its input (the loop appended it to the history).
            second.StringTag("langfuse.observation.input").Should().Contain(ToolName);
        }

        [Fact(Skip = "SDK bug: OllamaHandler only reads PromptEvalCount/EvalCount in the non-tool-call branch, so when the tool call arrives in the final (done) chunk the generation is traced with 0 tokens.")]
        public async Task Ollama_GetLlmResponse_WithToolCallInsideTheDoneChunk_KeepsTheGenerationTokens()
        {
            // Arrange
            var handler = new OllamaHandler(_serviceProvider.Object, _config.Object);
            var doneWithToolCall = DoneChunk(string.Empty, promptTokens: 20, outputTokens: 8);
            doneWithToolCall.Message = ToolCallMessage();
            _ollamaClient.SetupSequence(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(Stream(doneWithToolCall))
                .Returns(Stream(DoneChunk("final answer", promptTokens: 30, outputTokens: 6)));

            // Act
            using (ActivityCollector.StartRoot())
                await handler.GetLlmResponse(UserRequest("llama3"), EchoTools());

            // Assert
            Generations().First.Tag("gen_ai.usage.input_tokens").Should().Be(20);
        }

        [Fact]
        public async Task Ollama_GetLlmResponse_WithTracingDisabled_CreatesNoActivities()
        {
            // Arrange: the collector resets LameTelemetry, so IsEnabled is false and there is no root.
            var handler = new OllamaHandler(_serviceProvider.Object, _config.Object);
            _ollamaClient.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>())).Returns(Stream(DoneChunk("answer", 1, 1)));

            // Act
            var result = await handler.GetLlmResponse(UserRequest("llama3"));

            // Assert
            result.Should().Be("answer");
            _collector.Stopped.Should().BeEmpty();
        }

        #endregion

        #region GroqHandler

        [Fact]
        public async Task Groq_GetLlmResponse_WithToolCall_TracesGenerationToolAndGenerationAsSiblingsOfTheRoot()
        {
            // Arrange
            var handler = new GroqHandler(_serviceProvider.Object, _config.Object);
            _groqClient.SetupSequence(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>()))
                .ReturnsAsync(GroqCompletion(ToolCallMessage(), promptTokens: 15, completionTokens: 5))
                .ReturnsAsync(GroqCompletion(new Message(ChatRole.Assistant, "final answer"), promptTokens: 25, completionTokens: 7));

            // Act
            string result;
            using (ActivityCollector.StartRoot())
                result = await handler.GetLlmResponse(UserRequest("test-model"), EchoTools());

            // Assert
            result.Should().Be("final answer");
            AssertGenerationToolGenerationSiblings();

            var first = Generations().First;
            first.Tag("langfuse.observation.output").Should().Be(ToolName);
            first.Tag("gen_ai.usage.input_tokens").Should().Be(15);
            first.Tag("gen_ai.usage.output_tokens").Should().Be(5);
        }

        [Fact(Skip = "SDK gap: GroqHandler only calls TraceGenerationResponse in the tool-call branch, so a plain answer is traced without output nor tokens.")]
        public async Task Groq_GetLlmResponse_WithPlainAnswer_TracesOutputAndTokens()
        {
            // Arrange
            var handler = new GroqHandler(_serviceProvider.Object, _config.Object);
            _groqClient.Setup(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>()))
                .ReturnsAsync(GroqCompletion(new Message(ChatRole.Assistant, "answer"), promptTokens: 9, completionTokens: 2));

            // Act
            using (ActivityCollector.StartRoot())
                await handler.GetLlmResponse(UserRequest("test-model"));

            // Assert
            var generation = _collector.Single(Generation);
            generation.Tag("langfuse.observation.output").Should().Be("answer");
            generation.Tag("gen_ai.usage.input_tokens").Should().Be(9);
        }

        [Fact(Skip = "SDK bug: GroqHandler reads response.Usage without a null check in the traced tool-call branch, so with tracing on a completion without usage throws NullReferenceException.")]
        public async Task Groq_GetLlmResponse_WithToolCallAndNoUsageWhileTracing_DoesNotThrow()
        {
            // Arrange
            var handler = new GroqHandler(_serviceProvider.Object, _config.Object);
            _groqClient.SetupSequence(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>()))
                .ReturnsAsync(new GroqChatCompletion { Choices = new List<GroqCompletionChoice> { new GroqCompletionChoice { Message = ToolCallMessage() } } })
                .ReturnsAsync(new GroqChatCompletion { Choices = new List<GroqCompletionChoice> { new GroqCompletionChoice { Message = new Message(ChatRole.Assistant, "final answer") } } });

            // Act
            string result;
            using (ActivityCollector.StartRoot())
                result = await handler.GetLlmResponse(UserRequest("test-model"), EchoTools());

            // Assert
            result.Should().Be("final answer");
        }

        #endregion

        #region ClaudeHandler

        [Fact]
        public async Task Claude_GetLlmResponse_UnderAgentRoot_TracesGenerationWithOutputAndTokens()
        {
            // Arrange
            var handler = new ClaudeHandler(_serviceProvider.Object, _config.Object);
            _claudeClient.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<AIChatMessage>>(), It.IsAny<AIChatOptions>()))
                .ReturnsAsync(ClaudeResponse(new AIChatMessage(AIChatRole.Assistant, "Hello answer"), AIChatFinishReason.Stop, inputTokens: 7, outputTokens: 3));

            // Act
            using (ActivityCollector.StartRoot())
                await handler.GetLlmResponse(UserRequest("claude-sonnet-4-6"));

            // Assert
            var generation = _collector.Single(Generation);
            generation.ParentSpanId.Should().Be(_collector.Root.SpanId);
            generation.Tag("gen_ai.request.model").Should().Be("claude-sonnet-4-6");
            generation.Tag("langfuse.observation.output").Should().Be("Hello answer");
            generation.Tag("gen_ai.usage.input_tokens").Should().Be(7);
            generation.Tag("gen_ai.usage.output_tokens").Should().Be(3);
        }

        [Fact]
        public async Task Claude_GetLlmResponse_WithToolCall_TracesGenerationToolAndGenerationAsSiblingsOfTheRoot()
        {
            // Arrange
            var handler = new ClaudeHandler(_serviceProvider.Object, _config.Object);
            var toolCall = new AIFunctionCallContent("call_1", ToolName, new Dictionary<string, object?> { ["input"] = JsonSerializer.SerializeToElement("hello") });
            _claudeClient.SetupSequence(c => c.GetResponseAsync(It.IsAny<IEnumerable<AIChatMessage>>(), It.IsAny<AIChatOptions>()))
                .ReturnsAsync(ClaudeResponse(new AIChatMessage(AIChatRole.Assistant, new List<AIContent> { toolCall }), AIChatFinishReason.ToolCalls, inputTokens: 11, outputTokens: 4))
                .ReturnsAsync(ClaudeResponse(new AIChatMessage(AIChatRole.Assistant, "final answer"), AIChatFinishReason.Stop, inputTokens: 21, outputTokens: 5));

            // Act
            string result;
            using (ActivityCollector.StartRoot())
                result = await handler.GetLlmResponse(UserRequest("claude-sonnet-4-6"), EchoTools());

            // Assert
            result.Should().Be("final answer");
            AssertGenerationToolGenerationSiblings();

            var (first, second) = Generations();
            first.Tag("langfuse.observation.output").Should().Be(ToolName);
            first.Tag("gen_ai.usage.input_tokens").Should().Be(11);
            second.Tag("langfuse.observation.output").Should().Be("final answer");
            second.Tag("gen_ai.usage.output_tokens").Should().Be(5);
        }

        #endregion

        #region BaseHandler.getToolResult

        [Fact]
        public async Task GetToolResult_UnderAgentRoot_TracesToolWithArgumentsAndResult()
        {
            // Arrange
            var handler = new TestBaseHandler(_serviceProvider.Object, _config.Object, "test");
            var binding = new ToolBinding(typeof(TestToolsService).GetMethod(nameof(TestToolsService.EchoAsync))!, new TestToolsService());
            var arguments = new Dictionary<string, object> { ["input"] = JsonSerializer.SerializeToElement("hello") };

            // Act
            using (ActivityCollector.StartRoot())
                await handler.InvokeGetToolResult(binding, arguments);

            // Assert
            var tool = _collector.Single(nameof(TestToolsService.EchoAsync));
            tool.ParentSpanId.Should().Be(_collector.Root.SpanId);
            tool.Tag("langfuse.observation.type").Should().Be("tool");
            tool.StringTag("langfuse.observation.input").Should().Contain("\"input\"").And.Contain("hello");
            tool.Tag("langfuse.observation.output").Should().Be(JsonSerializer.Serialize("hello"));
        }

        [Fact]
        public async Task GetToolResult_WithTracingDisabled_CreatesNoActivity()
        {
            // Arrange
            var handler = new TestBaseHandler(_serviceProvider.Object, _config.Object, "test");
            var binding = new ToolBinding(typeof(TestToolsService).GetMethod(nameof(TestToolsService.Echo))!, new TestToolsService());

            // Act
            var result = await handler.InvokeGetToolResult(binding, new Dictionary<string, object> { ["input"] = JsonSerializer.SerializeToElement("hello") });

            // Assert
            result.Should().Be("hello");
            _collector.Stopped.Should().BeEmpty();
        }

        #endregion

        #region Helper Methods

        // generation -> tool -> generation, each one closed before the next starts, all children of the root.
        private void AssertGenerationToolGenerationSiblings()
        {
            _collector.StoppedNames.Should().Equal(Generation, ToolName, Generation, ActivityCollector.RootName);

            var rootSpan = _collector.Root.SpanId;
            _collector.Stopped.Where(a => a.OperationName != ActivityCollector.RootName)
                .Should().OnlyContain(a => a.ParentSpanId == rootSpan && a.TraceId == _collector.Root.TraceId);

            var tool = _collector.Single(ToolName);
            tool.Tag("langfuse.observation.type").Should().Be("tool");
            tool.StringTag("langfuse.observation.input").Should().Contain("hello");
        }

        private (Activity First, Activity Second) Generations()
        {
            var generations = _collector.Named(Generation);
            return (generations[0], generations.Count > 1 ? generations[1] : null!);
        }

        private static ChatRequest UserRequest(string model)
            => new ChatRequest { Model = model, Messages = new List<Message> { new Message(ChatRole.User, "what is the weather in Kingston?") }, Tools = new List<object>() };

        private static Dictionary<string, ToolBinding> EchoTools()
            => new Dictionary<string, ToolBinding> { [ToolName] = new ToolBinding(typeof(TestToolsService).GetMethod(nameof(TestToolsService.Echo))!) };

        private static Message ToolCallMessage()
            => new Message(ChatRole.Assistant, string.Empty)
            {
                ToolCalls = new List<Message.ToolCall>
                {
                    new Message.ToolCall
                    {
                        Id = "call_1",
                        Function = new Message.Function { Name = ToolName, Arguments = new Dictionary<string, object?> { ["input"] = JsonSerializer.SerializeToElement("hello") } }
                    }
                }
            };

        private static ChatDoneResponseStream DoneChunk(string content, int promptTokens, int outputTokens)
            => new ChatDoneResponseStream { Message = new Message(ChatRole.Assistant, content), Done = true, PromptEvalCount = promptTokens, EvalCount = outputTokens };

        private static GroqChatCompletion GroqCompletion(Message message, int promptTokens, int completionTokens)
            => new GroqChatCompletion
            {
                Choices = new List<GroqCompletionChoice> { new GroqCompletionChoice { Message = message } },
                Usage = new GroqCompletionUsage { PromptTokens = promptTokens, CompletionTokens = completionTokens, TotalTokens = promptTokens + completionTokens }
            };

        private static AIChatResponse ClaudeResponse(AIChatMessage message, AIChatFinishReason finishReason, long inputTokens, long outputTokens)
            => new AIChatResponse(message) { FinishReason = finishReason, Usage = new AIUsageDetails { InputTokenCount = inputTokens, OutputTokenCount = outputTokens } };

        private static IAsyncEnumerable<ChatResponseStream?> Stream(params ChatResponseStream[] parts) => StreamIterator(parts);

        private static async IAsyncEnumerable<ChatResponseStream?> StreamIterator(ChatResponseStream[] parts)
        {
            foreach (var part in parts)
            {
                await Task.Yield();
                yield return part;
            }
        }

        #endregion
    }
}
