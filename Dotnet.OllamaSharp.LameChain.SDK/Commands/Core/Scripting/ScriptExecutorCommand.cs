using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using Microsoft.Extensions.Options;
using OllamaSharp.Models.Chat;
using System.Text;
using System.Text.Json;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Scripting
{
    public class ScriptExecutorCommand : BasePromptCommand<ChatMessage>
    {
        private readonly IScriptExecutor _executor;
        private readonly ScriptExecutionOptions _options;
        public ScriptExecutorCommand() { }

        public ScriptExecutorCommand(IOllamaInferenceService ollama, IScriptExecutor executor, IOptions<ScriptExecutionOptions> options) : base(ollama)
        {
            _executor = executor;
            _options = options.Value ?? throw new ArgumentNullException($"{nameof(ScriptExecutorCommand)} >> {nameof(ScriptExecutionOptions)} >> Execution options not found");
        }

        public ScriptExecutorCommand(IOllamaInferenceService ollama, IScriptExecutor executor, IOptions<ScriptExecutionOptions> options, string? systemMessage = null, CommandSettings? settings = null) : base(ollama, systemMessage, settings)
        {
            _executor = executor;
            _options = options.Value ?? throw new ArgumentNullException($"{nameof(ScriptExecutorCommand)} >> {nameof(ScriptExecutionOptions)} >> Execution options not found");
        }

        public override async Task<ChatMessage> Prompt(PromptCommandRequest request)
        {
            // req.PathToScript
            // ScriptGeneratorResponse READ FROM JSON
            // CHECK SHELL SCRIPT VS EXECUTOR SHELL & THROW IF MISSMATCH
            // EXECUTION RESULTS
            // return CHAT MESSAGE WITH EXECUTION RESULTS
            validateInputRequest<ScriptExecutionCommandRequest>(request);

            var req = (ScriptExecutionCommandRequest)request;

            if (string.IsNullOrEmpty(req.ScriptPath))
                return new ChatMessage(ChatRole.Assistant.ToString(), "Empty ScriptPath provided, cannot find and execute script.");

            var root = _options.GetWorkRoot();

            if (!req.ScriptPath.StartsWith(root))
                throw new InvalidOperationException($"{nameof(ScriptExecutorCommand)} >> {req.ScriptPath} is outside root");

            if (!File.Exists(req.ScriptPath))
                throw new InvalidOperationException($"{nameof(ScriptExecutorCommand)} >> {req.ScriptPath} not found");

            //var workDir = string.Join("/", req.ScriptPath.Split("/").SkipLast(1));

            var fullPath = Path.GetFullPath(req.ScriptPath);

            var workDir = Path.GetDirectoryName(fullPath);

            var generatedDataFile = $"{workDir}/generator_response.json";

            if (!File.Exists(generatedDataFile))
                throw new InvalidOperationException($"{nameof(ScriptExecutorCommand)} >> {generatedDataFile} not found");

            var generatedData = JsonSerializer.Deserialize<ScriptGeneratorResponse>(await File.ReadAllTextAsync(generatedDataFile));

            if (generatedData.Shell.ToUpper() != $"{_executor.Shell}".ToUpper())
                throw new InvalidOperationException($"{nameof(ScriptExecutorCommand)} >> requested script shell ({generatedData.Shell}) cannot be runned with the current executor ({_executor.Shell})");

            var executionResult = await _executor.ExecuteFileAsync(req.ScriptPath, generatedData.Arguments);

            if (executionResult.Error != null)
                return new ChatMessage(ChatRole.Assistant.ToString(), $"Execution failed with error >> Type: {executionResult.Error.Type}, Message: {executionResult.Error.Message}");

            request.Prompt = getExecutionResultPrompt(executionResult);

            var message = request.GetType() == typeof(ChatCommandRequest) ?
                await _ollama.ChatPrompt(request.ToOllamaChat(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings), provider: request.Provider, request.HasTools ? request.Tools : null) :
                await _ollama.GeneratePrompt(request.ToOllamaGenerate(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings), provider: request.Provider);

            return new ChatMessage(message.Role.ToString(), message.Content);
        }

        protected override string getDefaultInstruction() => "You are a helpful assistant. Your task is to return a brief sentence informing of the output of the provided code execution result. If the provided output includes any generated file, you must include in your response the file names and also the path to the directory containing those files";

        private string getExecutionResultPrompt(ScriptExecutionResult result)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"The {result.Shell} script executed successfully.");
            
            if (!string.IsNullOrWhiteSpace(result.Stdout))
                sb.AppendLine($"Console output:\n{result.Stdout}");

            return sb.ToString();
        }
    }
}
