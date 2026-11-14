using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Scripting
{
    // Problems of the executor itself. A script that runs and fails is not an error here: Success = false + ExitCode + Stderr
    public sealed record ScriptError(string Type, string Message);   // Type: InputError | FileNotFound | LauncherError | Timeout

    public sealed record ScriptExecutionResult
    {
        public bool Success { get; init; }          // the script ran, finished in time and returned exit code 0
        public int? ExitCode { get; init; }         // null when it did not run or was killed by timeout
        public string Stdout { get; init; } = "";   // partial output when TimedOut
        public string Stderr { get; init; } = "";
        public bool TimedOut { get; init; }
        public long DurationMs { get; init; }
        public string? ScriptPath { get; init; }
        public EScriptShell Shell { get; init; }
        public ScriptError? Error { get; init; }
    }
}
