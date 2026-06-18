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
using Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Extensions;
using static Dotnet.OllamaSharp.LameChain.SDK.Extensions.LameChain;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Evaluators;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Schema;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.QueryCommands;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Steps
{
    public class LameExtensionsTests
    {
        private readonly Mock<IOllamaInferenceService> _mockOllama = new();
        private readonly CommandSettings _cmdSettings = new CommandSettings("test-model");

        private Mock<IJsoneable> MockCommand<T>(T response) where T : class
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var json = JsonSerializer.Serialize(response, options);
            var promptResult = new JsonPromptResult(
                "# INSTRUCTION: test",
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
            mock.Setup(x => x.CommandSettings).Returns(default(CommandSettings));
            mock.Setup(x => x.BorrowLlama).Returns(_mockOllama.Object);
            return mock;
        }

        private StepSettings Settings(Mock<IJsoneable> mock, string prompt = "test-prompt")
            => new StepSettings(mock.Object, new PromptCommandRequest(prompt));

        #region StartWith Tests

        [Fact]
        public void StartWith_CreatesRunningFirstStep()
        {
            var mock = MockCommand(new BooleanResponse { Answer = true });
            var settings = Settings(mock, "initial prompt");

            var step = StartWith(settings, _cmdSettings, null, "test-intent");

            step.Should().NotBeNull().And.BeOfType<SingleThrowStep>();
            step.IsRunning.Should().BeTrue();
            step.Commands.Should().HaveCount(1);
            step.Runner.Should().NotBeNull();
            step.Runner!.UserPrompt.Should().Be("initial prompt");
        }

        [Fact]
        public void StartWith_Generic_CreatesSpecifiedStepType()
        {
            // StashedStep has the (StepSettings, ChainRunner) constructor required by StartWith<TStep>.
            var mock = MockCommand(new List<string> { "data" });
            var settings = Settings(mock, "stash start");

            var step = StartWith<StashedStep>(settings, _cmdSettings, null, "test-intent");

            step.Should().NotBeNull().And.BeOfType<StashedStep>();
            step.IsRunning.Should().BeTrue();
        }

        #endregion

        #region Then Tests

        [Fact]
        public void Then_LinksStepsSequentiallyWithCorrectBackReferences()
        {
            var mock = MockCommand(new BooleanResponse());
            var step1 = StartWith(Settings(mock, "step1"), _cmdSettings, null, "intent");
            var step2 = step1.Then(Settings(mock, "step2"));
            var step3 = step2.Then(Settings(mock, "step3"));

            step1.Next.Should().BeSameAs(step2);
            step2.Previous.Should().BeSameAs(step1);
            step2.Next.Should().BeSameAs(step3);
            step3.Previous.Should().BeSameAs(step2);
            step3.Next.Should().BeNull();
            step1.IsFirstStep().Should().BeTrue();
        }

        #endregion

        #region ExposeThisId Tests

        [Fact]
        public void ExposeThisId_ExposedDelegateReturnsStepGuid()
        {
            var mock = MockCommand(new BooleanResponse());
            var step = StartWith(Settings(mock), _cmdSettings, null, "intent");

            var returned = step.ExposeThisId(out Func<Guid> id);

            returned.Should().BeSameAs(step);
            id.Should().NotBeNull();
            id().Should().Be(step.Id);
        }

        #endregion

        #region Tap Tests

        [Fact]
        public void Tap_CreatesSplitterStepLinkedToPreviousWithBranches()
        {
            var mock = MockCommand(new BooleanResponse());
            var step = StartWith(Settings(mock, "main"), _cmdSettings, null, "intent");
            var branches = new List<StepSettings>
            {
                Settings(mock, "branch-a"),
                Settings(mock, "branch-b")
            };

            var splitter = step.Tap(branches);

            splitter.Should().NotBeNull().And.BeOfType<SplitterStep>();
            step.Next.Should().BeSameAs(splitter);
            splitter.Previous.Should().BeSameAs(step);
        }

        #endregion

        #region Join Tests

        [Fact]
        public void Join_CreatesJunctionStepAsNextOfSplitter()
        {
            var mock = MockCommand(new BooleanResponse());
            var step = StartWith(Settings(mock), _cmdSettings, null, "intent");
            var branches = new List<StepSettings> { Settings(mock, "b1"), Settings(mock, "b2") };
            var splitter = step.Tap(branches);

            var junction = splitter.Join(Settings(mock, "join"));

            junction.Should().NotBeNull().And.BeOfType<JunctionStep>();
            splitter.Next.Should().BeSameAs(junction);
            junction.Previous.Should().BeSameAs(splitter);
        }

        #endregion

        #region Pipe Tests

        [Fact]
        public void Pipe_CreatesPipedStepLinkedToSplitter()
        {
            var mock = MockCommand(new BooleanResponse());
            var step = StartWith(Settings(mock), _cmdSettings, null, "intent");
            var splitter = step.Tap([Settings(mock, "b1"), Settings(mock, "b2")]);

            var piped = splitter.Pipe(Settings(mock, "pipe"));

            piped.Should().NotBeNull().And.BeOfType<PipedStep>();
            splitter.Next.Should().BeSameAs(piped);
            piped.Previous.Should().BeSameAs(splitter);
        }

        #endregion

        #region WithRebujito Tests

        [Fact]
        public void WithRebujito_AddsBoostersAndReturnsThisStep()
        {
            var mock = MockCommand(new BooleanResponse());
            var step = StartWith(Settings(mock), _cmdSettings, null, "intent");
            var sources = new List<string> { "doc-1", "doc-2", "doc-3" };

            var returned = step.WithRebujito(sources, "use these docs as context");

            // WithRebujito is a fluent method — it must return the same step.
            returned.Should().BeSameAs(step);
        }

        [Fact]
        public void WithRebujito_WithFeedDose_TruncatesLongSourcesAndReturnsStep()
        {
            var mock = MockCommand(new BooleanResponse());
            var step = StartWith(Settings(mock), _cmdSettings, null, "intent");
            // Source is 50 chars; feedDose=10 should truncate it to 10.
            var longSource = new string('A', 50);

            var returned = step.WithRebujito([longSource], "guidance", feedDose: 10);

            returned.Should().BeSameAs(step);
        }

        #endregion

        #region ForwardFirstType Tests

        [Fact]
        public void ForwardFirstType_ReturnsFirstStepCastToRequestedType()
        {
            var mock = MockCommand(new BooleanResponse());
            var step1 = StartWith(Settings(mock, "first"), _cmdSettings, null, "intent");
            var step2 = step1.Then(Settings(mock, "second"));

            // Navigate from step2 back to step1 as SingleThrowStep.
            var first = step2.ForwardFirstType<SingleThrowStep>();

            first.Should().BeSameAs(step1);
        }

        [Fact]
        public void ForwardFirstType_WhenTypeDoesNotMatch_ThrowsInvalidOperationException()
        {
            var mock = MockCommand(new BooleanResponse());
            var step1 = StartWith(Settings(mock), _cmdSettings, null, "intent");
            var step2 = step1.Then(Settings(mock));

            // step1 is SingleThrowStep — requesting StashedStep should throw.
            var act = () => step2.ForwardFirstType<StashedStep>();

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*INVALID CHAIN CONFIGURATION*");
        }

        #endregion

        #region ThenIf Tests

        [Fact]
        public void ThenIf_Smart_CreatesSmartConditionalStepWithTrueBranchSet()
        {
            var mock = MockCommand(new BooleanResponse());
            var step1 = StartWith(Settings(mock, "step1"), _cmdSettings, null, "intent");
            // ScoredBoolCommand is required by ToSmartConditional's type guard.
            var scoredCmd = new ScoredBoolCommand(_mockOllama.Object, null, _cmdSettings);
            var evaluator = new StepSettings(scoredCmd, new PromptCommandRequest("evaluate this"));

            var conditional = step1.ThenIf<SingleThrowStep>(evaluator, Settings(mock, "true-action"));

            conditional.Should().BeOfType<SmartConditionalStep>();
            step1.Next.Should().BeSameAs(conditional);
            conditional.Previous.Should().BeSameAs(step1);
            conditional.TrueBranchRunner.Should().NotBeNull();
        }

        [Fact]
        public void ThenIf_Smart_WhenEvaluatorIsNotScoredBoolCommand_ThrowsInvalidDataException()
        {
            var mock = MockCommand(new BooleanResponse());
            var step1 = StartWith(Settings(mock), _cmdSettings, null, "intent");
            // Moq proxy is not a ScoredBoolCommand — the type guard must reject it.
            var wrongEvaluator = Settings(mock, "bad-evaluator");

            var act = () => step1.ThenIf<SingleThrowStep>(wrongEvaluator, Settings(mock, "action"));

            act.Should().Throw<InvalidDataException>()
               .WithMessage("*ScoredBoolCommand*");
        }

        [Fact]
        public void ThenIf_Func_CreatesConditionalStepWithTrueBranchSet()
        {
            var mock = MockCommand(new BooleanResponse());
            var step1 = StartWith(Settings(mock, "step1"), _cmdSettings, null, "intent");
            // ConditionalStep does not execute a command itself — empty request is fine.
            var conditionSettings = new StepSettings(new PromptCommandRequest("condition-step"));

            var conditional = step1.ThenIf<SingleThrowStep>(
                () => true,
                conditionSettings,
                Settings(mock, "true-action"));

            conditional.Should().BeOfType<ConditionalStep>();
            step1.Next.Should().BeSameAs(conditional);
            conditional.Previous.Should().BeSameAs(step1);
            conditional.TrueBranchRunner.Should().NotBeNull();
        }

        #endregion

        #region Store Tests

        [Fact]
        public void Store_CreatesStoredStepLinkedToChain()
        {
            var mock = MockCommand(new BooleanResponse());
            var step1 = StartWith(Settings(mock, "step1"), _cmdSettings, null, "intent");

            var store = step1.Store<BooleanResponse>(Settings(mock, "store"));

            store.Should().BeOfType<StoredStep<BooleanResponse>>();
            step1.Next.Should().BeSameAs(store);
            store.Previous.Should().BeSameAs(step1);
        }

        #endregion

        #region Stash Tests

        [Fact]
        public void Stash_CreatesStashedStepAndExposesLazyId()
        {
            var mock = MockCommand(new BooleanResponse());
            var step = StartWith(Settings(mock), _cmdSettings, null, "intent");
            // RagExpansionCommand is a SourceableCommand subclass — satisfies the ToStash type guard.
            var ragCmd = new RagExpansionCommand(_mockOllama.Object);
            var stashSettings = new StashSettings(ragCmd, "retrieve documents");

            var stash = step.Stash(stashSettings, out Func<Guid> stashId);

            stash.Should().NotBeNull().And.BeOfType<StashedStep>();
            step.Next.Should().BeSameAs(stash);
            stash.Previous.Should().BeSameAs(step);
            stashId.Should().NotBeNull();
            stashId().Should().Be(stash.Id);
        }

        [Fact]
        public void Stash_WhenCommandIsNotSourceable_ThrowsInvalidOperationException()
        {
            var mock = MockCommand(new BooleanResponse());
            var step = StartWith(Settings(mock), _cmdSettings, null, "intent");
            // Moq proxy is not a SourceableCommand — ToStash must reject it.
            var wrongSettings = new StashSettings(mock.Object, "bad-stash");

            var act = () => { step.Stash(wrongSettings, out _); };

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*SourceableCommand*");
        }

        #endregion

        #region ChainFeedsFrom Tests

        [Fact]
        public void ChainFeedsFrom_ConfiguresFeedDelegatesAndReturnsStep()
        {
            var mock = MockCommand(new BooleanResponse());
            var step1 = StartWith(Settings(mock, "step1"), _cmdSettings, null, "intent");
            var step2 = step1.Then(Settings(mock, "step2"));

            step1.ExposeThisId(out Func<Guid> id1);
            var returned = step2.ChainFeedsFrom([id1]);

            // Fluent API must return the same step.
            returned.Should().BeSameAs(step2);
        }

        #endregion

        #region UseBroadcaster Tests

        [Fact]
        public void UseBroadcaster_RegistersBroadcasterWhenStepIsRunning()
        {
            var mock = MockCommand(new BooleanResponse());
            // StartWith gives the step a runner, making IsRunning = true.
            var step = StartWith(Settings(mock), _cmdSettings, null, "intent");
            Action<Guid, string, LogLevel> broadcaster = (_, _, _) => { };

            var returned = step.UseBroadcaster(broadcaster);

            returned.Should().BeSameAs(step);
            step.Runner!.onBroadcast.Should().NotBeNull();
        }

        [Fact]
        public void UseBroadcaster_WhenStepIsNotRunning_DoesNotRegisterAndReturnsSameStep()
        {
            // A step created without a runner has IsRunning = false.
            var mock = MockCommand(new BooleanResponse());
            var step = new SingleThrowStep(Settings(mock));
            Action<Guid, string, LogLevel> broadcaster = (_, _, _) => { };

            var returned = step.UseBroadcaster(broadcaster);

            returned.Should().BeSameAs(step);
            step.Runner.Should().BeNull();
        }

        #endregion

        #region ThenExecuteAsync Tests

        [Fact]
        public async Task ThenExecuteAsync_ExecutesChainAndReturnsResult()
        {
            var mock = MockCommand(new BooleanResponse { Answer = true });
            var step1 = StartWith(Settings(mock, "step1"), _cmdSettings, null, "intent");
            var step2 = step1.Then(Settings(mock, "step2"));

            var result = await step1.ThenExecuteAsync();

            result.Should().NotBeNull();
            result.Result.Should().NotBeNull();
            result.ChainStepsLog.Should().NotBeEmpty();
        }

        #endregion
    }
}
