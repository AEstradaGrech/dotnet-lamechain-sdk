
namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding
{
    public class PythonExecutionOptions
    {
        // true:  uv manages the Python version and installs the pip dependencies of every execution (Docker or not)
        // false: PythonExe is used as it is and no packages are installed (local install, IIS server, etc.)
        public bool UseUv { get; set; }

        // ---------------- UV MODE ----------------
        public string UvPath { get; set; } = "uv";
        public string PythonVersion { get; set; } = "3.12";
        // uv cache (downloaded packages + cached environments). Fallback: UV_CACHE_DIR env var >> <WorkRoot>/.uv/cache
        public string? UvCacheDir { get; set; }
        // uv managed Python installs. Fallback: UV_PYTHON_INSTALL_DIR env var >> <WorkRoot>/.uv/python
        public string? UvPythonInstallDir { get; set; }
        // Installed in background on startup (PythonEnvService) so the first executions do not wait for them
        public List<string>? WarmupPackages { get; set; }

        // ---------------- LOCAL MODE ----------------
        // Path to the python executable. Recommended: a venv (venv\Scripts\python.exe) instead of the global install
        public string? PythonExe { get; set; }

        // ---------------- COMMON ----------------
        // Every execution gets its own folder here (generated code, harness, envelope and artifacts). Empty: <temp>/lamechain-python
        public string WorkRoot { get; set; } = "";
        public TimeSpan InstallTimeout { get; set; } = TimeSpan.FromMinutes(3);
        public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromSeconds(60);

        // Package age exclusion policy (uv --exclude-newer): ignores packages and versions uploaded in the last N days. 0 to disable
        public int MinPackageAgeDays { get; set; } = 14;
        // Packages that will never be installed
        public List<string>? ExcludedPackages { get; set; }
        // Strict package policy (null or empty to allow any pip package)
        public List<string>? AllowedPackages { get; set; }

        internal string GetWorkRoot()
            => string.IsNullOrWhiteSpace(WorkRoot) ? Path.Combine(Path.GetTempPath(), "lamechain-python") : Path.GetFullPath(WorkRoot);
    }
}
