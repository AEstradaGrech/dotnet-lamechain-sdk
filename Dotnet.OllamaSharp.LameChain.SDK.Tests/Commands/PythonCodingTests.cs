using Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Agents;
using DotnetLlamaSharp.Domain.Services.Inference;
using FluentAssertions;
using Moq;
using System.Text.Json;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Commands
{
    public class PythonAgentTaskTests
    {
        private const string _userRequest = "I have these sales records: North Q1 1200.5, South Q1 980.0. Compute the total.";

        [Fact]
        public void BuildPythonTask_AlwaysIncludesTheOriginalUserRequest()
        {
            // The LLM summarized the task and did not pass any data in inputData
            var task = PythonAgent.BuildPythonTask("Compute the total of the sales records", null, null, _userRequest);

            task.Should().StartWith("Task: Compute the total of the sales records");
            task.Should().Contain("Original user request").And.Contain("North Q1 1200.5, South Q1 980.0");
            task.Should().NotContain("Input data:").And.NotContain("Expected output:");
        }

        [Fact]
        public void BuildPythonTask_IncludesEverySectionTheLlmFilled()
        {
            var task = PythonAgent.BuildPythonTask("Compute the total", "North Q1 1200.5, South Q1 980.0", "a number rounded to 2 decimals", _userRequest);

            task.Should().Contain("Input data: North Q1 1200.5, South Q1 980.0");
            task.Should().Contain("Expected output: a number rounded to 2 decimals");
            task.IndexOf("Task:").Should().BeLessThan(task.IndexOf("Original user request"));
        }

        [Fact]
        public void BuildPythonTask_WithoutUserRequest_OnlyHasTheToolArguments()
        {
            var task = PythonAgent.BuildPythonTask("Compute the total", null, null, null);

            task.Trim().Should().Be("Task: Compute the total");
        }
    }

    public class PythonExecutionExactResultTests
    {
        [Fact]
        public void ExactResult_AddsTheRawResultAndTheFiles()
        {
            var result = new ExecutionResults
            {
                Success = true,
                Result = JsonSerializer.Deserialize<JsonElement>("""{"total": 9557.25, "best_region": "East"}"""),
                Artifacts = [new PythonArtifact("/data/Resources/PythonEnv/run_1/out/chart.png", 1234)],
            };

            var text = PythonCodeExecutorCommand.exactResult(result);

            text.Should().Contain("EXACT RESULT").And.Contain("""{"total": 9557.25, "best_region": "East"}""");
            text.Should().Contain("FILES:").And.Contain("- /data/Resources/PythonEnv/run_1/out/chart.png");
        }

        [Fact]
        public void ExactResult_WithoutResultOrFiles_AddsNothing()
        {
            PythonCodeExecutorCommand.exactResult(new ExecutionResults { Success = true }).Should().BeEmpty();
        }
    }

    public class PythonGeneratorInstructionTests
    {
        [Fact]
        public void Instruction_ForbidsExternalInputsAndInventedData()
        {
            var instruction = new TestPythonGeneratorCommand(new Mock<IOllamaInferenceService>().Object).Instruction();

            instruction.Should().Contain("NEVER read inputs from environment variables, command line arguments or stdin");
            instruction.Should().Contain("NEVER invent sample data, placeholders or TODOs");
        }

        private sealed class TestPythonGeneratorCommand(IOllamaInferenceService ollama) : PythonGeneratorCommand(ollama)
        {
            public string Instruction() => getDefaultInstruction();
        }
    }
}
