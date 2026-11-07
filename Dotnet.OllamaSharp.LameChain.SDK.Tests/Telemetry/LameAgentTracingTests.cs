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
    // LameAgent.WithLangfuseTraces + the root activity opened by RunPrompt.
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

        #region WithLangfuseTraces Tests

        [Fact]
        public void WithLangfuseTraces_WithEmptyConfig_DefaultsTraceNameToAgentNameAndSessionToAgentId()
        {
            // Arrange
            var agent = CreateAgent();
            var config = new TraceConfig();

            // Act
            agent.WithLangfuseTraces(config);

            // Assert
            config.TraceName.Should().Be(AgentName);
            config.SessionId.Should().Be(agent.Id.ToString());
            Guid.TryParse(config.UserId, out _).Should().BeTrue();
            LameTelemetry.IsEnabled.Should().BeTrue();
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
            var agent = CreateAgent().WithLangfuseTraces(new TraceConfig());

            // Act
            await agent.RunPrompt("hello");

            // Assert
            var root = _collector.Single(AgentName);
            root.Kind.Should().Be(ActivityKind.Internal);
            root.ParentSpanId.Should().Be(default(ActivitySpanId));
            // Everything below the agent (inference service, handler, tools) hangs from this activity.
            currentDuringInference.Should().BeSameAs(root);
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
        public async Task RunPrompt_InsideAnUnrecordedAspNetCoreRequestActivity_StillStartsANewRootTraceAndRestoresTheRequestActivity()
        {
            // Arrange: ASP.NET Core opens an activity per request that nobody records. With OpenTelemetry's
            // ParentBased sampler a child of it would be dropped, so the agent must start its own trace.
            var agent = CreateAgent().WithLangfuseTraces(new TraceConfig());
            using var requestActivity = new Activity("Microsoft.AspNetCore.Hosting.HttpRequestIn").Start();
            requestActivity.Recorded.Should().BeFalse();

            // Act
            await agent.RunPrompt("hello");

            // Assert
            var root = _collector.Single(AgentName);
            root.ParentSpanId.Should().Be(default(ActivitySpanId));
            root.TraceId.Should().NotBe(requestActivity.TraceId);
            Activity.Current.Should().BeSameAs(requestActivity);
        }

        [Fact]
        public async Task RunPrompt_InsideAnotherLameChainActivity_NestsTheAgentUnderIt()
        {
            // Arrange: e.g. an agent running inside a traced chain step.
            var agent = CreateAgent().WithLangfuseTraces(new TraceConfig());

            // Act
            using (ActivityCollector.StartRoot())
                await agent.RunPrompt("hello");

            // Assert
            var agentActivity = _collector.Single(AgentName);
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

        [Fact(Skip = "SDK bug: RunPrompt adds the agent metadata with Dictionary.Add on every run, so the second run of the same traced agent throws ArgumentException (and the tags list keeps growing with duplicates).")]
        public async Task RunPrompt_WithTracesRunTwice_DoesNotThrow()
        {
            // Arrange
            var agent = CreateAgent().WithLangfuseTraces(new TraceConfig());
            await agent.RunPrompt("first");

            // Act
            var secondRun = () => agent.RunPrompt("second");

            // Assert
            await secondRun.Should().NotThrowAsync();
            _collector.Named(AgentName).Should().HaveCount(2);
        }

        [Fact(Skip = "SDK gap: the TraceConfig (session, user, tags, metadata) is filled but never written to the root activity, so Langfuse receives none of it.")]
        public async Task RunPrompt_WithTraces_WritesTheTraceConfigToTheRootActivity()
        {
            // Arrange
            var agent = CreateAgent().WithLangfuseTraces(new TraceConfig("my-trace", "chat-thread-42", "user-7", tags: ["sample"]));

            // Act
            await agent.RunPrompt("hello");

            // Assert: attribute names from the Langfuse OpenTelemetry property mapping.
            var root = _collector.Single("my-trace");
            root.Tag("langfuse.session.id").Should().Be("chat-thread-42");
            root.Tag("langfuse.user.id").Should().Be("user-7");
            root.Tag("langfuse.trace.tags").Should().BeAssignableTo<IEnumerable<string>>().Which.Should().Contain("sample");
            root.Tag("langfuse.trace.metadata.model").Should().Be("llama3");
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
            agent.WithLangfuseTraces(new TraceConfig());

            // Act
            var result = await agent.RunPrompt("what is the weather in Kingston?");

            // Assert
            result.Should().Be("It is sunny in Kingston");
            _collector.StoppedNames.Should().Equal(
                "generation", nameof(AutoSkilledAgent.SkillsLoaderTool),
                "generation", nameof(AgentTestTools.LookupWeather),
                "generation", AgentName);

            var root = _collector.Single(AgentName);
            _collector.Stopped.Where(a => a != root)
                .Should().OnlyContain(a => a.ParentSpanId == root.SpanId && a.TraceId == root.TraceId);
            _collector.Single(nameof(AgentTestTools.LookupWeather)).StringTag("langfuse.observation.output").Should().Contain("sunny in Kingston");
        }

        #endregion
    }
}
