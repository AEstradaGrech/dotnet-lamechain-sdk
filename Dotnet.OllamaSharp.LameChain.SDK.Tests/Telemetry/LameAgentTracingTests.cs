using System.Diagnostics;
using FluentAssertions;
using Moq;
using OllamaSharp.Models.Chat;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Telemetry;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Agents;
using Dotnet.OllamaSharp.LameChain.SDK.Tests.Agents;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Telemetry
{
    // LameAgent.WithLangfuseTraces + the root activity opened by RunPrompt. Tracing is decided per async flow
    // (LameTelemetry.IsTracing: is there a LameChain activity in Activity.Current?), and the root is registered
    // in LameTelemetry while the agent runs and removed when it finishes, whatever the outcome.
    [Collection(TelemetryCollection.Name)]
    public class LameAgentTracingTests : IDisposable
    {
        private const string AgentName = "Assistant";
        private const string AgentDescription = "A helpful assistant";

        private readonly ActivityCollector _collector = new();
        private readonly AgentTestHost _host = new AgentTestHost().WithDefaultSkills();

        public void Dispose()
        {
            _host.Dispose();
            _collector.Dispose();
        }

        private LameAgent CreateAgent(string? name = AgentName, string? description = AgentDescription)
            => new LameAgent(_host.Services, "llama3", name: name, description: description);

        // Agent with an empty TraceConfig: its root activity is named after the agent name + id.
        private (LameAgent Agent, TraceConfig Config) CreateTracedAgent(string? name = AgentName, TraceConfig? config = null)
        {
            config ??= new TraceConfig();
            var agent = CreateAgent(name).WithLangfuseTraces(config);
            return (agent, config);
        }

        #region WithLangfuseTraces Tests

        [Fact]
        public void WithLangfuseTraces_WithEmptyConfig_DefaultsTraceNameToAgentNameAndIdAndSessionToAgentId()
        {
            // Arrange
            var agent = CreateAgent();
            var config = new TraceConfig();

            // Act
            agent.WithLangfuseTraces(config);

            // Assert
            config.TraceName.Should().Be($"{AgentName}_{agent.Id}");
            config.SessionId.Should().Be(agent.Id.ToString());
            Guid.TryParse(config.UserId, out _).Should().BeTrue();
        }

        [Fact]
        public void WithLangfuseTraces_WithEmptyConfigAndUnnamedAgent_DefaultsTraceNameFromAgentId()
        {
            // Arrange
            var agent = CreateAgent(name: null);
            var config = new TraceConfig();

            // Act
            agent.WithLangfuseTraces(config);

            // Assert
            config.TraceName.Should().Be($"LameAgent_{agent.Id}");
        }

        [Fact]
        public void WithLangfuseTraces_WithProvidedValues_KeepsThem()
        {
            // Arrange
            var agent = CreateAgent();
            var config = new TraceConfig("my-trace", "chat-thread-42", "user-7");

            // Act
            agent.WithLangfuseTraces(config);

            // Assert
            config.TraceName.Should().Be("my-trace");
            config.SessionId.Should().Be("chat-thread-42");
            config.UserId.Should().Be("user-7");
        }

        [Fact]
        public void WithLangfuseTraces_OnlyConfiguresTheAgent_NothingIsTracedUntilItRuns()
        {
            // Act
            CreateTracedAgent();

            // Assert
            LameTelemetry.IsTracing.Should().BeFalse();
            _collector.Stopped.Should().BeEmpty();
        }

        #endregion

        #region RunPrompt Root Activity Tests

        [Fact]
        public async Task RunPrompt_WithTraces_OpensTheRootActivityForTheInferenceCallAndClosesIt()
        {
            // Arrange
            Activity? currentDuringInference = null;
            _host.Inference
                .Setup(s => s.ChatPrompt(It.IsAny<ChatRequest>(), It.IsAny<string>(), It.IsAny<Dictionary<string, ToolBinding>?>()))
                .Callback(() => currentDuringInference = Activity.Current)
                .ReturnsAsync(new Message(ChatRole.Assistant, "llm answer"));
            var (agent, config) = CreateTracedAgent();

            // Act
            await agent.RunPrompt("hello");

            // Assert
            var root = _collector.Single(config.TraceName!);
            root.Kind.Should().Be(ActivityKind.Internal);
            root.ParentSpanId.Should().Be(default(ActivitySpanId));
            // Everything below the agent (inference service, handler, tools) hangs from this activity.
            currentDuringInference.Should().BeSameAs(root);
        }

        [Fact]
        public async Task RunPrompt_WithTraces_TracesOnlyInsideTheRunAndUnregistersTheRootWhenItEnds()
        {
            // Arrange
            bool? tracingDuringInference = null;
            _host.Inference
                .Setup(s => s.ChatPrompt(It.IsAny<ChatRequest>(), It.IsAny<string>(), It.IsAny<Dictionary<string, ToolBinding>?>()))
                .Callback(() => tracingDuringInference = LameTelemetry.IsTracing)
                .ReturnsAsync(new Message(ChatRole.Assistant, "llm answer"));
            var (agent, config) = CreateTracedAgent();

            // Act
            await agent.RunPrompt("hello");

            // Assert
            tracingDuringInference.Should().BeTrue();
            LameTelemetry.IsTracing.Should().BeFalse();
            // Already removed by RunPrompt: a second stop finds nothing.
            LameTelemetry.TryStopRootActivity(_collector.Single(config.TraceName!).Id!).Should().BeFalse();
        }

        [Fact]
        public async Task RunPrompt_WithoutTraces_CreatesNoActivity()
        {
            // Arrange
            var agent = CreateAgent();

            // Act
            await agent.RunPrompt("hello");

            // Assert
            _collector.Stopped.Should().BeEmpty();
        }

        [Fact]
        public async Task RunPrompt_WithoutAConfiguredSource_RunsWithoutTracing()
        {
            // Arrange: the app did not call AddLangfuseTracing.
            LameTelemetry.StopSource();
            var (agent, _) = CreateTracedAgent();

            // Act
            var result = await agent.RunPrompt("hello");

            // Assert
            result.Should().Be("llm answer");
            LameTelemetry.HasSource.Should().BeFalse();
            _collector.Stopped.Should().BeEmpty();
        }

        [Fact]
        public async Task RunPrompt_InsideAnUnrecordedAspNetCoreRequestActivity_StillStartsANewRootTraceAndRestoresTheRequestActivity()
        {
            // Arrange: ASP.NET Core opens an activity per request that nobody records. With OpenTelemetry's
            // ParentBased sampler a child of it would be dropped, so the agent must start its own trace.
            var (agent, config) = CreateTracedAgent();
            using var requestActivity = new Activity("Microsoft.AspNetCore.Hosting.HttpRequestIn").Start();
            requestActivity.Recorded.Should().BeFalse();

            // Act
            await agent.RunPrompt("hello");

            // Assert
            var root = _collector.Single(config.TraceName!);
            root.ParentSpanId.Should().Be(default(ActivitySpanId));
            root.TraceId.Should().NotBe(requestActivity.TraceId);
            Activity.Current.Should().BeSameAs(requestActivity);
        }

        [Fact]
        public async Task RunPrompt_InsideAnotherLameChainActivity_NestsTheAgentUnderIt()
        {
            // Arrange: e.g. an agent running inside a traced chain step.
            var (agent, config) = CreateTracedAgent();

            // Act
            using (ActivityCollector.StartRoot())
                await agent.RunPrompt("hello");

            // Assert
            var agentActivity = _collector.Single(config.TraceName!);
            agentActivity.ParentSpanId.Should().Be(_collector.Root.SpanId);
            agentActivity.TraceId.Should().Be(_collector.Root.TraceId);
        }

        [Fact]
        public async Task RunPrompt_WithTraces_AddsTheAgentTagsAndMetadataToTheTraceConfig()
        {
            // Arrange
            var config = new TraceConfig();
            var agent = CreateAgent()
                .AddTool(nameof(AgentTestTools.LookupWeather), new ToolBinding(typeof(AgentTestTools).GetMethod(nameof(AgentTestTools.LookupWeather))!))
                .WithSkills(["pirate-assistant"])
                .WithLangfuseTraces(config);

            // Act
            await agent.RunPrompt("hello");

            // Assert
            config.Tags.Should().Contain([nameof(LameAgent), agent.Id.ToString(), agent.FullHandlerModel, AgentName]);
            config.Metadata.Should().Contain(new Dictionary<string, string>
            {
                ["name"] = AgentName,
                ["description"] = AgentDescription,
                ["skills"] = "pirate-assistant",
                ["tools"] = nameof(AgentTestTools.LookupWeather),
                ["provider"] = "ollama",
                ["model"] = "llama3"
            });
        }

        [Fact]
        public async Task RunPrompt_WithTracesRunTwice_DoesNotThrowNorDuplicateTags()
        {
            // Arrange
            var (agent, config) = CreateTracedAgent();
            await agent.RunPrompt("first");

            // Act
            var secondRun = () => agent.RunPrompt("second");

            // Assert
            await secondRun.Should().NotThrowAsync();
            _collector.Named(config.TraceName!).Should().HaveCount(2);
            config.Tags.Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public async Task RunPrompt_WithTraces_WritesTheTraceConfigToTheRootActivity()
        {
            // Arrange
            var (agent, _) = CreateTracedAgent(config: new TraceConfig("my-trace", "chat-thread-42", "user-7", tags: ["sample"]));

            // Act
            await agent.RunPrompt("hello");

            // Assert: attribute names from the Langfuse OpenTelemetry property mapping.
            var root = _collector.Single("my-trace");
            root.Tag("langfuse.trace.name").Should().Be("my-trace");
            root.Tag("langfuse.session.id").Should().Be("chat-thread-42");
            root.Tag("langfuse.user.id").Should().Be("user-7");
            root.Tag("langfuse.trace.tags").Should().BeAssignableTo<IEnumerable<string>>().Which.Should().Contain(["sample", nameof(LameAgent)]);
            root.Tag("langfuse.trace.metadata.model").Should().Be("llama3");
        }

        [Fact]
        public async Task RunPrompt_WhenTheInferenceThrows_StopsAndUnregistersTheRoot()
        {
            // Arrange
            _host.Inference
                .Setup(s => s.ChatPrompt(It.IsAny<ChatRequest>(), It.IsAny<string>(), It.IsAny<Dictionary<string, ToolBinding>?>()))
                .ThrowsAsync(new HttpRequestException("ollama is down"));
            var (agent, config) = CreateTracedAgent();

            // Act
            var run = () => agent.RunPrompt("hello");

            // Assert
            await run.Should().ThrowAsync<HttpRequestException>();
            var root = _collector.Named(config.TraceName!).Should().ContainSingle().Subject;
            LameTelemetry.TryStopRootActivity(root.Id!).Should().BeFalse();
            LameTelemetry.IsTracing.Should().BeFalse();
        }

        #endregion

        #region Concurrent Runs

        [Fact]
        public async Task RunPrompt_TwoTracedAgentsAtTheSameTime_KeepTheirOwnRootsAndTracingUntilBothFinish()
        {
            // Arrange
            var (first, firstConfig) = CreateTracedAgent();
            var (second, secondConfig) = CreateTracedAgent();

            // Act
            var firstFlow = await RunFirstWhileSecondRunsToTheEnd(first, second);

            // Assert
            var firstRoot = _collector.Single(firstConfig.TraceName!);
            firstFlow.TracingAfterTheOtherRunEnded.Should().BeTrue();
            firstFlow.CurrentAfterTheOtherRunEnded.Should().BeSameAs(firstRoot);
            firstRoot.TraceId.Should().NotBe(_collector.Single(secondConfig.TraceName!).TraceId);
        }

        [Fact]
        public async Task RunPrompt_TwoRunsWithTheSameTraceNameAtTheSameTime_KeepTracingUntilBothFinish()
        {
            // Arrange: e.g. two requests to the same endpoint, both using TraceConfig("my-endpoint").
            var (first, _) = CreateTracedAgent(config: new TraceConfig("my-endpoint"));
            var (second, _) = CreateTracedAgent(config: new TraceConfig("my-endpoint"));

            // Act
            var firstFlow = await RunFirstWhileSecondRunsToTheEnd(first, second);

            // Assert
            var roots = _collector.Named("my-endpoint");
            roots.Should().HaveCount(2);
            roots.Select(r => r.TraceId).Should().OnlyHaveUniqueItems();
            firstFlow.TracingAfterTheOtherRunEnded.Should().BeTrue();
            firstFlow.CurrentAfterTheOtherRunEnded.Should().BeSameAs(roots.Last());
        }

        private sealed record FirstFlowState(bool TracingAfterTheOtherRunEnded, Activity? CurrentAfterTheOtherRunEnded);

        // The first agent's LLM call stays pending while the second agent runs from start to end; then, still
        // inside the first agent's flow (where its next LLM calls would happen), it checks what a handler would see.
        private async Task<FirstFlowState> RunFirstWhileSecondRunsToTheEnd(LameAgent first, LameAgent second)
        {
            var secondFinished = new TaskCompletionSource();
            FirstFlowState? state = null;
            var calls = 0;

            _host.Inference
                .Setup(s => s.ChatPrompt(It.IsAny<ChatRequest>(), It.IsAny<string>(), It.IsAny<Dictionary<string, ToolBinding>?>()))
                .Returns(() => Interlocked.Increment(ref calls) == 1
                    ? FirstInference()
                    : Task.FromResult(new Message(ChatRole.Assistant, "second answer")));

            async Task<Message> FirstInference()
            {
                await secondFinished.Task;
                state = new FirstFlowState(LameTelemetry.IsTracing, Activity.Current);
                return new Message(ChatRole.Assistant, "first answer");
            }

            var firstRun = first.RunPrompt("first");
            await second.RunPrompt("second");
            secondFinished.SetResult();
            await firstRun;

            return state!;
        }

        #endregion

        #region End To End Trace Tree

        [Fact]
        public async Task RunPrompt_AutoSkilledAgentLoadingAToolSkill_TracesTheWholeRunUnderTheAgentRoot()
        {
            // Arrange: real inference service + handler, only the Ollama HTTP client is mocked.
            using var host = new InferenceAgentTestHost().WithSkill("docs-query", TestSkills.DocsQuery);
            host.RespondsWithToolCall(nameof(AutoSkilledAgent.SkillsLoaderTool), InferenceAgentTestHost.Args(("skills", new[] { "docs-query" })))
                .RespondsWithToolCall(nameof(AgentTestTools.LookupWeather), InferenceAgentTestHost.Args(("city", "Kingston")))
                .RespondsWithText("It is sunny in Kingston");
            var agent = new AutoSkilledAgent(host.Services, "llama3", name: AgentName);
            agent.WithSkills(["docs-query"]);
            var config = new TraceConfig();
            agent.WithLangfuseTraces(config);

            // Act
            var result = await agent.RunPrompt("what is the weather in Kingston?");

            // Assert
            result.Should().Be("It is sunny in Kingston");
            _collector.StoppedNames.Should().Equal(
                "generation", nameof(AutoSkilledAgent.SkillsLoaderTool),
                "generation", nameof(AgentTestTools.LookupWeather),
                "generation", config.TraceName);

            var root = _collector.Single(config.TraceName!);
            _collector.Stopped.Where(a => a != root)
                .Should().OnlyContain(a => a.ParentSpanId == root.SpanId && a.TraceId == root.TraceId);
            _collector.Single(nameof(AgentTestTools.LookupWeather)).StringTag("langfuse.observation.output").Should().Contain("sunny in Kingston");
        }

        #endregion
    }
}
