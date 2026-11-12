
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Coding;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Text.Json;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Coding
{
    /// <summary>
    /// Shared execution flow of every Python mode:
    ///  1. validates the dependencies (stdlib, Excluded / Allowed packages)
    ///  2. creates the execution folder: generated.py (the code string) + lame_harness.py (embedded in the SDK)
    ///  3. mode specific dependency handling (uv installs them, local mode only checks them)
    ///  4. runs the harness, which runs the generated code and writes envelope.json
    ///  5. reads the envelope
    /// Subclasses only decide how Python is launched and what to do with the dependencies.
    /// </summary>
    public abstract class BasePythonExecutor : IPythonExecutor
    {
        private const int _maxOutputLength = 4000;
        private static readonly JsonSerializerOptions _envelopeJsonOptions = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        protected readonly PythonExecutionOptions _options;

        public BasePythonExecutor(IOptions<PythonExecutionOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException($"{nameof(PythonExecutionOptions)} >> No Python Executor options configured in appsettings");
        }

        public async Task<PythonExecutionResult> ExecuteAsync(string code, IReadOnlyList<string> dependencies, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(code))
                return failed("InputError", "No Python code was provided");

            // 1. Dependencies policy
            var selection = PythonDependencyPolicy.Select(dependencies, _options);

            if (selection.Errors.Count > 0)
                return failed("DependencyError", $"Some dependencies were rejected: {string.Join(" | ", selection.Errors)}");

            // 2. Execution folder: the code string becomes generated.py next to the harness
            var runDir = Directory.CreateDirectory(Path.Combine(_options.GetWorkRoot(), $"run_{Guid.NewGuid():N}")).FullName;
            var codePath = Path.Combine(runDir, "generated.py");
            var outDir = Path.Combine(runDir, "out");
            var envelopePath = Path.Combine(runDir, "envelope.json");

            await File.WriteAllTextAsync(codePath, code, ct);

            var harnessPath = PythonHarness.WriteTo(runDir);

            try
            {
                // 3. The only thing that changes between modes
                if (await prepareDependenciesAsync(selection.Accepted, runDir, ct) is { } dependencyError)
                    return dependencyError;

                // 4. Run the harness
                var (fileName, prefix) = getLauncher(selection.Accepted);

                var run = await runProcess(fileName, [.. prefix, "-X", "utf8", harnessPath, codePath, outDir, envelopePath], runDir, _options.ExecutionTimeout, ct);

                if (run.TimedOut)
                    return failed("Timeout", $"The execution exceeded {_options.ExecutionTimeout}") with { TimedOut = true };

                // No envelope: the harness itself died (crash, os._exit, launcher error...)
                if (!File.Exists(envelopePath))
                    return failed("HarnessFailure", $"The execution did not produce any result (exit code {run.ExitCode}). {tail(run.Stderr)}")
                        with { ExitCode = run.ExitCode, Stdout = tail(run.Stdout), Stderr = tail(run.Stderr) };

                // 5. Read the envelope
                var envelope = JsonSerializer.Deserialize<PythonExecutionResult>(await File.ReadAllTextAsync(envelopePath, ct), _envelopeJsonOptions);

                return envelope == null ?
                    failed("HarnessFailure", "The execution produced an empty result envelope") :
                    envelope with { ExitCode = run.ExitCode };
            }
            catch (Win32Exception ex)
            {
                return failed("LauncherError", $"Unable to start the Python process: {ex.Message}. Check the {nameof(PythonExecutionOptions)} paths ({nameof(PythonExecutionOptions.UvPath)} / {nameof(PythonExecutionOptions.PythonExe)})");
            }
            catch (JsonException ex)
            {
                return failed("HarnessFailure", $"Invalid result envelope: {ex.Message}");
            }
        }

        /// <summary>
        /// Mode specific dependency handling. Returns null when the execution can go on, or the error to return otherwise.
        /// </summary>
        protected abstract Task<PythonExecutionResult?> prepareDependenciesAsync(IReadOnlyList<PythonDependency> dependencies, string runDir, CancellationToken ct);

        /// <summary>
        /// Program to start and the arguments placed before "-X utf8 lame_harness.py ..."
        /// </summary>
        protected abstract (string FileName, string[] Prefix) getLauncher(IReadOnlyList<PythonDependency> dependencies);

        /// <summary>
        /// Environment of every child process. The API environment is NOT inherited (it may contain secrets)
        /// </summary>
        protected virtual Dictionary<string, string?> buildEnvironment(string homeDir) => PythonProcess.BaseEnvironment(homeDir);

        protected Task<PythonProcessResult> runProcess(string fileName, IEnumerable<string> args, string workDir, TimeSpan timeout, CancellationToken ct = default)
            => PythonProcess.RunAsync(fileName, args, workDir, buildEnvironment(workDir), timeout, ct);

        protected static PythonExecutionResult failed(string type, string message)
            => new() { Success = false, Error = new PythonError(type, message, string.Empty) };

        protected static string tail(string? text)
            => string.IsNullOrEmpty(text) || text.Length <= _maxOutputLength ? text ?? string.Empty : text[^_maxOutputLength..];
    }
}
