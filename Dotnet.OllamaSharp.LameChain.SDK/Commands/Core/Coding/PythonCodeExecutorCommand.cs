using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using OllamaSharp.Models.Chat;


namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Coding
{
    /// <summary>
    /// Recieves a python code snippet in string format in the request Prompt
    /// Executes the code and returns the LLM response explaining / summarizing the result
    /// </summary>
    public class PythonCodeExecutorCommand : BasePromptCommand<ChatMessage>
    {
        public PythonCodeExecutorCommand() : base() { }
        public PythonCodeExecutorCommand(IOllamaInferenceService ollama, IPythonExecutor executor, string harnessPath, string workRoot, string? systemMessage = null, CommandSettings? settings = null) 
            : base(ollama, systemMessage, settings) 
        {
            _harnessPath = harnessPath;
            _workRoot = workRoot;
            _executor = executor;
        }

        private readonly IPythonExecutor _executor;
        private readonly string _harnessPath;
        private readonly string _workRoot;

        public override async Task<ChatMessage> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<PythonCodingRequest>(request);

            var req = request as PythonCodingRequest;

            if (string.IsNullOrEmpty(request.Prompt))
                return new ChatMessage(ChatRole.Assistant.ToString(), "Empty input provided, cannot execute Python code.");

            var executionResult = await _executor.ExecuteAsync(request.Prompt);

            var message = request.GetType() == typeof(ChatCommandRequest) ?
                await _ollama.ChatPrompt(request.ToOllamaChat(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings), provider: request.Provider, request.HasTools ? request.Tools : null) :
                await _ollama.GeneratePrompt(request.ToOllamaGenerate(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings), provider: request.Provider);

            return new ChatMessage(message.Role.ToString(), message.Content);
        }

        protected override string getDefaultInstruction() => "You are a helpful assistant. Your task is to return a brief sentence informing of the output of the provided code execution result";

    }
}
