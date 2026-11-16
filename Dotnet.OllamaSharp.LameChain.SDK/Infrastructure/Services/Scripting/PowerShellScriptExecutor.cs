using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Scripting;
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
        // Added to every script, right after its param() block if it has one (see ScriptFormat)
        internal const string Header = ScriptFormat.PowerShellHeader;

        public PowerShellScriptExecutor(IOptions<ScriptExecutionOptions> options) : base(options) { }

        public override EScriptShell Shell => EScriptShell.PowerShell;

        protected override string fileExtension => ".ps1";

        protected override (string FileName, string[] Args) getLauncher(string scriptPath)
            => (_options.ResolvePowerShellPath(), ["-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptPath]);

        protected override string prepareScript(string script) => ScriptFormat.PreparePowerShell(script);

        private static readonly string[] _switchOn = ["true", "1", "yes", "on", "$true"];

        // By name (-Name value): it does not depend on the order and works with optional parameters.
        // [switch] parameters are flags: '-Name' when the value is true, nothing when it is false.
        // Positional when the script has no param() block (it reads $args) or an argument has no name
        protected override async Task<IReadOnlyList<string>> scriptArguments(string scriptPath, IReadOnlyList<ScriptArgument> arguments, CancellationToken ct)
        {
            if (arguments.Count == 0) return [];

            var declared = ScriptFormat.PowerShellParameters(await File.ReadAllTextAsync(scriptPath, ct));

            if (declared == null || arguments.Any(a => string.IsNullOrWhiteSpace(a.Name)))
                return [.. arguments.Select(a => a.Value)];

            var result = new List<string>();

            foreach (var argument in arguments)
            {
                var name = argument.Name.Trim().TrimStart('$', '-');
                var parameter = declared.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (parameter?.IsSwitch == true)
                {
                    if (_switchOn.Contains(argument.Value.Trim(), StringComparer.OrdinalIgnoreCase))
                        result.Add($"-{parameter.Name}");

                    continue;
                }

                // An unknown name is passed as it is: PowerShell fails with a clear 'parameter cannot be found' error
                result.AddRange([$"-{parameter?.Name ?? name}", argument.Value]);
            }

            return result;
        }

        protected override Encoding scriptEncoding => ScriptFormat.FileEncoding(EScriptShell.PowerShell);
    }
}
