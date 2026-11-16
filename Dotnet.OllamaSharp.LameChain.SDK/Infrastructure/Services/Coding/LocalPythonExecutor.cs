
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Coding;
using Microsoft.Extensions.Options;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Coding
{
    /// <summary>
    /// Runs the code with the configured python executable (local install, venv, IIS server...).
    /// Never installs packages: it only checks that the requested ones are already installed.
    /// </summary>
    public sealed class LocalPythonExecutor : BasePythonExecutor
    {
        private static readonly TimeSpan _checkTimeout = TimeSpan.FromSeconds(30);

        // importlib.metadata looks packages up by their PyPI name, which is what the generator returns
        private const string _missingDepsScript = """
        import importlib.metadata as m, sys
        missing = []
        for name in sys.argv[1:]:
            try: m.distribution(name)
            except m.PackageNotFoundError: missing.append(name)
        print(",".join(missing))
        """;

        public LocalPythonExecutor(IOptions<PythonExecutionOptions> options) : base(options) {}

        protected override (string FileName, string[] Prefix) getLauncher(IReadOnlyList<PythonDependency> dependencies) => (_options.PythonExe!, []);

        // Version specifiers are ignored: there is nothing to install, so only the package names are checked
        protected override async Task<PythonExecutionResults?> prepareDependenciesAsync(IReadOnlyList<PythonDependency> dependencies, string runDir, CancellationToken ct)
        {
            if (dependencies.Count == 0) return null;

            var check = await runProcess(_options.PythonExe!, ["-c", _missingDepsScript, .. dependencies.Select(d => d.Name)], runDir, _checkTimeout, ct);

            if (check.TimedOut || check.ExitCode != 0)
                return failed("DependencyError", $"Unable to check the installed packages of '{_options.PythonExe}': {tail(check.Stderr)}");

            var missing = check.Stdout.Trim();

            return string.IsNullOrEmpty(missing) ? null
                : failed("DependencyError", $"Not installed in the local Python: {missing}. Local mode does not install packages; use the standard library or the installed ones.");
        }

        // Local mode behaves like the user's own Python: keeps the real profile folders so user site-packages (pip install --user) are visible
        protected override Dictionary<string, string?> buildEnvironment(string homeDir)
            => ProcessRunner.PassThrough(base.buildEnvironment(homeDir), ["HOME", "USERPROFILE", "APPDATA", "LOCALAPPDATA"]);
    }
}
