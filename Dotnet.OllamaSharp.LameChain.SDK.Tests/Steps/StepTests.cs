using FluentAssertions;
using Moq;
using DotnetLlamaSharp.Domain.Services.Inference;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Steps;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Response;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Storeables;
using System.Text.Json;
using System.Text.Json.Schema;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Steps
{
    public class StepTests
    {
        private readonly Mock<IOllamaInferenceService> _mockOllama = new();

        // Builds a mocked IJsoneable that returns a controlled JsonPromptResult for any Prompt call.
        private Mock<IJsoneable> MockCommand<T>(T response) where T : class
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var json = JsonSerializer.Serialize(response, options);
            var promptResult = new JsonPromptResult(
                "# INSTRUCTION: test instruction",
                response,
                typeof(T),
                json,
                JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(T)),
                options);

            var mock = new Mock<IJsoneable>();
            mock.Setup(x => x.JsonPrompt(
                    It.IsAny<PromptCommandRequest>(),
                    It.IsAny<CommandSettings?>(),
                    It.IsAny<bool>(),
                    It.IsAny<string?>()))
                .ReturnsAsync(promptResult);
            mock.Setup(x => x.CommandSettings).Returns((CommandSettings?)null);
            mock.Setup(x => x.BorrowLlama).Returns(_mockOllama.Object);
            return mock;
        }

        // Creates a minimal ChainRunner with a non-null intent to avoid the intent.Trim() NPE in the constructor.
        private static ChainRunner MakeRunner(string input = "test user input")
            => new ChainRunner(input, new CommandSettings("test-model"), null, "test-intent");

        #region SingleThrowStep Tests

        [Fact]
        public async Task SingleThrowStep_Forge_TwoStepChain_BothStepsAreForged()
        {
            var mock = MockCommand(new BooleanResponse { Answer = true });
            var runner = MakeRunner();
            var step1 = new SingleThrowStep(
                new StepSettings(mock.Object, new PromptCommandRequest("step 1 prompt")), runner);
            var step2 = new SingleThrowStep(
                new StepSettings(mock.Object, new PromptCommandRequest("step 2 prompt")));

            step1.Link(step2, isForward: true, isTwoWay: true);

            var result = await step1.Forge(null!);

            result.Should().BeSameAs(step2);
            result.IsForged.Should().BeTrue();
            result.Outputs.Should().HaveCount(1);
            step1.IsForged.Should().BeTrue();
        }

        [Fact]
        public async Task SingleThrowStep_ExecuteChainAsync_WhenNotReady_ThrowsInvalidOperationException()
        {
            // A step without a ChainRunner is not ready; ExecuteChainAsync must reject it.
            var step = new SingleThrowStep(new StepSettings(new PromptCommandRequest("test")));
            var next = new SingleThrowStep(new StepSettings(new PromptCommandRequest("next")));
            // Must link a next so IsFirstStep() returns true (needed by getFirstStep traversal).
            step.Link(next, isForward: true, isTwoWay: true);

            await Assert.ThrowsAsync<InvalidOperationException>(() => step.ExecuteChainAsync());
        }

        [Fact]
        public async Task SingleThrowStep_Forge_WithEmptyRequestModel_AppliesProviderAndModelFromRunnerDefaultSettings()
        {
            // forgeLink now defaults an empty PromptCommandRequest.Model from the command's own
            // CommandSettings (null here, via MockCommand) or the ChainRunner's DefaultSettings,
            // before calling JsonPrompt - it no longer implicitly stays on "ollama".
            var mock = MockCommand(new BooleanResponse { Answer = true });
            var runner = new ChainRunner("test user input", new CommandSettings("groq/some-model"), null, "test-intent");
            var request = new PromptCommandRequest("step 1 prompt"); // Model left null -> Provider="ollama", Model=""
            var step = new SingleThrowStep(new StepSettings(mock.Object, request), runner);

            await step.Forge(null!);

            step.Request.Provider.Should().Be("groq");
            step.Request.Model.Should().Be("some-model");
        }

        [Fact]
        public async Task SingleThrowStep_Forge_WithEmptyRequestModelAndCommandLevelSettings_PrefersCommandSettingsOverRunnerDefault()
        {
            // Arrange: the command's own CommandSettings takes precedence over the ChainRunner's
            // DefaultSettings when both are present.
            var mock = MockCommand(new BooleanResponse { Answer = true });
            mock.Setup(x => x.CommandSettings).Returns(new CommandSettings("anthropic/claude-model"));
            var runner = new ChainRunner("test user input", new CommandSettings("groq/other-model"), null, "test-intent");
            var request = new PromptCommandRequest("step 1 prompt");
            var step = new SingleThrowStep(new StepSettings(mock.Object, request), runner);

            await step.Forge(null!);

            step.Request.Provider.Should().Be("anthropic");
            step.Request.Model.Should().Be("claude-model");
        }

        [Fact]
        public async Task SingleThrowStep_Forge_WithModelAlreadySet_DoesNotOverrideExistingModelOrProvider()
        {
            // Arrange: the default-model block is only entered when Request.Model is empty - a
            // caller-provided model/provider must survive untouched.
            var mock = MockCommand(new BooleanResponse { Answer = true });
            var runner = new ChainRunner("test user input", new CommandSettings("groq/other-model"), null, "test-intent");
            var request = new PromptCommandRequest("step 1 prompt", model: "preset-model");
            var step = new SingleThrowStep(new StepSettings(mock.Object, request), runner);

            await step.Forge(null!);

            step.Request.Provider.Should().Be("ollama");
            step.Request.Model.Should().Be("preset-model");
        }

        #endregion

        #region ConditionalStep Tests

        [Fact]
        public async Task ConditionalStep_Forge_WhenConditionIsFalse_ContinuesToNextStep()
        {
            // ConditionalStep passes outputs through and skips the true-branch when condition=false.
            var mock = MockCommand(new BooleanResponse { Answer = true });
            var runner = MakeRunner();

            var step1 = new SingleThrowStep(
                new StepSettings(mock.Object, new PromptCommandRequest("step1")), runner);
            // ConditionalStep needs no command of its own — it copies previous outputs.
            var conditional = new ConditionalStep(
                () => false,
                new StepSettings(new PromptCommandRequest("conditional")));
            var trueBranch = new SingleThrowStep(
                new StepSettings(mock.Object, new PromptCommandRequest("true-branch")));
            var step3 = new SingleThrowStep(
                new StepSettings(mock.Object, new PromptCommandRequest("step3")));

            conditional.IfTrueThen(trueBranch);
            step1.Link(conditional, isForward: true, isTwoWay: true);
            conditional.Link(step3, isForward: true, isTwoWay: true);

            var result = await step1.Forge(null!);

            result.Should().BeSameAs(step3);
            step3.IsForged.Should().BeTrue();
        }

        [Fact]
        public void ConditionalStep_CanBeForged_WhenTrueBranchIsNotSet_ReturnsFalse()
        {
            var runner = MakeRunner();
            var conditional = new ConditionalStep(
                () => true,
                new StepSettings(new PromptCommandRequest("conditional")));
            var prevStep = new SingleThrowStep(new StepSettings(new PromptCommandRequest("prev")));

            conditional.Catch(runner);
            // Link a backward reference so IsChained(isForwardCheck: false) = true.
            conditional.Link(prevStep, isForward: false, isTwoWay: false);
            // TrueBranchRunner deliberately not set.

            conditional.CanBeForged(prevStep).Should().BeFalse();
        }

        #endregion

        #region SmartConditionalStep Tests

        [Fact]
        public async Task SmartConditionalStep_Forge_WhenScoredAnswerIsFalse_ContinuesToNextStep()
        {
            // SmartConditionalStep runs its ScoredBoolCommand and branches based on the Answer flag.
            var boolMock = MockCommand(new ScoredBoolResponse { Answer = false, Score = 0.1f, Justification = "no" });
            var genericMock = MockCommand(new BooleanResponse { Answer = true });
            var runner = MakeRunner();

            var step1 = new SingleThrowStep(
                new StepSettings(genericMock.Object, new PromptCommandRequest("step1")), runner);
            var smartCond = new SmartConditionalStep(
                new StepSettings(boolMock.Object, new PromptCommandRequest("smart-eval")));
            var trueBranch = new SingleThrowStep(
                new StepSettings(genericMock.Object, new PromptCommandRequest("true-branch")));
            var step3 = new SingleThrowStep(
                new StepSettings(genericMock.Object, new PromptCommandRequest("step3")));

            smartCond.IfTrueThen(trueBranch);
            step1.Link(smartCond, isForward: true, isTwoWay: true);
            smartCond.Link(step3, isForward: true, isTwoWay: true);

            var result = await step1.Forge(null!);

            result.Should().BeSameAs(step3);
            step3.IsForged.Should().BeTrue();
        }

        [Fact]
        public void SmartConditionalStep_CanBeForged_WhenTrueBranchIsNotSet_ReturnsFalse()
        {
            // CanBeForged requires IsReady() && IsChained(forward) && TrueBranchRunner != null.
            var mock = MockCommand(new BooleanResponse());
            var runner = MakeRunner();
            var smart = new SmartConditionalStep(
                new StepSettings(mock.Object, new PromptCommandRequest("smart")));
            var nextStep = new SingleThrowStep(
                new StepSettings(mock.Object, new PromptCommandRequest("next")));

            smart.Catch(runner);
            smart.Link(nextStep, isForward: true, isTwoWay: true);
            // TrueBranchRunner deliberately not set.

            smart.CanBeForged(null!).Should().BeFalse();
        }

        #endregion

        #region SplitterStep Tests

        [Fact]
        public void SplitterStep_CanBeForged_WhenProperlyConfiguredWithBranches_ReturnsTrue()
        {
            // SplitterStep requires IsRunning, non-null non-multisocket previous, and at least one branch.
            var mock = MockCommand(new BooleanResponse());
            var runner = MakeRunner();
            var branchSettings = new List<StepSettings>
            {
                new StepSettings(mock.Object, new PromptCommandRequest("branch1")),
                new StepSettings(mock.Object, new PromptCommandRequest("branch2"))
            };
            var splitter = new SplitterStep(branchSettings, new StepSettings(new PromptCommandRequest("splitter")));
            splitter.Catch(runner);

            var prevMock = new Mock<IChaineable>();
            prevMock.Setup(x => x.IsMultiSocket).Returns(false);

            splitter.CanBeForged(prevMock.Object).Should().BeTrue();
        }

        [Fact]
        public void SplitterStep_CanBeForged_WhenNoBranchesConfigured_ReturnsFalse()
        {
            var runner = MakeRunner();
            // Empty branch list means nothing to split into.
            var splitter = new SplitterStep([], new StepSettings(new PromptCommandRequest("splitter")));
            splitter.Catch(runner);

            var prevMock = new Mock<IChaineable>();
            prevMock.Setup(x => x.IsMultiSocket).Returns(false);

            splitter.CanBeForged(prevMock.Object).Should().BeFalse();
        }

        #endregion

        #region PipedStep Tests

        [Fact]
        public void PipedStep_CanBeForged_WhenMultiSocketPreviousAndOneCommand_ReturnsTrue()
        {
            // PipedStep maps one command onto each grouped output from a multi-socket previous.
            var mock = MockCommand(new BooleanResponse());
            var piped = new PipedStep(new StepSettings(mock.Object, new PromptCommandRequest("pipe")));

            var prevMock = new Mock<IChaineable>();
            prevMock.Setup(x => x.IsMultiSocket).Returns(true);

            piped.CanBeForged(prevMock.Object).Should().BeTrue();
        }

        [Fact]
        public void PipedStep_CanBeForged_WhenPreviousIsNotMultiSocket_ReturnsFalse()
        {
            // PipedStep is only valid after a SplitterStep or another PipedStep.
            var mock = MockCommand(new BooleanResponse());
            var piped = new PipedStep(new StepSettings(mock.Object, new PromptCommandRequest("pipe")));

            var prevMock = new Mock<IChaineable>();
            prevMock.Setup(x => x.IsMultiSocket).Returns(false);

            piped.CanBeForged(prevMock.Object).Should().BeFalse();
        }

        #endregion

        #region JunctionStep Tests

        [Fact]
        public void JunctionStep_CanBeForged_WhenMultiSocketPrevious_ReturnsTrue()
        {
            // JunctionStep aggregates all parallel branch outputs and continues the single chain.
            var mock = MockCommand(new BooleanResponse());
            var junction = new JunctionStep(
                new StepSettings(mock.Object, new PromptCommandRequest("junction")));

            var prevMock = new Mock<IChaineable>();
            prevMock.Setup(x => x.IsMultiSocket).Returns(true);

            junction.CanBeForged(prevMock.Object).Should().BeTrue();
        }

        [Fact]
        public void JunctionStep_CanBeForged_WhenPreviousIsNotMultiSocket_ReturnsFalse()
        {
            // JunctionStep must follow a SplitterStep or PipedStep — single-socket previous is invalid.
            var mock = MockCommand(new BooleanResponse());
            var junction = new JunctionStep(
                new StepSettings(mock.Object, new PromptCommandRequest("junction")));

            var prevMock = new Mock<IChaineable>();
            prevMock.Setup(x => x.IsMultiSocket).Returns(false);

            junction.CanBeForged(prevMock.Object).Should().BeFalse();
        }

        #endregion

        #region StashedStep Tests

        [Fact]
        public async Task StashedStep_Forge_WhenAtStartOfChain_ExecutesAndPassesRunnerToNext()
        {
            // A StashedStep at the chain head retrieves data and feeds it to subsequent steps.
            var mock = MockCommand(new List<string> { "retrieved-doc-1", "retrieved-doc-2" });
            var runner = MakeRunner();

            // Use the (StepSettings, ChainRunner) constructor for the chain-starting stash.
            var stash = new StashedStep(
                new StepSettings(mock.Object, new PromptCommandRequest("fetch context")), runner);
            var next = new SingleThrowStep(
                new StepSettings(mock.Object, new PromptCommandRequest("use context")));

            stash.Link(next, isForward: true, isTwoWay: true);

            var result = await stash.Forge(null!);

            result.Should().BeSameAs(next);
            next.IsForged.Should().BeTrue();
        }

        [Fact]
        public void StashedStep_CanBeForged_WhenNotLinkedToAnyStep_ReturnsFalse()
        {
            // A stash with no links is neither a first step nor chained — cannot be forged.
            var mock = MockCommand(new List<string> { "data" });
            var runner = MakeRunner();
            var stash = new StashedStep(
                new StepSettings(mock.Object, new PromptCommandRequest("stash")), runner);
            // No Link calls — _next and _prev are both null.

            stash.CanBeForged(null!).Should().BeFalse();
        }

        #endregion

        #region StoredStep Tests

        [Fact]
        public async Task StoredStep_Forge_WhenPreviousOutputMatchesStoredType_StoresAndContinuesChain()
        {
            // StoredStep deserializes the previous output as TPrev and injects it into the request.
            var boolResponse = new BooleanResponse { Answer = true };
            var genericMock = MockCommand(boolResponse);
            var storedMock = MockCommand(boolResponse);
            var runner = MakeRunner();

            var step1 = new SingleThrowStep(
                new StepSettings(genericMock.Object, new PromptCommandRequest("step1")), runner);
            // StoreableCommandRequest<T> is the request type StoredStep casts to when setting .Stored.
            var request = new StoreableCommandRequest<BooleanResponse>("test-collection", "store-step");
            var stored = new StoredStep<BooleanResponse>(
                new StepSettings(storedMock.Object, request));
            var step3 = new SingleThrowStep(
                new StepSettings(genericMock.Object, new PromptCommandRequest("step3")));

            step1.Link(stored, isForward: true, isTwoWay: true);
            stored.Link(step3, isForward: true, isTwoWay: true);

            var result = await step1.Forge(null!);

            result.Should().BeSameAs(step3);
            step3.IsForged.Should().BeTrue();
        }

        [Fact]
        public void StoredStep_CanBeForged_WhenPreviousIsMultiSocket_ReturnsFalse()
        {
            // StoredStep inherits SingleThrowStep.CanBeForged — multi-socket previous is always rejected.
            var mock = MockCommand(new BooleanResponse());
            var request = new StoreableCommandRequest<BooleanResponse>("col", "store");
            var stored = new StoredStep<BooleanResponse>(
                new StepSettings(mock.Object, request));

            var prevMock = new Mock<IChaineable>();
            prevMock.Setup(x => x.IsMultiSocket).Returns(true);

            stored.CanBeForged(prevMock.Object).Should().BeFalse();
        }

        #endregion

        #region ChainStep Factory Method Tests

        [Fact]
        public void ChainStep_ToStash_WhenCommandIsNotSourceableCommand_ThrowsInvalidOperationException()
        {
            // ToStash validates that the configured command is a SourceableCommand subclass.
            var mock = MockCommand(new BooleanResponse());
            var runner = MakeRunner();
            var step = new SingleThrowStep(
                new StepSettings(mock.Object, new PromptCommandRequest("step")), runner);
            // Moq proxy is not a SourceableCommand — ToStash must reject it.
            var stashSettings = new StashSettings(mock.Object, "context-prompt");

            var act = () => step.ToStash(stashSettings);

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*SourceableCommand*");
        }

        [Fact]
        public void ChainStep_ToSmartConditional_WhenCommandIsNotScoredBoolCommand_ThrowsInvalidDataException()
        {
            // ToSmartConditional validates that the command is a ScoredBoolCommand or subclass.
            var mock = MockCommand(new BooleanResponse());
            var runner = MakeRunner();
            var step = new SingleThrowStep(
                new StepSettings(mock.Object, new PromptCommandRequest("step")), runner);
            // Moq proxy is not a ScoredBoolCommand — ToSmartConditional must reject it.
            var settings = new StepSettings(mock.Object, new PromptCommandRequest("smart"));

            var act = () => step.ToSmartConditional(settings);

            act.Should().Throw<InvalidDataException>()
               .WithMessage("*ScoredBoolCommand*");
        }

        #endregion
    }
}
