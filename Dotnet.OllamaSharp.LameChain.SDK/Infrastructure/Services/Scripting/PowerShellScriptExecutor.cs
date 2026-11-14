using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Scripting;
using Microsoft.Extensions.Options;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Scripting
{
    /// <summary>
    /// Runs .ps1 scripts with powershell.exe (Windows PowerShell 5.1) or pwsh (PowerShell 7):
    ///  -NoProfile: the result does not depend on the user's profile
    ///  -NonInteractive: anything that asks for input fails instead of hanging until the timeout
    ///  -ExecutionPolicy Bypass: only for this process, so unsigned scripts run even with the default Restricted policy (no system setting is changed)
    /// </summary>
    public sealed class PowerShellScriptExecutor : BaseScriptExecutor
    {
        // Added at the top of every script (it is saved in the file, so what is reviewed is exactly what runs).
        // UTF-8 output: Windows PowerShell writes redirected output in the console code page otherwise (accents get broken).
        // No progress bars: with redirected output they end up in stderr as noise.
        internal const string Header = """
            # --- LameChain: UTF-8 output and no progress bars (they break redirected output) ---
            [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
            $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
            $ProgressPreference = 'SilentlyContinue'
            # ---
            """;

        public PowerShellScriptExecutor(IOptions<ScriptExecutionOptions> options) : base(options) { }

        public override EScriptShell Shell => EScriptShell.PowerShell;

        protected override string fileExtension => ".ps1";

        protected override (string FileName, string[] Args) getLauncher(string scriptPath)
            => (_options.ResolvePowerShellPath(), ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptPath]);

        protected override string prepareScript(string script) => $"{Header}{Environment.NewLine}{script}";

        // Windows PowerShell 5.1 reads .ps1 files without BOM in the ANSI code page: with the BOM accents are not broken
        protected override Encoding scriptEncoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
    }
}
