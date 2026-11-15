using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Scripting
{
    public class ScriptGeneratorCommandRequest : PromptCommandRequest
    {
        public EScriptShell? RequestedShell { get; private set; }
        public ScriptGeneratorCommandRequest() { }

        // By default it is expected a command with _systemMessage + req.Guidance + core message (db || hardcoded)
        // In case there is no DB || Hardcoded message, change 'isGuidanceAppend' to true so the context is added to the _system instruction
        public ScriptGeneratorCommandRequest(string message, EScriptShell? requestedShell = null, bool isGuidanceAppend = true, string? model = null) : base(message, isGuidanceAppend, model)
        {
            RequestedShell = requestedShell;
        }

        public ScriptGeneratorCommandRequest(string message, string? guidanceMessage, EScriptShell? requestedShell = null, bool isGuidanceAppend = true, string? model = null) : base(message, guidanceMessage, isGuidanceAppend, model)
        {
            RequestedShell = requestedShell;
        }
    }
}
