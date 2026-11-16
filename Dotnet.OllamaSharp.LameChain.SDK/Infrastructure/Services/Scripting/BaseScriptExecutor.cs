using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Scripting;
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

        public async Task<string> SaveScriptAsync(string script, EScriptShell? desiredExtension = null, string? generatedJson = null, string? folder = null, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(script);

            var root = _options.GetWorkRoot();

            // Checked before creating anything: an absolute or '..' folder must not create directories outside WorkRoot
            var dir = Path.GetFullPath(Path.Combine(root, folder ?? $"run_{Guid.NewGuid():N}"));

            if (!dir.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"'{folder}' is outside {root}");

            Directory.CreateDirectory(dir);

            // The script may be for another shell (to run it on the user's machine): extension, content and encoding of that shell
            var target = desiredExtension is EScriptShell.Bash or EScriptShell.PowerShell ? desiredExtension.Value : Shell;

            var path = Path.Combine(dir, $"{ScriptFileName}{ScriptFormat.Extension(target)}");

            var content = target == Shell ? prepareScript(script) : ScriptFormat.Prepare(target, script);
            var encoding = target == Shell ? scriptEncoding : ScriptFormat.FileEncoding(target);

            await File.WriteAllTextAsync(path, content, encoding, ct);

            if (!string.IsNullOrEmpty(generatedJson))
                await File.WriteAllTextAsync(Path.Combine(dir, $"generator_response.json"), generatedJson, ct);

            return path;
        }

        public async Task<ScriptExecutionResult> ExecuteAsync(string script, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(script))
                return failed("InputError", "No script was provided");

            return await ExecuteFileAsync(await SaveScriptAsync(script, ct: ct), ct: ct);
        }

        public async Task<ScriptExecutionResult> ExecuteFileAsync(string scriptPath, IReadOnlyList<ScriptArgument>? arguments = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
                return failed("FileNotFound", $"Script not found: '{scriptPath}'") with { ScriptPath = scriptPath };

            var fullPath = Path.GetFullPath(scriptPath);
            var workDir = Path.GetDirectoryName(fullPath)!;
            var (fileName, args) = getLauncher(fullPath);
            var watch = Stopwatch.StartNew();

            // Each argument is a separate process argument (ArgumentList): values are never parsed by a shell, so no injection
            var scriptArgs = await scriptArguments(fullPath, arguments ?? [], ct);

            try
            {
                var run = await ProcessRunner.RunAsync(fileName, [.. args, .. scriptArgs], workDir, buildEnvironment(workDir), _options.ExecutionTimeout, ct, _options.MaxOutputLength);

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
        /// Arguments placed after the script path. By default the values in order (positional: Bash $1, $2...)
        /// </summary>
        protected virtual Task<IReadOnlyList<string>> scriptArguments(string scriptPath, IReadOnlyList<ScriptArgument> arguments, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>([.. arguments.Select(a => a.Value)]);

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
