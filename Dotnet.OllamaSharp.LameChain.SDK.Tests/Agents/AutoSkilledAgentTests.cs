using System.Text.Json;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Agents;
using Dotnet.OllamaSharp.LameChain.SDK.Tests.Infrastructure;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Agents
{
    public class AutoSkilledAgentTests : IDisposable
    {
        private readonly AgentTestHost _host = new AgentTestHost().WithDefaultSkills();

        public void Dispose() => _host.Dispose();

        // LameAgent's fluent builders return LameAgent, so skills are assigned here to keep the AutoSkilledAgent type.
        private AutoSkilledAgent CreateAgent(params string[] skills)
        {
            var agent = new AutoSkilledAgent(_host.Services, "llama3");
            agent.WithSkills(skills.ToList());
            return agent;
        }

        #region System message & built-in tools

        [Fact]
        public async Task RunPrompt_WithSkills_EquipsBuiltInSkillToolsBoundToTheAgent()
        {
            var agent = CreateAgent("pirate-assistant", "docs-query");

            await agent.RunPrompt("hello");

            agent.Tools.Keys.Should().BeEquivalentTo(nameof(AutoSkilledAgent.SkillsDiscoveryTool), nameof(AutoSkilledAgent.SkillsLoaderTool));
            // Built-in tools carry the agent itself as target, so they are never resolved from a ToolService.
            agent.Tools.Values.Should().OnlyContain(b => ReferenceEquals(b.target, agent));
            _host.LastToolDefinitionNames.Should().BeEquivalentTo(nameof(AutoSkilledAgent.SkillsDiscoveryTool), nameof(AutoSkilledAgent.SkillsLoaderTool));
        }

        [Fact]
        public async Task RunPrompt_WithSkills_AddsLoadingInstructionsInsteadOfSkillContent()
        {
            var agent = CreateAgent("pirate-assistant", "docs-query");

            await agent.RunPrompt("hello");

            _host.LastSystemMessage.Should().Contain(nameof(AutoSkilledAgent.SkillsDiscoveryTool))
                .And.Contain(nameof(AutoSkilledAgent.SkillsLoaderTool))
                .And.NotContain(TestSkills.PirateBody)
                .And.NotContain(TestSkills.DocsBody);
            // Skill tools are loaded progressively by the LLM, never upfront.
            agent.Tools.Should().NotContainKey(nameof(AgentTestTools.SearchDocs));
        }

        [Fact]
        public async Task RunPrompt_WithoutSkills_HasNoBuiltInToolsNorLoadingInstructions()
        {
            var agent = CreateAgent();

            await agent.RunPrompt("hello");

            agent.HasTools.Should().BeFalse();
            _host.LastSystemMessage.Should().NotContain(nameof(AutoSkilledAgent.SkillsDiscoveryTool));
        }

        #endregion

        #region SkillsDiscoveryTool

        [Fact]
        public async Task SkillsDiscoveryTool_ReturnsTheHeadingOfEveryAssignedSkill()
        {
            var agent = CreateAgent("pirate-assistant", "docs-query");

            var catalogue = await agent.SkillsDiscoveryTool();

            catalogue.Should().Contain("name: pirate-assistant").And.Contain("name: docs-query")
                .And.Contain("allowed-tools: [LookupWeather, SearchDocs, UnknownTool]")
                .And.NotContain(TestSkills.PirateBody)
                .And.NotContain(TestSkills.DocsBody);
            catalogue.Should().NotContain("senior-code-reviewer", "only the skills assigned to the agent are discoverable");
        }

        [Fact]
        public async Task SkillsDiscoveryTool_WithoutSkills_ReturnsEmpty()
        {
            (await CreateAgent().SkillsDiscoveryTool()).Should().BeEmpty();
        }

        #endregion

        #region SkillsLoaderTool

        [Fact]
        public async Task SkillsLoaderTool_WithToolSkill_ReturnsSkillContentAndEquipsAllowedToolsAsToolServiceBindings()
        {
            var agent = CreateAgent("docs-query");

            var loaded = await agent.SkillsLoaderTool(["docs-query"]);

            loaded.Should().Contain(TestSkills.DocsBody);
            agent.Tools.Should().ContainKeys(nameof(AgentTestTools.LookupWeather), nameof(AgentTestTools.SearchDocs));
            agent.Tools.Should().NotContainKey("UnknownTool");
            agent.Tools[nameof(AgentTestTools.SearchDocs)].target.Should().BeNull();
        }

        [Fact]
        public async Task SkillsLoaderTool_WithBehavioralSkills_ReturnsEveryRequestedSkillWithoutEquippingTools()
        {
            var agent = CreateAgent("pirate-assistant", "senior-code-reviewer");

            var loaded = await agent.SkillsLoaderTool(["pirate-assistant", "senior-code-reviewer"]);

            loaded.Should().Contain(TestSkills.PirateBody).And.Contain(TestSkills.ReviewerBody);
            agent.HasTools.Should().BeFalse();
        }

        #endregion

        #region Built-in tool resolution through the inference handler

        // The handler's service provider is strict and has no setups: if the SDK tried to resolve the
        // built-in tool from an IToolsService<> instead of using the binding target, the call would throw.
        private static TestBaseHandler CreateHandlerWithoutToolServices()
            => new(new Mock<IServiceProvider>(MockBehavior.Strict).Object, new Mock<IConfiguration>().Object, "test");

        [Fact]
        public async Task BuiltInDiscoveryTool_InvokedByHandler_RunsOnTheAgentInstance()
        {
            var agent = CreateAgent("pirate-assistant");
            await agent.RunPrompt("hello");

            var result = await CreateHandlerWithoutToolServices()
                .InvokeGetToolResult(agent.Tools[nameof(AutoSkilledAgent.SkillsDiscoveryTool)], new Dictionary<string, object>());

            result.Should().BeOfType<string>().Which.Should().Contain("name: pirate-assistant");
        }

        [Fact]
        public async Task BuiltInLoaderTool_InvokedByHandlerWithLlmArguments_LoadsSkillAndEquipsItsTools()
        {
            var agent = CreateAgent("docs-query");
            await agent.RunPrompt("search the docs");
            var llmArguments = new Dictionary<string, object> { ["skills"] = JsonSerializer.SerializeToElement(new[] { "docs-query" }) };

            var result = await CreateHandlerWithoutToolServices()
                .InvokeGetToolResult(agent.Tools[nameof(AutoSkilledAgent.SkillsLoaderTool)], llmArguments);

            result.Should().BeOfType<string>().Which.Should().Contain(TestSkills.DocsBody);
            agent.Tools.Should().ContainKeys(nameof(AgentTestTools.LookupWeather), nameof(AgentTestTools.SearchDocs));
            // The loaded tools land in the same dictionary the handler received as toolsLookup,
            // so the follow-up tool calls can be resolved within the same RunPrompt.
            _host.LastTools.Should().BeSameAs(agent.Tools);
        }

        #endregion
    }
}
