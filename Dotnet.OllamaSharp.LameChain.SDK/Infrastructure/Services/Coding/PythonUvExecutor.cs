using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Coding;
using Microsoft.Extensions.Options;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Coding
{
    /// <summary>
    /// uv manages the Python version and the dependencies of every execution through cached ephemeral environments
    /// ("uv run --with pandas --with matplotlib python ..."): nothing to uninstall, different sets of packages never collide
    /// and concurrent executions are safe.
    /// </summary>
    public sealed class PythonUvExecutor : BasePythonExecutor
    {
        public PythonUvExecutor(IOptions<PythonExecutionOptions> options) : base(options) {}

        protected override (string FileName, string[] Prefix) getLauncher(IReadOnlyList<PythonDependency> dependencies)
            => (_options.UvPath, [.. UvTooling.RunPrefix(_options, dependencies), "python"]);

        // Install phase: same "uv run" arguments as the run phase, so the run phase finds the environment already cached
        // and installing (the first time can take a while) does not eat the execution timeout
        protected override async Task<PythonExecutionResult?> prepareDependenciesAsync(IReadOnlyList<PythonDependency> dependencies, string runDir, CancellationToken ct)
        {
            if (dependencies.Count == 0) return null;

            var install = await runProcess(_options.UvPath, [.. UvTooling.RunPrefix(_options, dependencies), "python", "-c", "pass"], runDir, _options.InstallTimeout, ct);

            if (install.TimedOut)
                return failed("DependencyError", $"Installing {string.Join(", ", dependencies.Select(d => d.Requirement))} exceeded {_options.InstallTimeout}");

            return install.ExitCode == 0 ? null : failed("DependencyError", $"Unable to install the dependencies: {tail(install.Stderr)}");
        }

        protected override Dictionary<string, string?> buildEnvironment(string homeDir) => UvTooling.BuildEnvironment(_options, homeDir);
    }
}
