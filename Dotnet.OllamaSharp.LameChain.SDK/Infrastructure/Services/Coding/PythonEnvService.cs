using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Coding;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;


namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Coding
{
    /// <summary>
    /// UV mode only. Prepares the uv tooling in background so the API start is not blocked (the first Python download can take a while):
    ///  1. checks that uv is reachable at UvPath
    ///  2. prunes the uv cache (it only grows otherwise)
    ///  3. installs the configured Python version (idempotent: nothing happens if it is already in the cache folder / volume)
    ///  4. pre-installs the WarmupPackages
    /// Failures are logged and never stop the host: executions will report the problem in their own result.
    /// </summary>
    public class PythonEnvService : BackgroundService
    {
        private readonly PythonExecutionOptions _settings;
        private readonly ILogger<PythonEnvService> _logger;

        public PythonEnvService(IOptions<PythonExecutionOptions> settings, ILogger<PythonEnvService> logger)
        {
            _settings = settings?.Value ?? throw new ArgumentNullException($"{nameof(PythonExecutionOptions)} >> No Python UV install options have been configured. Unable to setup the PythonExecutor");
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var toolsDir = Directory.CreateDirectory(UvTooling.ToolsDir(_settings)).FullName;
                var env = UvTooling.BuildEnvironment(_settings, toolsDir);

                Task<ProcessResult> uv(string[] args, TimeSpan timeout)
                    => PythonProcess.RunAsync(_settings.UvPath, args, toolsDir, env, timeout, stoppingToken);

                var version = await uv(["--version"], TimeSpan.FromSeconds(30));

                if (version.ExitCode != 0)
                {
                    _logger.LogError("{Service} >> uv is not working at '{UvPath}': {Error}", nameof(PythonEnvService), _settings.UvPath, version.Stderr);
                    return;
                }

                _logger.LogInformation("{Service} >> {Version} >> cache: {Cache} >> python installs: {Python}",
                    nameof(PythonEnvService), version.Stdout.Trim(), UvTooling.CacheDir(_settings), UvTooling.PythonInstallDir(_settings));

                var prune = await uv(["cache", "prune"], TimeSpan.FromMinutes(1));

                if (prune.ExitCode != 0)
                    _logger.LogWarning("{Service} >> uv cache prune failed: {Error}", nameof(PythonEnvService), prune.Stderr);

                var python = await uv(["python", "install", _settings.PythonVersion], _settings.InstallTimeout);

                if (python.ExitCode != 0)
                {
                    _logger.LogError("{Service} >> unable to install Python {Version}: {Error}", nameof(PythonEnvService), _settings.PythonVersion, python.TimedOut ? "timeout" : python.Stderr);
                    return;
                }

                var warmup = PythonDependencyPolicy.Select(_settings.WarmupPackages, _settings);

                foreach (var error in warmup.Errors)
                    _logger.LogWarning("{Service} >> warmup package skipped: {Error}", nameof(PythonEnvService), error);

                if (warmup.Accepted.Count == 0) return;

                var install = await uv([.. UvTooling.RunPrefix(_settings, warmup.Accepted), "python", "-c", "pass"], _settings.InstallTimeout);

                if (install.ExitCode != 0)
                    _logger.LogWarning("{Service} >> warmup packages install failed: {Error}", nameof(PythonEnvService), install.TimedOut ? "timeout" : install.Stderr);
                else
                    _logger.LogInformation("{Service} >> warmup packages ready: {Packages}", nameof(PythonEnvService), string.Join(", ", warmup.Accepted.Select(d => d.Requirement)));
            }
            catch (Win32Exception ex)
            {
                _logger.LogError(ex, "{Service} >> uv not found at '{UvPath}'. Install uv or fix {Option}", nameof(PythonEnvService), _settings.UvPath, nameof(PythonExecutionOptions.UvPath));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                // An unhandled exception in a BackgroundService stops the whole host: log it instead
                _logger.LogError(ex, "{Service} >> unexpected error while preparing the uv environment", nameof(PythonEnvService));
            }
        }
    }
}
