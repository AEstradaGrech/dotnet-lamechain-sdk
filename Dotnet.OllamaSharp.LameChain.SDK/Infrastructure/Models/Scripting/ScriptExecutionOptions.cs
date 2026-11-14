using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Scripting
{
    public class ScriptExecutionOptions
    {
        // Auto: PowerShell on Windows, Bash anywhere else
        public EScriptShell Shell { get; set; } = EScriptShell.Auto;
        public string BashPath { get; set; } = "bash";
        // Null: powershell.exe (Windows PowerShell 5.1, always installed) on Windows, pwsh anywhere else
        public string? PowerShellPath { get; set; }

        // Every script gets its own folder here (script + anything it writes in its working directory). Empty: <temp>/lamechain-scripts
        public string WorkRoot { get; set; } = "";
        public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromMinutes(2);
        // Max characters kept from stdout and from stderr (beginning + end). Scripts can be very chatty and the output goes to an LLM
        public int MaxOutputLength { get; set; } = 20_000;

        // Extra API environment variables passed to the scripts. The API environment is NOT inherited (it may contain secrets):
        // only the system variables scripts need (PATH, user profile folders...) plus these ones
        public List<string>? PassThroughVariables { get; set; }

        internal EScriptShell ResolveShell()
            => Shell != EScriptShell.Auto ? Shell : OperatingSystem.IsWindows() ? EScriptShell.PowerShell : EScriptShell.Bash;

        internal string ResolvePowerShellPath()
            => !string.IsNullOrWhiteSpace(PowerShellPath) ? PowerShellPath : OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh";

        internal string GetWorkRoot()
            => string.IsNullOrWhiteSpace(WorkRoot) ? Path.Combine(Path.GetTempPath(), "lamechain-scripts") : Path.GetFullPath(WorkRoot);
    }
}
