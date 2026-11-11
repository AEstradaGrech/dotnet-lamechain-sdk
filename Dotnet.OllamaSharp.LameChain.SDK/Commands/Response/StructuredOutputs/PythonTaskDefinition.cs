using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs
{
    public class PythonTaskDefinition
    {
        public string Goal { get; set; } // high-level description of what the Python code is supposed to achieve
        public List<string> InputFiles { get; set; }          // absolute paths to input files that the code may need to read from
        public EPythonOutputKind ExpectedOutput { get; set; } // Value | File | ValueAndFile
        public string ResultShape { get; set; } // string describing the expected shape of the result (e.g., "list of dicts", "single value", etc.)
    }
}
