using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Coding;
using Microsoft.Extensions.Logging;
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
        private readonly IMaliciousPackageChecker? _maliciousChecker;
        private readonly ILogger<PythonUvExecutor>? _logger;

        public PythonUvExecutor(IOptions<PythonExecutionOptions> options, IMaliciousPackageChecker? maliciousChecker = null, ILogger<PythonUvExecutor>? logger = null)
            : base(options)
        {
            _maliciousChecker = maliciousChecker;
            _logger = logger;
        }

        protected override (string FileName, string[] Prefix) getLauncher(IReadOnlyList<PythonDependency> dependencies)
            => (_options.UvPath, [.. UvTooling.RunPrefix(_options, dependencies), "python"]);

        protected override async Task<ExecutionResults?> prepareDependenciesAsync(IReadOnlyList<PythonDependency> dependencies, string runDir, CancellationToken ct)
        {
            if (dependencies.Count == 0) return null;

            // 1. Known malicious packages, before anything is downloaded
            if (await checkMaliciousPackagesAsync(dependencies, ct) is { } maliciousError)
                return maliciousError;

            // 2. Install phase: same "uv run" arguments as the run phase, so the run phase finds the environment already cached
            //    and installing (the first time can take a while) does not eat the execution timeout
            var install = await runProcess(_options.UvPath, [.. UvTooling.RunPrefix(_options, dependencies), "python", "-c", "pass"], runDir, _options.InstallTimeout, ct);

            if (install.TimedOut)
                return failed("DependencyError", $"Installing {string.Join(", ", dependencies.Select(d => d.Requirement))} exceeded {_options.InstallTimeout}");

            return install.ExitCode == 0 ? null : failed("DependencyError", $"Unable to install the dependencies: {tail(install.Stderr)}");
        }

        // Python variables (base) + uv variables
        protected override Dictionary<string, string?> buildEnvironment(string homeDir) => UvTooling.ApplyUvVariables(base.buildEnvironment(homeDir), _options);

        private async Task<ExecutionResults?> checkMaliciousPackagesAsync(IReadOnlyList<PythonDependency> dependencies, CancellationToken ct)
        {
            if (!_options.CheckMaliciousPackages) return null;

            if (_maliciousChecker == null)
                return _options.RequireMaliciousPackagesCheck ?
                    failed("DependencyError", $"No {nameof(IMaliciousPackageChecker)} registered and {nameof(PythonExecutionOptions.RequireMaliciousPackagesCheck)} is enabled. Installation aborted") :
                    null;

            var report = await _maliciousChecker.CheckAsync(dependencies, ct);

            if (report.Error != null)
            {
                if (_options.RequireMaliciousPackagesCheck)
                    return failed("DependencyError", $"Unable to check the dependencies against the malicious packages database ({report.Error}). Installation aborted");

                _logger?.LogWarning("{Executor} >> malicious packages check skipped, the database is not available: {Error}", nameof(PythonUvExecutor), report.Error);

                return null;
            }

            if (report.Malicious.Count == 0) return null;

            _logger?.LogWarning("{Executor} >> malicious packages blocked: {Packages}", nameof(PythonUvExecutor),
                string.Join(", ", report.Malicious.Select(p => $"{p.Name} ({string.Join(", ", p.AdvisoryIds)})")));

            return failed("MaliciousPackage", string.Join(" | ", report.Malicious.Select(p =>
                $"'{p.Name}' is reported as a malicious package ({string.Join(", ", p.AdvisoryIds)}). Do not use it: check the package name (typosquatting) or use another library")));
        }
    }
}
