using FluentAssertions;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Agents;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Agents
{
    public class LameAgentTests : IDisposable
    {
        private readonly AgentTestHost _host = new AgentTestHost().WithDefaultSkills();

        public void Dispose() => _host.Dispose();

        private LameAgent CreateAgent(string model = "llama3", PromptSettings? settings = null, string? name = null, string? description = null)
            => new LameAgent(_host.Services, model, settings, name, description);

        private static ToolBinding BindingFor(string methodName)
            => new ToolBinding(typeof(AgentTestTools).GetMethod(methodName)!);

        #region Construction

        [Theory]
        [InlineData("llama3", "ollama", "llama3")]
        [InlineData("groq_llama-3.3-70b-versatile", "groq", "llama-3.3-70b-versatile")]
        [InlineData("anthropic_claude-sonnet-4-6", "anthropic", "claude-sonnet-4-6")]
        [InlineData("ollama_HammerAI/gemma4-heretic", "ollama", "HammerAI/gemma4-heretic")]
        public void Ctor_WithModel_ParsesProviderAndModel(string model, string expectedProvider, string expectedModel)
        {
            var agent = CreateAgent(model);

            agent.Provider.Should().Be(expectedProvider);
            agent.Model.Should().Be(expectedModel);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Ctor_WithoutModel_UsesOllamaDefaultModelFromSettings(string? model)
        {
            var agent = CreateAgent(model!);

            agent.Provider.Should().Be("ollama");
            agent.Model.Should().Be(AgentTestHost.DefaultModel);
        }

        [Fact]
        public void Ctor_WithNullServiceProvider_ThrowsArgumentNullException()
        {
            Action act = () => new LameAgent(null!, "llama3");

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Ctor_AssignsUniqueIdNameAndDescription()
        {
            var first = CreateAgent(name: "Agent", description: "Does things");
            var second = CreateAgent();

            first.Id.Should().NotBe(Guid.Empty).And.NotBe(second.Id);
            first.Name.Should().Be("Agent");
            first.Description.Should().Be("Does things");
        }

        #endregion

        #region Builders

        [Fact]
        public void AddTool_SameNameTwice_KeepsFirstBinding()
        {
            var first = BindingFor(nameof(AgentTestTools.LookupWeather));
            var agent = CreateAgent()
                .AddTool("Weather", first)
                .AddTool("Weather", BindingFor(nameof(AgentTestTools.SearchDocs)));

            agent.Tools.Should().ContainSingle();
            agent.Tools["Weather"].Should().BeSameAs(first);
            agent.HasTools.Should().BeTrue();
        }

        [Fact]
        public void AddTools_AddsEveryBinding()
        {
            var agent = CreateAgent().AddTools(new Dictionary<string, ToolBinding>
            {
                [nameof(AgentTestTools.LookupWeather)] = BindingFor(nameof(AgentTestTools.LookupWeather)),
                [nameof(AgentTestTools.SearchDocs)] = BindingFor(nameof(AgentTestTools.SearchDocs))
            });

            agent.Tools.Keys.Should().BeEquivalentTo(nameof(AgentTestTools.LookupWeather), nameof(AgentTestTools.SearchDocs));
        }

        [Fact]
        public void WithSkills_IgnoresDuplicates()
        {
            var agent = CreateAgent()
                .WithSkills(["pirate-assistant", "senior-code-reviewer"])
                .WithSkills(["pirate-assistant"]);

            agent.Skills.Should().Equal("pirate-assistant", "senior-code-reviewer");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AddDataSources_ExistingSource_ReplacesItsData(bool isOverride)
        {
            var agent = CreateAgent()
                .AddDataSources("catalog", ["old"])
                .AddDataSources("catalog", ["new"], isOverride);

            agent.DataSources.Should().ContainSingle();
            agent.DataSources["catalog"].Should().Equal("new");
        }

        #endregion

        #region RunPrompt

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task RunPrompt_WithEmptyPrompt_ThrowsInvalidOperationException(string prompt)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAgent().RunPrompt(prompt));

            _host.ChatPromptCalls.Should().Be(0);
        }

        [Fact]
        public async Task RunPrompt_Basic_SendsSystemAndUserMessagesToAgentModelAndProvider()
        {
            var agent = CreateAgent("groq_llama-3.3-70b-versatile");

            var result = await agent.RunPrompt("hello");

            result.Should().Be("llm answer");
            agent.UserInput.Should().Be("hello");
            _host.LastProvider.Should().Be("groq");
            _host.LastRequest!.Model.Should().Be("llama-3.3-70b-versatile");
            _host.LastRequest.Stream.Should().BeFalse();
            _host.LastMessages.Select(m => m.Role).Should().Equal(ChatRole.System, ChatRole.User);
            _host.LastMessages[1].Content.Should().Be("hello");
        }

        [Fact]
        public async Task RunPrompt_WithNameDescriptionAndInstruction_BuildsSystemMessageWithInstructionFirst()
        {
            var agent = CreateAgent(name: "Captain", description: "A helpful sailor");

            await agent.RunPrompt("hello", systemInstruction: "Be brief.");

            var system = _host.LastSystemMessage;
            system.Should().StartWith("Be brief.");
            system.Should().Contain("# Agent Name: Captain");
            system.Should().Contain("# Agent Description: A helpful sailor");
            system.IndexOf("Be brief.").Should().BeLessThan(system.IndexOf("# Agent Name"));
        }

        [Fact]
        public async Task RunPrompt_WithDataSources_AppendsThemToSystemMessage()
        {
            var agent = CreateAgent().AddDataSources("catalog", ["item one", "item two"]);

            await agent.RunPrompt("hello");

            _host.LastSystemMessage.Should().Contain("# Data Sources:")
                .And.Contain("- catalog: item one\n\nitem two");
        }

        [Fact]
        public async Task RunPrompt_WithSettings_MapsThemToRequestOptions()
        {
            var agent = CreateAgent(settings: new PromptSettings(maxTokens: 123, temperature: 0.7f));

            await agent.RunPrompt("hello");

            _host.LastRequest!.Options!.NumPredict.Should().Be(123);
            _host.LastRequest.Options.Temperature.Should().Be(0.7f);
        }

        [Fact]
        public async Task RunPrompt_WithChatHistory_ReplacesHistorySystemMessageAndAppendsUserPrompt()
        {
            var agent = CreateAgent(name: "Captain");
            var history = new List<ChatMessage>
            {
                new(ChatRole.System.ToString(), "old system message"),
                new(ChatRole.User.ToString(), "previous question"),
                new(ChatRole.Assistant.ToString(), "previous answer")
            };

            await agent.RunPrompt("follow-up", chatHistory: history);

            var messages = _host.LastMessages;
            messages.Select(m => m.Role).Should().Equal(ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.User);
            messages[0].Content.Should().Contain("# Agent Name: Captain").And.NotContain("old system message");
            messages[1].Content.Should().Be("previous question");
            messages[3].Content.Should().Be("follow-up");
        }

        [Fact]
        public async Task RunPrompt_WithChatHistoryWithoutSystemMessage_KeepsWholeHistory()
        {
            var history = new List<ChatMessage>
            {
                new(ChatRole.User.ToString(), "previous question"),
                new(ChatRole.Assistant.ToString(), "previous answer")
            };

            await CreateAgent().RunPrompt("follow-up", chatHistory: history);

            _host.LastMessages.Select(m => m.Content).Should().Equal("", "previous question", "previous answer", "follow-up");
        }

        [Fact]
        public async Task RunPrompt_WithTools_AdvertisesDefinitionsAndPassesBindingsForResolution()
        {
            var agent = CreateAgent().AddTool(nameof(AgentTestTools.LookupWeather), BindingFor(nameof(AgentTestTools.LookupWeather)));

            await agent.RunPrompt("weather in Madrid?");

            _host.LastToolDefinitionNames.Should().Equal(nameof(AgentTestTools.LookupWeather));
            _host.LastTools.Should().BeSameAs(agent.Tools);
        }

        [Fact]
        public async Task RunPrompt_WithoutTools_SendsNoToolDefinitions()
        {
            await CreateAgent().RunPrompt("hello");

            _host.LastRequest!.Tools.Should().BeNull();
        }

        [Fact]
        public async Task RunPrompt_WhenResponseContentIsNull_ReturnsEmptyString()
        {
            _host.Inference
                .Setup(s => s.ChatPrompt(It.IsAny<ChatRequest>(), It.IsAny<string>(), It.IsAny<Dictionary<string, ToolBinding>?>()))
                .ReturnsAsync(new Message { Role = ChatRole.Assistant, Content = null });

            var result = await CreateAgent().RunPrompt("hello");

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task RunPrompt_WhenInferenceReturnsNull_ThrowsArgumentNullException()
        {
            _host.Inference
                .Setup(s => s.ChatPrompt(It.IsAny<ChatRequest>(), It.IsAny<string>(), It.IsAny<Dictionary<string, ToolBinding>?>()))
                .ReturnsAsync((Message)null!);

            await Assert.ThrowsAsync<ArgumentNullException>(() => CreateAgent().RunPrompt("hello"));
        }

        #endregion

        #region Skills

        [Fact]
        public async Task RunPrompt_WithBehavioralSkills_AddsEverySkillToSystemMessageWithoutTools()
        {
            var agent = CreateAgent().WithSkills(["pirate-assistant", "senior-code-reviewer"]);

            await agent.RunPrompt("review this");

            _host.LastSystemMessage.Should().Contain("# SKILLS")
                .And.Contain(TestSkills.PirateBody)
                .And.Contain(TestSkills.ReviewerBody);
            agent.HasTools.Should().BeFalse();
        }

        [Fact]
        public async Task RunPrompt_WithToolSkill_EquipsAllowedToolsFromToolboxAsToolServiceBindings()
        {
            var agent = CreateAgent().WithSkills(["docs-query"]);

            await agent.RunPrompt("search the docs");

            _host.LastSystemMessage.Should().Contain(TestSkills.DocsBody);
            // UnknownTool is listed in 'allowed-tools' but is not registered in any ToolService -> skipped.
            agent.Tools.Keys.Should().BeEquivalentTo(nameof(AgentTestTools.LookupWeather), nameof(AgentTestTools.SearchDocs));
            agent.Tools.Values.Should().OnlyContain(b => b.target == null && b.methodInfo.DeclaringType == typeof(AgentTestTools));
            _host.LastToolDefinitionNames.Should().BeEquivalentTo(nameof(AgentTestTools.LookupWeather), nameof(AgentTestTools.SearchDocs));
        }

        [Fact]
        public async Task RunPrompt_WithToolSkillRunTwice_DoesNotDuplicateTools()
        {
            var agent = CreateAgent().WithSkills(["docs-query"]);

            await agent.RunPrompt("first");
            await agent.RunPrompt("second");

            agent.Tools.Should().HaveCount(2);
        }

        [Fact(Skip = "SDK bug: LameAgent.loadSkillTools reads everything after 'allowed-tools:' up to the end of the frontmatter, so any field after it (here 'version') is glued to the last tool name and that tool is silently dropped.")]
        public async Task RunPrompt_WithToolSkillWhereAllowedToolsIsNotLastField_EquipsAllAllowedTools()
        {
            var agent = CreateAgent().WithSkills(["docs-query-reordered"]);

            await agent.RunPrompt("search the docs");

            agent.Tools.Keys.Should().BeEquivalentTo(nameof(AgentTestTools.LookupWeather), nameof(AgentTestTools.SearchDocs));
        }

        #endregion

        #region Broadcast

        [Fact]
        public async Task RunPrompt_WithBroadcast_BroadcastsSetupRequestAndResponseWithAgentId()
        {
            var broadcasts = new List<(Guid Id, string Message, LogLevel Level)>();
            var agent = CreateAgent("groq_llama-3.3-70b-versatile", name: "Captain")
                .WithBroadcast((id, message, level) => broadcasts.Add((id, message, level)));

            await agent.RunPrompt("hello");

            broadcasts.Should().HaveCount(3).And.OnlyContain(b => b.Id == agent.Id && b.Level == LogLevel.Information);
            broadcasts[0].Message.Should().Contain("groq/llama-3.3-70b-versatile").And.Contain("> Agent Setup:").And.Contain("# Agent Name: Captain");
            broadcasts[1].Message.Should().Contain("Sending request to LLM");
            broadcasts[2].Message.Should().Contain("LLM Response >> llm answer");
        }

        [Fact]
        public async Task RunPrompt_WithBroadcast_ForwardsInferenceServiceNotificationsToBroadcaster()
        {
            Action<string>? notifier = null;
            _host.Inference.Setup(s => s.SubscribeNotifier(It.IsAny<Action<string>>())).Callback<Action<string>>(n => notifier = n);
            var broadcasts = new List<string>();
            var agent = CreateAgent().WithBroadcast((_, message, _) => broadcasts.Add(message));

            await agent.RunPrompt("hello");
            notifier.Should().NotBeNull();
            notifier!("- LLM TOOL CALL: ollama/llama3 >> LookupWeather");

            broadcasts.Last().Should().Contain(nameof(LameAgent)).And.Contain("- LLM TOOL CALL: ollama/llama3 >> LookupWeather");
        }

        [Fact]
        public async Task RunPrompt_WithoutBroadcast_DoesNotSubscribeToInferenceNotifications()
        {
            await CreateAgent().RunPrompt("hello");

            _host.Inference.Verify(s => s.SubscribeNotifier(It.IsAny<Action<string>>()), Times.Never);
        }

        [Fact(Skip = "SDK bug: LameAgent.RunPrompt calls SubscribeNotifier on every run, so a reused agent (or a shared scoped inference service) accumulates handlers and broadcasts every notification N times.")]
        public async Task RunPrompt_WithBroadcastRunTwice_SubscribesToInferenceNotificationsOnce()
        {
            var agent = CreateAgent().WithBroadcast((_, _, _) => { });

            await agent.RunPrompt("first");
            await agent.RunPrompt("second");

            _host.Inference.Verify(s => s.SubscribeNotifier(It.IsAny<Action<string>>()), Times.Once);
        }

        #endregion

        #region AgentsFactory

        [Fact]
        public void AgentsFactory_CreatesEveryAgentTypeWithItsConfiguration()
        {
            using var scope = _host.Services.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IAgentsFactory>();
            var settings = new PromptSettings(100, 0.1f);

            var basic = factory.CreateAgent("llama3", settings);
            var named = factory.CreateAgent("groq_llama-3.3-70b-versatile", "Captain", "A sailor", settings);
            var selected = factory.CreateSelectedSkillsAgent("llama3", "Selector", "Selects skills", settings);
            var auto = factory.CreateAutoSkilledAgent("anthropic_claude-sonnet-4-6", "Auto", "Loads skills", settings);

            basic.Should().BeOfType<LameAgent>();
            basic.Name.Should().BeNull();
            named.Name.Should().Be("Captain");
            named.Description.Should().Be("A sailor");
            named.Provider.Should().Be("groq");
            selected.Should().BeOfType<SelectedSkillsAgent>();
            selected.Name.Should().Be("Selector");
            auto.Should().BeOfType<AutoSkilledAgent>();
            auto.Provider.Should().Be("anthropic");
            auto.Model.Should().Be("claude-sonnet-4-6");
        }

        #endregion
    }
}
