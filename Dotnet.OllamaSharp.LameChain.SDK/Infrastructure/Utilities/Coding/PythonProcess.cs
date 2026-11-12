using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using System.Diagnostics;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Coding
{
    /// <summary>
    /// Launches the child processes of the Python executors (uv / python) with a timeout and a clean environment:
    /// the API environment is NOT inherited because it may contain secrets (API keys, connection strings...) that the generated code could read.
    /// </summary>
    internal static class PythonProcess
    {
        // Proxy / TLS settings some environments need to reach PyPI. Passed through only when the API has them
        private static readonly string[] _passThroughVariables =
            ["HTTP_PROXY", "HTTPS_PROXY", "NO_PROXY", "ALL_PROXY", "http_proxy", "https_proxy", "no_proxy", "all_proxy", "SSL_CERT_FILE", "SSL_CERT_DIR"];

        public static Dictionary<string, string?> BaseEnvironment(string homeDir)
        {
            var env = new Dictionary<string, string?>
            {
                ["HOME"] = homeDir,
                ["MPLCONFIGDIR"] = homeDir,     // matplotlib needs a writable cache folder
                ["MPLBACKEND"] = "Agg",         // matplotlib without trying to open windows
            };

            if (OperatingSystem.IsWindows())
            {
                env["SYSTEMROOT"] = Environment.GetEnvironmentVariable("SYSTEMROOT");   // Python does not even start without it
                env["WINDIR"] = Environment.GetEnvironmentVariable("WINDIR");
                env["USERPROFILE"] = homeDir;
                env["TEMP"] = homeDir;
                env["TMP"] = homeDir;
                env["PATH"] = string.Join(Path.PathSeparator, Environment.GetFolderPath(Environment.SpecialFolder.System), Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            }
            else env["PATH"] = "/usr/local/bin:/usr/bin:/bin";

            foreach (var key in _passThroughVariables)
                if (Environment.GetEnvironmentVariable(key) is { Length: > 0 } value)
                    env[key] = value;

            return env;
        }

        /// <summary>
        /// Runs the process and returns its output. Throws Win32Exception when the executable cannot be started (wrong path)
        /// and OperationCanceledException when <paramref name="ct"/> is cancelled. A timeout kills the whole process tree.
        /// </summary>
        public static async Task<PythonProcessResult> RunAsync(string fileName, IEnumerable<string> args, string workDir, IReadOnlyDictionary<string, string?> environment, TimeSpan timeout, CancellationToken ct = default)
        {
            var psi = new ProcessStartInfo(fileName)
            {
                WorkingDirectory = workDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (var arg in args)
                psi.ArgumentList.Add(arg);

            psi.Environment.Clear();

            foreach (var (key, value) in environment)
                if (value != null)
                    psi.Environment[key] = value;

            using var proc = Process.Start(psi) ?? throw new InvalidOperationException($"{nameof(PythonProcess)} >> unable to start '{fileName}'");

            // Both pipes are drained concurrently so a chatty process never blocks on a full buffer
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            cts.CancelAfter(timeout);

            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }  // already exited

                ct.ThrowIfCancellationRequested();     // cancelled from outside, not a timeout

                return new PythonProcessResult(null, string.Empty, string.Empty, TimedOut: true);
            }

            return new PythonProcessResult(proc.ExitCode, await stdoutTask, await stderrTask, TimedOut: false);
        }
    }
}
