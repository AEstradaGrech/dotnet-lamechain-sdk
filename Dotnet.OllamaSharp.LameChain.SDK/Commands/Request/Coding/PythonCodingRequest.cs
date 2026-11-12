
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Coding
{
    public class PythonCodingRequest : PromptCommandRequest
    {
        public PythonCodingRequest() { }

        // By default it is expected a command with _systemMessage + req.Guidance + core message (db || hardcoded)
        // In case there is no DB || Hardcoded message, change 'isGuidanceAppend' to true so the context is added to the _system instruction
        public PythonCodingRequest(string pythonCode, string? pathToPython = null, bool isGuidanceAppend = true, string? model = null) : base(pythonCode, isGuidanceAppend, model)
        {
            PythonExe = pathToPython;
        }

        public PythonCodingRequest(string pythonCode, string? pathToPython = null, string? guidanceMessage, bool isGuidanceAppend = true, string? model = null) : base(pythonCode, guidanceMessage, isGuidanceAppend, model)
        {
            PythonExe = pathToPython;
        }

        public string? PythonExe { get; init; }

        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    }
}
