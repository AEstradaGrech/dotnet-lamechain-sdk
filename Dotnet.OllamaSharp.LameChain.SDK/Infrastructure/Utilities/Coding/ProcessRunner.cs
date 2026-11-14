using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using System.Diagnostics;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Coding
{
    /// <summary>
    /// Runs processes on the machine where the API runs (the container when the API is in Docker, the host otherwise),
    /// with a timeout and a clean environment: the API environment is NOT inherited because it may contain secrets
    /// (API keys, connection strings...) that generated code or scripts could read.
    ///
    /// Only the generic variables live here. Each executor family adds its own on top of <see cref="BaseEnvironment"/>
    /// in its buildEnvironment() override (Python: matplotlib / uv variables, scripts: the system variables they need).
    /// </summary>
    internal static class ProcessRunner
    {
        public const int DefaultMaxOutputLength = 1_000_000;

        // Proxy / TLS settings some environments need to reach the network. Passed through only when the API has them
        private static readonly string[] _proxyVariables =
            ["HTTP_PROXY", "HTTPS_PROXY", "NO_PROXY", "ALL_PROXY", "http_proxy", "https_proxy", "no_proxy", "all_proxy", "SSL_CERT_FILE", "SSL_CERT_DIR"];

        // Time given to the pipes to deliver the partial output after a timeout kill
        private static readonly TimeSpan _drainTimeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Minimal environment: HOME (and TEMP / USERPROFILE on Windows) pointing to <paramref name="homeDir"/>, a minimal PATH,
        /// the Windows variables every process needs and the proxy settings.
        /// </summary>
        public static Dictionary<string, string?> BaseEnvironment(string homeDir)
        {
            var env = new Dictionary<string, string?> { ["HOME"] = homeDir };

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

            PassThrough(env, _proxyVariables);

            return env;
        }

        /// <summary>
        /// Copies the given variables from the API environment when they exist (overwriting the current values)
        /// </summary>
        public static Dictionary<string, string?> PassThrough(Dictionary<string, string?> env, IEnumerable<string> variables)
        {
            foreach (var key in variables)
                if (Environment.GetEnvironmentVariable(key) is { Length: > 0 } value)
                    env[key] = value;

            return env;
        }

        /// <summary>
        /// Runs the process and returns its output (UTF-8). Throws Win32Exception when the executable cannot be started (wrong path)
        /// and OperationCanceledException when <paramref name="ct"/> is cancelled. A timeout kills the whole process tree and
        /// returns whatever the process wrote until then.
        /// Each output keeps at most <paramref name="maxOutputLength"/> characters: the beginning and the end, with a marker in between.
        /// </summary>
        public static async Task<ProcessResult> RunAsync(string fileName, IEnumerable<string> args, string workDir, IReadOnlyDictionary<string, string?> environment,
            TimeSpan timeout, CancellationToken ct = default, int maxOutputLength = DefaultMaxOutputLength)
        {
            var psi = new ProcessStartInfo(fileName)
            {
                WorkingDirectory = workDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            foreach (var arg in args)
                psi.ArgumentList.Add(arg);

            psi.Environment.Clear();

            foreach (var (key, value) in environment)
                if (value != null)
                    psi.Environment[key] = value;

            using var proc = Process.Start(psi) ?? throw new InvalidOperationException($"{nameof(ProcessRunner)} >> unable to start '{fileName}'");

            // Both pipes are drained concurrently so a chatty process never blocks on a full buffer
            var stdoutTask = ReadCappedAsync(proc.StandardOutput, maxOutputLength);
            var stderrTask = ReadCappedAsync(proc.StandardError, maxOutputLength);

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

                // Killing the process closes the pipes: give them a moment to deliver the partial output
                await Task.WhenAny(Task.WhenAll(stdoutTask, stderrTask), Task.Delay(_drainTimeout, CancellationToken.None));

                return new ProcessResult(null, completedOrEmpty(stdoutTask), completedOrEmpty(stderrTask), TimedOut: true);
            }

            return new ProcessResult(proc.ExitCode, await stdoutTask, await stderrTask, TimedOut: false);
        }

        // Keeps the first and the last half of maxLength characters, so the memory stays bounded whatever the process writes
        internal static async Task<string> ReadCappedAsync(TextReader reader, int maxLength)
        {
            var headLimit = maxLength / 2;
            var tailLimit = maxLength - headLimit;
            var head = new StringBuilder();
            var tail = new StringBuilder();
            var buffer = new char[4096];
            long omitted = 0;
            int read;

            while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                var offset = 0;

                if (head.Length < headLimit)
                {
                    var toHead = Math.Min(read, headLimit - head.Length);
                    head.Append(buffer, 0, toHead);
                    offset = toHead;
                }

                if (offset == read) continue;

                tail.Append(buffer, offset, read - offset);

                if (tail.Length > tailLimit * 2)
                {
                    var excess = tail.Length - tailLimit;
                    omitted += excess;
                    tail.Remove(0, excess);
                }
            }

            if (tail.Length > tailLimit)
            {
                var excess = tail.Length - tailLimit;
                omitted += excess;
                tail.Remove(0, excess);
            }

            return omitted == 0 ?
                head.Append(tail).ToString() :
                $"{head}\n\n... [{omitted} characters omitted] ...\n\n{tail}";
        }

        private static string completedOrEmpty(Task<string> task) => task.IsCompletedSuccessfully ? task.Result : string.Empty;
    }
}
