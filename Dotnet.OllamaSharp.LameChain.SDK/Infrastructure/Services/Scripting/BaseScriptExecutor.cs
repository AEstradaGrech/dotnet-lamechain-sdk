using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Coding;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Scripting
{
    /// <summary>
    /// Shared flow of every shell: save the script (subclasses decide extension, encoding and header), run it with ProcessRunner
    /// in its own folder, and map the output to a ScriptExecutionResult. Subclasses only decide how the shell is launched.
    /// </summary>
    public abstract class BaseScriptExecutor : IScriptExecutor
    {
        protected const string ScriptFileName = "script";

        // System variables scripts need to behave like in a normal terminal (tools in PATH, user profile folders...).
        // Paths and names only: the rest of the API environment (API keys, connection strings) is never passed
        private static readonly string[] _systemVariables =
        [
            "PATH", "HOME", "USER", "LOGNAME", "SHELL", "LANG", "LC_ALL", "TERM", "TZ", "HOSTNAME",
            "PATHEXT", "COMSPEC", "USERPROFILE", "USERNAME", "USERDOMAIN", "COMPUTERNAME", "HOMEDRIVE", "HOMEPATH",
            "APPDATA", "LOCALAPPDATA", "PROGRAMDATA", "ALLUSERSPROFILE", "PUBLIC", "TEMP", "TMP", "SYSTEMDRIVE",
            "PROGRAMFILES", "PROGRAMFILES(X86)", "PROGRAMW6432", "COMMONPROGRAMFILES", "COMMONPROGRAMFILES(X86)",
            "PROCESSOR_ARCHITECTURE", "NUMBER_OF_PROCESSORS", "OS",
        ];

        protected readonly ScriptExecutionOptions _options;

        public BaseScriptExecutor(IOptions<ScriptExecutionOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException($"{nameof(ScriptExecutionOptions)} >> No script executor options configured");
        }

        public abstract EScriptShell Shell { get; }

        public async Task<string> SaveScriptAsync(string script, string? folder = null, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(script);

            var dir = Directory.CreateDirectory(folder ?? Path.Combine(_options.GetWorkRoot(), $"run_{Guid.NewGuid():N}")).FullName;
            var path = Path.Combine(dir, $"{ScriptFileName}{fileExtension}");

            await File.WriteAllTextAsync(path, prepareScript(script), scriptEncoding, ct);

            return path;
        }

        public async Task<ScriptExecutionResult> ExecuteAsync(string script, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(script))
                return failed("InputError", "No script was provided");

            return await ExecuteFileAsync(await SaveScriptAsync(script, ct: ct), ct);
        }

        public async Task<ScriptExecutionResult> ExecuteFileAsync(string scriptPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
                return failed("FileNotFound", $"Script not found: '{scriptPath}'") with { ScriptPath = scriptPath };

            var fullPath = Path.GetFullPath(scriptPath);
            var workDir = Path.GetDirectoryName(fullPath)!;
            var (fileName, args) = getLauncher(fullPath);
            var watch = Stopwatch.StartNew();

            try
            {
                var run = await ProcessRunner.RunAsync(fileName, args, workDir, buildEnvironment(workDir), _options.ExecutionTimeout, ct, _options.MaxOutputLength);

                return new ScriptExecutionResult
                {
                    Success = !run.TimedOut && run.ExitCode == 0,
                    ExitCode = run.ExitCode,
                    Stdout = run.Stdout,
                    Stderr = run.Stderr,
                    TimedOut = run.TimedOut,
                    DurationMs = watch.ElapsedMilliseconds,
                    ScriptPath = fullPath,
                    Shell = Shell,
                    Error = run.TimedOut ? new ScriptError("Timeout", $"The script exceeded {_options.ExecutionTimeout} and was stopped. Stdout / Stderr contain its partial output") : null,
                };
            }
            catch (Win32Exception ex)
            {
                return failed("LauncherError", $"Unable to start {Shell} ('{fileName}'): {ex.Message}. Check the {nameof(ScriptExecutionOptions)} paths ({nameof(ScriptExecutionOptions.BashPath)} / {nameof(ScriptExecutionOptions.PowerShellPath)})")
                    with { ScriptPath = fullPath };
            }
        }

        protected abstract string fileExtension { get; }

        /// <summary>
        /// Program to start and its arguments to run <paramref name="scriptPath"/>
        /// </summary>
        protected abstract (string FileName, string[] Args) getLauncher(string scriptPath);

        /// <summary>
        /// Final content of the script file (shell header, line endings...). What is saved is exactly what runs
        /// </summary>
        protected virtual string prepareScript(string script) => script;

        protected virtual Encoding scriptEncoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary>
        /// Generic ProcessRunner environment + the system variables + the configured PassThroughVariables
        /// </summary>
        protected virtual Dictionary<string, string?> buildEnvironment(string workDir)
            => ProcessRunner.PassThrough(ProcessRunner.PassThrough(ProcessRunner.BaseEnvironment(workDir), _systemVariables), _options.PassThroughVariables ?? []);

        protected ScriptExecutionResult failed(string type, string message)
            => new() { Success = false, Shell = Shell, Error = new ScriptError(type, message) };
    }
}
