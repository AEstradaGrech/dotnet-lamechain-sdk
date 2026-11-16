using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Scripting
{
    public class ScriptExecutionCommandRequest : PromptCommandRequest
    {
        public string ScriptPath { get; private set; }

        public ScriptExecutionCommandRequest() { }

        // By default it is expected a command with _systemMessage + req.Guidance + core message (db || hardcoded)
        // In case there is no DB || Hardcoded message, change 'isGuidanceAppend' to true so the context is added to the _system instruction
        public ScriptExecutionCommandRequest(string scriptPath, string resultsPrompt = "", bool isGuidanceAppend = true, string? model = null) : base(resultsPrompt, isGuidanceAppend, model)
        {
            ScriptPath = scriptPath;
        }

        public ScriptExecutionCommandRequest(string resultsPrompt, string scriptPath, string? guidanceMessage, bool isGuidanceAppend = true, string? model = null) : base(resultsPrompt, guidanceMessage, isGuidanceAppend, model)
        {
            ScriptPath = scriptPath;
        }
    }
}
