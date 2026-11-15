using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using Dotnet.OllamaSharp.LameChain.SDK.Extensions;
using OllamaSharp.Models.Chat;
using System.Text;
using System.Text.Json;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Coding
{
    /// <summary>
    /// Recieves a python code snippet in string format in the request Prompt
    /// Executes the code and returns the LLM response explaining / summarizing the result
    /// </summary>
    public class PythonCodeExecutorCommand : BasePromptCommand<ChatMessage>
    {
        private readonly IPythonExecutor _executor;

        public PythonCodeExecutorCommand() : base() { }
        public PythonCodeExecutorCommand(IOllamaInferenceService ollama, IPythonExecutor executor, string? systemMessage = null, CommandSettings? settings = null)
            : base(ollama, systemMessage, settings)
        {
            _executor = executor ?? throw new ArgumentNullException($"{nameof(IPythonExecutor)} >> No Python executor provided. Register it on startup with {nameof(StartupConfiguration.WithPythonExecutor)}()");
        }

        public override async Task<ChatMessage> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<PythonCodingRequest>(request);

            var req = (PythonCodingRequest)request;

            if (string.IsNullOrWhiteSpace(req.GeneratorParams?.Code))
                return new ChatMessage(ChatRole.Assistant.ToString(), "Empty input provided, cannot execute Python code.");

            // uv / local python, dependencies install and harness: all handled by the executor configured on startup
            var executionResult = await _executor.ExecuteAsync(req.GeneratorParams.Code, req.GeneratorParams.Dependencies ?? []);

            if(executionResult.Error != null)
                return new ChatMessage(ChatRole.Assistant.ToString(), $"Execution failed with error >> Type: {executionResult.Error.Type}, Message: {executionResult.Error.Message}, Traceback: {executionResult.Error.Traceback}");

            request.Prompt = getExecutionResultPrompt(executionResult);

            var message = request.GetType() == typeof(ChatCommandRequest) ?
                await _ollama.ChatPrompt(request.ToOllamaChat(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings), provider: request.Provider, request.HasTools ? request.Tools : null) :
                await _ollama.GeneratePrompt(request.ToOllamaGenerate(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings), provider: request.Provider);

            return new ChatMessage(message.Role.ToString(), $"{message.Content}{exactResult(executionResult)}");
        }

        // The summary above is written by an LLM. The exact values and file paths go along with it, so whoever reads this
        // (an agent, a chain, the user) does not have to trust the rewording
        internal static string exactResult(ExecutionResults result)
        {
            var sb = new StringBuilder();

            if (result.Result is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } value)
                sb.AppendLine().AppendLine()
                  .AppendLine("EXACT RESULT (report these values exactly as they are, do not recompute or round them):")
                  .AppendLine(value.GetRawText());

            if (result.Artifacts.Count > 0)
            {
                sb.AppendLine().AppendLine("FILES:");

                foreach (var artifact in result.Artifacts)
                    sb.AppendLine($"- {artifact.Path}");
            }

            return sb.ToString().TrimEnd();
        }

        protected override string getDefaultInstruction() => "You are a helpful assistant. Your task is to return a brief sentence informing of the output of the provided code execution result. If the provided output includes any generated file, you must include in your response the file names and also the path to the directory containing those files";

        private string getExecutionResultPrompt(ExecutionResults result)
        {
            StringBuilder sb = new StringBuilder();

            if (result.Result is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } value)
                sb.AppendLine($"The code executed successfully. The result ({result.ResultType}) is: {value.GetRawText()}");
            else
                sb.AppendLine("The code executed successfully but it did not return any result.");

            if(result.Artifacts.Count > 0)
            {
                sb.AppendLine("The following artifacts were generated:");

                foreach (var artifact in result.Artifacts)
                    sb.AppendLine($"- {artifact.Path} ({artifact.SizeBytes} bytes)");
            }

            if (!string.IsNullOrWhiteSpace(result.Stdout))
                sb.AppendLine($"Console output:\n{result.Stdout}");

            return sb.ToString();
        }

    }
}
