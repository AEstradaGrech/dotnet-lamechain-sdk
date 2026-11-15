using Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using DotnetLlamaSharp.Domain.Services.Inference;
using FluentAssertions;
using Moq;
using System.Text.Json;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Commands
{
    public class ScriptGeneratorCommandTests
    {
        private static string instructionFor(EScriptShell shell)
        {
            var executor = new Mock<IScriptExecutor>();
            executor.Setup(e => e.Shell).Returns(shell);

            return new TestScriptGeneratorCommand(new Mock<IOllamaInferenceService>().Object, executor.Object).Instruction();
        }

        [Theory]
        [InlineData(EScriptShell.PowerShell, "TARGET SHELL: PowerShell", "Windows PowerShell 5.1", "set -euo pipefail")]
        [InlineData(EScriptShell.Bash, "TARGET SHELL: Bash", "set -euo pipefail", "Windows PowerShell 5.1")]
        public void Instruction_TargetsTheShellOfTheExecutor(EScriptShell shell, string expectedTarget, string expectedRule, string otherShellRule)
        {
            var instruction = instructionFor(shell);

            instruction.Should().Contain(expectedTarget).And.Contain(expectedRule);
            instruction.Should().NotContain(otherShellRule, "the prompt only carries the rules of the target shell");
        }

        [Theory]
        [InlineData(EScriptShell.PowerShell)]
        [InlineData(EScriptShell.Bash)]
        public void Instruction_ExamplesMatchTheResponseSchema(EScriptShell shell)
        {
            var examples = instructionFor(shell)
                .Split('\n')
                .Where(line => line.StartsWith("{\"script\""))
                .ToList();

            examples.Should().HaveCount(2);

            foreach (var example in examples)
            {
                // JsonRequired properties: a renamed or missing field in the examples fails here
                var response = JsonSerializer.Deserialize<ScriptGeneratorResponse>(example)!;

                response.Script.Should().NotBeNullOrWhiteSpace();
                response.Shell.Should().Be(shell.ToString());
                response.Summary.Should().NotBeNullOrWhiteSpace();
                response.Arguments.Should().BeEmpty();
            }
        }

        private sealed class TestScriptGeneratorCommand(IOllamaInferenceService ollama, IScriptExecutor executor) : ScriptGeneratorCommand(ollama, executor)
        {
            public string Instruction() => getDefaultInstruction();
        }
    }
}
