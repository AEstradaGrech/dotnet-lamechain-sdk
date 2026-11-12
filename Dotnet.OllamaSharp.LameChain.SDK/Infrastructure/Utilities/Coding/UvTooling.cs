using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using System.Collections;
using System.Globalization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Coding
{
    /// <summary>
    /// Shared uv settings for the PythonUvExecutor and the PythonEnvService, so both use the same cache and Python installs
    /// </summary>
    internal static class UvTooling
    {
        public static string ToolsDir(PythonExecutionOptions options) => Path.Combine(options.GetWorkRoot(), ".uv");

        public static string CacheDir(PythonExecutionOptions options)
            => firstNonEmpty(options.UvCacheDir, Environment.GetEnvironmentVariable("UV_CACHE_DIR")) ?? Path.Combine(ToolsDir(options), "cache");

        public static string PythonInstallDir(PythonExecutionOptions options)
            => firstNonEmpty(options.UvPythonInstallDir, Environment.GetEnvironmentVariable("UV_PYTHON_INSTALL_DIR")) ?? Path.Combine(ToolsDir(options), "python");

        /// <summary>
        /// Clean base environment + the uv settings of the API (UV_* variables, e.g. a private index) + explicit cache and Python folders.
        /// Without explicit folders uv would use a cache inside HOME (the execution folder) and reinstall everything on every execution.
        /// Note: uv passes its environment to the python process, so the generated code can read these variables too.
        /// </summary>
        public static Dictionary<string, string?> BuildEnvironment(PythonExecutionOptions options, string homeDir)
        {
            var env = PythonProcess.BaseEnvironment(homeDir);

            foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
                if (variable.Key is string key && key.StartsWith("UV_", StringComparison.OrdinalIgnoreCase))
                    env[key] = variable.Value as string;

            env["UV_CACHE_DIR"] = CacheDir(options);
            env["UV_PYTHON_INSTALL_DIR"] = PythonInstallDir(options);
            env["PYTHONNOUSERSITE"] = "1";   // no user site-packages: nothing outside the uv environment gets imported

            return env;
        }

        /// <summary>
        /// "uv run" arguments shared by the install and the run phases. With the same arguments uv reuses the cached environment.
        ///  --no-project / --no-config: ignore any pyproject.toml / uv.toml found in the execution folder or its parents
        ///  --no-build: wheels only, no package code is executed while installing
        ///  --exclude-newer: ignores packages and versions uploaded in the last MinPackageAgeDays (UTC day granularity to keep the cache stable)
        /// </summary>
        public static string[] RunPrefix(PythonExecutionOptions options, IEnumerable<PythonDependency> dependencies)
        {
            List<string> args = ["run", "--python", options.PythonVersion, "--no-project", "--no-config", "--no-build"];

            if (options.MinPackageAgeDays > 0)
                args.AddRange(["--exclude-newer", DateTime.UtcNow.Date.AddDays(-options.MinPackageAgeDays).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)]);

            foreach (var dependency in dependencies)
                args.AddRange(["--with", dependency.Requirement]);

            return [.. args];
        }

        private static string? firstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }
}
