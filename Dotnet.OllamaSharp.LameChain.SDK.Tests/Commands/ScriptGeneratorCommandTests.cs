using Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using DotnetLlamaSharp.Domain.Services.Inference;
using FluentAssertions;
using Moq;
using OllamaSharp.Models.Chat;
using System.Text.Json;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Commands
{
    public class ScriptGeneratorCommandTests
    {
        private const string _bashWithTwoParameters = "#!/usr/bin/env bash\nset -euo pipefail\nhost=\"$1\"\nport=\"$2\"\necho \"$host:$port\"";

        private static Mock<IScriptExecutor> executorFor(EScriptShell shell)
        {
            var executor = new Mock<IScriptExecutor>();
            executor.Setup(e => e.Shell).Returns(shell);
            executor.Setup(e => e.SaveScriptAsync(It.IsAny<string>(), It.IsAny<EScriptShell?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("/scripts/network-checks/script.sh");
            return executor;
        }

        private static string instructionFor(EScriptShell shell)
            => new TestScriptGeneratorCommand(new Mock<IOllamaInferenceService>().Object, executorFor(shell).Object).Instruction();

        private const string _psWithParams = "param(\n    [Parameter(Mandatory = $true)]\n    [string]$ProcessName,\n    [int]$Top = 5\n)\nWrite-Output $ProcessName";

        private static ScriptGeneratorResponse response(string script, List<ScriptArgument> arguments, string? outputFolder = null)
            => new() { Script = script, Shell = "Bash", Summary = "summary", Arguments = arguments, OutputFolder = outputFolder };

        private static List<ScriptArgument> args(params (string Name, string Value)[] items) => [.. items.Select(i => new ScriptArgument(i.Name, i.Value))];

        [Theory]
        [InlineData(EScriptShell.PowerShell, "TARGET SHELL: PowerShell", "Windows PowerShell 5.1", "set -euo pipefail")]
        [InlineData(EScriptShell.Bash, "TARGET SHELL: Bash", "set -euo pipefail", "Windows PowerShell 5.1")]
        public void Instruction_TargetsTheShellOfTheExecutor(EScriptShell shell, string expectedTarget, string expectedRule, string otherShellRule)
        {
            var instruction = instructionFor(shell);

            instruction.Should().Contain(expectedTarget).And.Contain(expectedRule).And.Contain("# ARGUMENTS");
            instruction.Should().Contain("Write every identifier in English").And.Contain("exactly the same name the script uses");
            instruction.Should().NotContain(otherShellRule, "the prompt only carries the rules of the target shell");
        }

        [Theory]
        [InlineData(EScriptShell.PowerShell)]
        [InlineData(EScriptShell.Bash)]
        public void Instruction_ExamplesMatchTheSchemaAndTheParameterCheck(EScriptShell shell)
        {
            var examples = instructionFor(shell)
                .Split('\n')
                .Where(line => line.StartsWith("{\"script\""))
                .Select(line => JsonSerializer.Deserialize<ScriptGeneratorResponse>(line)!)   // JsonRequired: a renamed or missing field fails here
                .ToList();

            examples.Should().HaveCount(3);
            examples.Should().Contain(e => e.Arguments.Count > 0, "the model needs to see at least one example with arguments");

            foreach (var example in examples)
            {
                example.Shell.Should().Be(shell.ToString());
                example.Summary.Should().NotBeNullOrWhiteSpace();
                ScriptGeneratorCommand.parameterMismatch(example, shell).Should().BeNull("the examples must pass the same check as the generated scripts");
                ScriptGeneratorCommand.SafeFolderName(example.OutputFolder).Should().Be(example.OutputFolder);
            }
        }

        [Fact]
        public void ParameterMismatch_ScriptWithParametersAndNoArguments_IsReported()
        {
            ScriptGeneratorCommand.parameterMismatch(response(_bashWithTwoParameters, []), EScriptShell.Bash)
                .Should().Contain("reads 2 parameter(s) but arguments has 0");
        }

        [Fact]
        public void ParameterMismatch_ArgumentsForAScriptWithoutParameters_IsReported()
        {
            ScriptGeneratorCommand.parameterMismatch(response("echo hello", args(("host", "chromadb"))), EScriptShell.Bash)
                .Should().Contain("does not read any parameter");
        }

        [Fact]
        public void ParameterMismatch_MatchingArguments_IsValid()
        {
            ScriptGeneratorCommand.parameterMismatch(response(_bashWithTwoParameters, args(("host", "chromadb"), ("port", "8000"))), EScriptShell.Bash).Should().BeNull();
        }

        [Fact]
        public void ParameterMismatch_BashArgumentNamedDifferentlyThanItsVariable_IsReported()
        {
            // The case that happened: variables in English, argument names translated
            const string script = "#!/usr/bin/env bash\nfolder=\"$1\"\ndays=\"$2\"\necho \"Uso: $0 <directorio> <dias>\"\nfind \"$folder\" -mtime +\"$days\"";

            ScriptGeneratorCommand.parameterMismatch(response(script, args(("directorio", "/data"), ("dias", "7"))), EScriptShell.Bash)
                .Should().Contain("argument 1 is named 'directorio' but the script stores $1 in the variable 'folder'");

            ScriptGeneratorCommand.parameterMismatch(response(script, args(("folder", "/data"), ("days", "7"))), EScriptShell.Bash).Should().BeNull();
        }

        [Fact]
        public void ParameterMismatch_ArgumentWithoutName_IsReported()
        {
            ScriptGeneratorCommand.parameterMismatch(response(_bashWithTwoParameters, args(("host", "chromadb"), (" ", "8000"))), EScriptShell.Bash)
                .Should().Contain("needs the name");
        }

        [Theory]
        [InlineData("ProcessName")]
        [InlineData("$ProcessName")]
        [InlineData("-processname")]
        public void ParameterMismatch_PowerShellNamesMatchingTheParamBlock_AreValid(string name)
        {
            ScriptGeneratorCommand.parameterMismatch(response(_psWithParams, args((name, "dotnet"))), EScriptShell.PowerShell).Should().BeNull();
        }

        [Fact]
        public void ParameterMismatch_PowerShellUnknownName_IsReported()
        {
            ScriptGeneratorCommand.parameterMismatch(response(_psWithParams, args(("Process", "dotnet"))), EScriptShell.PowerShell)
                .Should().Contain("Process are not parameters").And.Contain("ProcessName, Top");
        }

        [Fact]
        public void ParameterMismatch_PowerShellMissingMandatory_IsReported()
        {
            ScriptGeneratorCommand.parameterMismatch(response(_psWithParams, args(("Top", "3"))), EScriptShell.PowerShell)
                .Should().Contain("ProcessName have no argument");
        }

        [Theory]
        [InlineData("network-checks", "network-checks")]
        [InlineData("/data/Resources/SysAdmin/network-checks/script.sh", "network-checks")]
        [InlineData("C:\\scripts\\java setup\\script.ps1", "java-setup")]
        [InlineData("../../etc", "etc")]
        [InlineData("..", null)]
        [InlineData("   ", null)]
        [InlineData(null, null)]
        public void SafeFolderName_KeepsOnlyAFolderName(string? requested, string? expected)
        {
            ScriptGeneratorCommand.SafeFolderName(requested).Should().Be(expected);
        }

        [Fact]
        public async Task Prompt_WhenArgumentsAreMissing_RetriesOnceAndKeepsTheFixedResponse()
        {
            var ollama = new Mock<IOllamaInferenceService>();
            var prompts = new List<string>();

            ollama.SetupSequence(o => o.CommandPrompt<ScriptGeneratorResponse>(It.IsAny<ChatRequest>(), It.IsAny<CommandPromptValidation<ScriptGeneratorResponse>?>(), It.IsAny<string>(), It.IsAny<Dictionary<string, ToolBinding>?>(), It.IsAny<bool>()))
                .ReturnsAsync(response(_bashWithTwoParameters, [], "/data/Resources/SysAdmin/network-checks/script.sh"))
                .ReturnsAsync(response(_bashWithTwoParameters, args(("$host", "chromadb"), ("port", "8000")), "network-checks"));

            ollama.Invocations.Clear();

            var executor = executorFor(EScriptShell.Bash);
            var command = new ScriptGeneratorCommand(ollama.Object, executor.Object);

            var result = await command.Prompt(new ScriptGeneratorCommandRequest("check chromadb 8000"));

            result.Arguments.Select(a => a.ToString()).Should().Equal("host = chromadb", "port = 8000");   // '$host' normalized
            result.OutputFolder.Should().Be("network-checks");
            result.ScriptPath.Should().Be("/scripts/network-checks/script.sh");

            var requests = ollama.Invocations.Select(i => (ChatRequest)i.Arguments[0]).ToList();
            requests.Should().HaveCount(2);
            requests[1].Messages!.Last().Content.Should().Contain("your previous answer was not valid").And.Contain("reads 2 parameter(s)");

            executor.Verify(e => e.SaveScriptAsync(_bashWithTwoParameters, null, It.IsAny<string?>(), "network-checks", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Prompt_WhenArgumentsAreStillMissingAfterTheRetry_Throws()
        {
            var ollama = new Mock<IOllamaInferenceService>();

            ollama.Setup(o => o.CommandPrompt<ScriptGeneratorResponse>(It.IsAny<ChatRequest>(), It.IsAny<CommandPromptValidation<ScriptGeneratorResponse>?>(), It.IsAny<string>(), It.IsAny<Dictionary<string, ToolBinding>?>(), It.IsAny<bool>()))
                .ReturnsAsync(response(_bashWithTwoParameters, []));

            var executor = executorFor(EScriptShell.Bash);
            var command = new ScriptGeneratorCommand(ollama.Object, executor.Object);

            var act = () => command.Prompt(new ScriptGeneratorCommandRequest("check chromadb 8000"));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*reads 2 parameter(s)*");
            executor.Verify(e => e.SaveScriptAsync(It.IsAny<string>(), It.IsAny<EScriptShell?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private sealed class TestScriptGeneratorCommand(IOllamaInferenceService ollama, IScriptExecutor executor) : ScriptGeneratorCommand(ollama, executor)
        {
            public string Instruction() => getDefaultInstruction();
        }
    }
}
