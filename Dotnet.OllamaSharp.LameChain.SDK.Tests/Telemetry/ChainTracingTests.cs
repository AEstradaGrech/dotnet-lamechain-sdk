using System.Diagnostics;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using DotnetLlamaSharp.Infrastructure.Services.Inference;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Telemetry;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Steps;
using static Dotnet.OllamaSharp.LameChain.SDK.Extensions.LameChain;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Telemetry
{
    // SingleThrowStep.ExecuteChainAsync with a TraceConfig: one root activity per chain execution and every
    // LLM call of every step (MessagePromptCommand -> OllamaInferenceService -> OllamaHandler.GenerateLlmResponse)
    // traced under it. Only the Ollama HTTP client is mocked.
    [Collection(TelemetryCollection.Name)]
    public class ChainTracingTests : IDisposable
    {
        private const string Intent = "geography";
        private const string FirstPrompt = "What is the capital of Jamaica?";
        private const string Generation = "generation";

        private readonly ActivityCollector _collector = new();
        private readonly Mock<IOllamaApiClient> _client = new();
        private readonly OllamaInferenceService _inference;
        private readonly Queue<string> _answers = new();

        // LameTelemetry.IsTracing seen by each LLM call (inside the handler), in call order.
        private readonly List<bool> _enabledPerCall = new();

        public ChainTracingTests()
        {
            var options = Options.Create(new OllamaSettings { DefaultModel = "test-model" });
            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider.Setup(sp => sp.GetService(typeof(IOllamaApiClient))).Returns(_client.Object);
            serviceProvider.Setup(sp => sp.GetService(typeof(IOptions<OllamaSettings>))).Returns(options);
            _inference = new OllamaInferenceService(_client.Object, new Mock<IConfiguration>().Object, serviceProvider.Object, options, NullLogger<OllamaInferenceService>.Instance);

            _client.Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns((GenerateRequest _, CancellationToken _) =>
                {
                    _enabledPerCall.Add(LameTelemetry.IsTracing);

                    if (_answers.Count == 0)
                        throw new InvalidOperationException($"No scripted Ollama answer left for call #{_enabledPerCall.Count}");

                    return Answer(_answers.Dequeue());
                });
        }

        public void Dispose() => _collector.Dispose();

        // StartWith("What is the capital of Jamaica?") -> Then("Translate the answer to Spanish")
        private SingleThrowStep TwoStepChain(string firstAnswer = "Kingston", string secondAnswer = "Kingston (es)")
        {
            _answers.Enqueue(firstAnswer);
            _answers.Enqueue(secondAnswer);

            var first = StartWith(new StepSettings(new MessagePromptCommand(_inference, "Answer the question"), new PromptCommandRequest(FirstPrompt)),
                new CommandSettings("llama3"), null, Intent);
            first.Then(new StepSettings(new MessagePromptCommand(_inference, "Translate the previous answer"), new PromptCommandRequest("Translate the answer to Spanish")));

            return first;
        }

        private Activity ChainRoot() => _collector.Stopped.Single(a => a.ParentSpanId == default && a.OperationName.StartsWith($"{Intent}_"));

        #region Trace Tree Tests

        [Fact]
        public async Task ExecuteChainAsync_WithTraceConfig_TracesEveryStepLlmCallUnderTheChainRoot()
        {
            // Arrange
            var chain = TwoStepChain();

            // Act
            var result = await chain.ExecuteChainAsync(traceConfig: new TraceConfig());

            // Assert
            result.Should().NotBeNull();
            var root = ChainRoot();
            root.Kind.Should().Be(ActivityKind.Internal);
            _collector.StoppedNames.Should().Equal(Generation, Generation, root.OperationName);

            var generations = _collector.Named(Generation);
            generations.Should().OnlyContain(g => g.ParentSpanId == root.SpanId && g.TraceId == root.TraceId);
            generations[0].StringTag("langfuse.observation.input").Should().Contain(FirstPrompt);
            generations[0].Tag("langfuse.observation.output").Should().Be("Kingston");
            generations[1].Tag("langfuse.observation.output").Should().Be("Kingston (es)");
        }

        [Fact]
        public async Task ExecuteChainAsync_WithoutTraceConfig_CreatesNoActivities()
        {
            // Arrange
            var chain = TwoStepChain();

            // Act
            await chain.ExecuteChainAsync();

            // Assert
            _collector.Stopped.Should().BeEmpty();
            _enabledPerCall.Should().Equal(false, false);
        }

        [Fact]
        public async Task ExecuteChainAsync_WithTraceConfig_TracesOnlyInsideTheRunAndUnregistersTheRootWhenItEnds()
        {
            // Arrange
            var chain = TwoStepChain();

            // Act
            await chain.ExecuteChainAsync(traceConfig: new TraceConfig());

            // Assert
            _enabledPerCall.Should().Equal(true, true);
            LameTelemetry.IsTracing.Should().BeFalse();
            LameTelemetry.TryStopRootActivity(ChainRoot().Id!).Should().BeFalse();
        }

        [Fact]
        public async Task ExecuteChainAsync_InsideAnUnrecordedAspNetCoreRequestActivity_StillStartsANewRootTrace()
        {
            // Arrange
            var chain = TwoStepChain();
            using var requestActivity = new Activity("Microsoft.AspNetCore.Hosting.HttpRequestIn").Start();

            // Act
            await chain.ExecuteChainAsync(traceConfig: new TraceConfig());

            // Assert
            var root = ChainRoot();
            root.TraceId.Should().NotBe(requestActivity.TraceId);
            _collector.Named(Generation).Should().HaveCount(2);
            Activity.Current.Should().BeSameAs(requestActivity);
        }

        [Fact]
        public async Task ExecuteChainAsync_ExecutedTwice_OpensOneRootPerExecution()
        {
            // Arrange
            var firstRun = TwoStepChain();
            var secondRun = TwoStepChain();

            // Act
            await firstRun.ExecuteChainAsync(traceConfig: new TraceConfig("geo-chain"));
            await secondRun.ExecuteChainAsync(traceConfig: new TraceConfig("geo-chain"));

            // Assert
            var roots = _collector.Stopped.Where(a => a.ParentSpanId == default).ToList();
            roots.Should().HaveCount(2);
            roots.Select(r => r.TraceId).Should().OnlyHaveUniqueItems();
            _collector.Named(Generation).Should().HaveCount(4);
        }

        [Fact]
        public async Task ExecuteChainAsync_WhenAStepThrows_StopsAndUnregistersTheRoot()
        {
            // Arrange: no scripted answers -> the first LLM call throws.
            var chain = StartWith(new StepSettings(new MessagePromptCommand(_inference, "Answer the question"), new PromptCommandRequest(FirstPrompt)),
                new CommandSettings("llama3"), null, Intent);
            chain.Then(new StepSettings(new MessagePromptCommand(_inference, "Translate"), new PromptCommandRequest("Translate")));

            // Act
            var run = () => chain.ExecuteChainAsync(traceConfig: new TraceConfig());

            // Assert
            await run.Should().ThrowAsync<InvalidOperationException>();
            LameTelemetry.TryStopRootActivity(ChainRoot().Id!).Should().BeFalse();
            LameTelemetry.IsTracing.Should().BeFalse();
        }

        #endregion

        #region Trace Config Tests

        [Fact]
        public async Task ExecuteChainAsync_WithEmptyTraceConfig_FillsTheDefaultsAndTheChainData()
        {
            // Arrange
            var chain = TwoStepChain();
            var config = new TraceConfig();

            // Act
            await chain.ExecuteChainAsync(traceConfig: config);

            // Assert
            var root = ChainRoot();
            config.TraceName.Should().Be($"ChainRunner_{chain.Id}");
            config.SessionId.Should().Be(root.OperationName);
            Guid.TryParse(config.UserId, out _).Should().BeTrue();
            config.Tags.Should().Contain([nameof(SingleThrowStep), Intent]);
            config.Metadata.Should().Contain(new Dictionary<string, string>
            {
                ["intent"] = Intent,
                ["userprompt"] = FirstPrompt,
                ["provider"] = "ollama"
            });
        }

        [Fact]
        public async Task ExecuteChainAsync_WithTraceConfig_WritesItToTheChainRoot()
        {
            // Arrange
            var chain = TwoStepChain();

            // Act
            await chain.ExecuteChainAsync(traceConfig: new TraceConfig("geo-chain", "thread-1", "user-1", tags: ["sample"]));

            // Assert
            var root = ChainRoot();
            root.Tag("langfuse.trace.name").Should().Be("geo-chain");
            root.Tag("langfuse.session.id").Should().Be("thread-1");
            root.Tag("langfuse.user.id").Should().Be("user-1");
            root.Tag("langfuse.trace.tags").Should().BeAssignableTo<IEnumerable<string>>().Which.Should().Contain(["sample", nameof(SingleThrowStep), Intent]);
            root.Tag("langfuse.trace.metadata.intent").Should().Be(Intent);
            root.Tag("langfuse.trace.metadata.userprompt").Should().Be(FirstPrompt);
        }

        #endregion

        #region Helper Methods

        private static async IAsyncEnumerable<GenerateResponseStream?> Answer(string text)
        {
            await Task.Yield();
            yield return new GenerateResponseStream { Response = text };
        }

        #endregion
    }
}
