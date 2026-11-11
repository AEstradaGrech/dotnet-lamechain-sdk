
namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding
{
    public class PythonExecutionOptions
    {
        public string PythonExe { get; init; }
        public string HarnessPath { get; init; } = "";
        public string WorkRoot { get; set; } = "";
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    }
}
