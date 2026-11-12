
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Coding
{
    public class PythonCodingRequest : PromptCommandRequest
    {
        public PythonCodingRequest() { }

        // By default it is expected a command with _systemMessage + req.Guidance + core message (db || hardcoded)
        // In case there is no DB || Hardcoded message, change 'isGuidanceAppend' to true so the context is added to the _system instruction
        public PythonCodingRequest(PythonGeneratorResponse parameters, string resultsPrompt, bool isGuidanceAppend = true, string? model = null) : base(resultsPrompt, isGuidanceAppend, model)
        {
            GeneratorParams = parameters;
        }

        public PythonCodingRequest(PythonGeneratorResponse parameters, string resultsPrompt, string? guidanceMessage, bool isGuidanceAppend = true, string? model = null) : base(resultsPrompt, guidanceMessage, isGuidanceAppend, model)
        {
            GeneratorParams = parameters;
        }

        public PythonGeneratorResponse GeneratorParams { get; set; }

        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    }
}
