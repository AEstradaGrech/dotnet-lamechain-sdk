using FluentAssertions;
using Moq;
using OllamaSharp.Models.Chat;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.AtomicValues;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Agents;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Agents
{
    public class SelectedSkillsAgentTests : IDisposable
    {
        private readonly AgentTestHost _host = new AgentTestHost().WithDefaultSkills();
        private ChatRequest? _selectionRequest;

        public SelectedSkillsAgentTests()
        {
            // The agent asks the commands factory for a MultiChoiceCommand; use a real one running on the
            // mocked inference service so the selection request goes through the actual command logic.
            _host.CommandsFactory
                .Setup(f => f.GetMultiChoiceCommand(It.IsAny<string?>(), It.IsAny<CommandSettings?>()))
                .Returns(() => new MultiChoiceCommand(_host.Inference.Object));
        }

        public void Dispose() => _host.Dispose();

        private void SetupSelection(params string[] selected)
            => _host.Inference
                .Setup(s => s.CommandPrompt<MultiChoiceResponse>(
                    It.IsAny<ChatRequest>(),
                    It.IsAny<CommandPromptValidation<MultiChoiceResponse>?>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, ToolBinding>?>(),
                    It.IsAny<bool>()))
                .Callback<ChatRequest, CommandPromptValidation<MultiChoiceResponse>?, string, Dictionary<string, ToolBinding>?, bool>((r, _, _, _, _) => _selectionRequest = r)
                .ReturnsAsync(new MultiChoiceResponse { Selected = selected.ToList() });

        private SelectedSkillsAgent CreateAgent()
            => new SelectedSkillsAgent(_host.Services, _host.CommandsFactory.Object, "llama3");

        [Fact]
        public void Ctor_WithNullCommandsFactory_ThrowsArgumentNullException()
        {
            Action act = () => new SelectedSkillsAgent(_host.Services, null!, "llama3");

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public async Task RunPrompt_SendsSkillNamesAndDescriptionsAsChoicesWithUserInput()
        {
            SetupSelection();
            var agent = CreateAgent().WithSkills(["pirate-assistant", "senior-code-reviewer"]);

            await agent.RunPrompt("review my C# code");

            var selectionMessages = _selectionRequest!.Messages.ToList();
            var choicesPrompt = selectionMessages.First(m => m.Role == ChatRole.System).Content;
            choicesPrompt.Should().Contain("pirate-assistant").And.Contain("Makes the assistant talk like a pirate.")
                .And.Contain("senior-code-reviewer").And.Contain("Makes the assistant answer as a strict senior code reviewer.");
            // Only name + description are offered as choices, never the frontmatter metadata or the skill body.
            choicesPrompt.Should().NotContain("version").And.NotContain(TestSkills.PirateBody);
            selectionMessages.Last().Content.Should().EndWith("review my C# code");
        }

        [Fact]
        public async Task RunPrompt_WhenNoSkillIsSelected_DoesNotAddSkillsToSystemMessage()
        {
            SetupSelection();
            var agent = CreateAgent().WithSkills(["pirate-assistant", "senior-code-reviewer"]);

            await agent.RunPrompt("what time is it?");

            _host.LastSystemMessage.Should().NotContain("# SKILLS")
                .And.NotContain(TestSkills.PirateBody)
                .And.NotContain(TestSkills.ReviewerBody);
        }

        [Fact]
        public async Task RunPrompt_WithoutSkills_DoesNotRunSkillSelection()
        {
            var agent = CreateAgent();

            await agent.RunPrompt("hello");

            _host.CommandsFactory.Verify(f => f.GetMultiChoiceCommand(It.IsAny<string?>(), It.IsAny<CommandSettings?>()), Times.Never);
            _host.LastSystemMessage.Should().NotContain("# SKILLS");
        }

        [Fact]
        public async Task RunPrompt_WhenOneSkillIsSelected_AddsOnlyThatSkillToSystemMessage()
        {
            SetupSelection("senior-code-reviewer"); // SELECTION_MESSAGE asks for SKILL NAMES, so the LLM returns the bare name
            var agent = CreateAgent().WithSkills(["pirate-assistant", "senior-code-reviewer"]);

            await agent.RunPrompt("review my C# code");

            _host.LastSystemMessage.Should().Contain(TestSkills.ReviewerBody)
                .And.NotContain(TestSkills.PirateBody);
        }
    }
}
