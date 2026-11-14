using Dotnet.OllamaSharp.LameChain.SDK.Extensions;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Coding;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Infrastructure
{
    public class ProcessRunnerTests
    {
        [Fact]
        public async Task ReadCappedAsync_KeepsShortOutputUntouched()
        {
            var text = await ProcessRunner.ReadCappedAsync(new StringReader("hello\nworld"), 100);

            text.Should().Be("hello\nworld");
        }

        [Fact]
        public async Task ReadCappedAsync_KeepsBeginningAndEndOfLongOutput()
        {
            var output = "BEGIN" + new string('x', 50_000) + "END";

            var text = await ProcessRunner.ReadCappedAsync(new StringReader(output), 1_000);

            text.Should().StartWith("BEGIN").And.EndWith("END").And.Contain("characters omitted");
            text.Length.Should().BeLessThan(1_100);
        }
    }

    public class ProcessEnvironmentTests : IDisposable
    {
        private const string _secret = "LAMECHAIN_ENV_TEST_SECRET";
        private const string _allowed = "LAMECHAIN_ENV_TEST_ALLOWED";

        public ProcessEnvironmentTests()
        {
            Environment.SetEnvironmentVariable(_secret, "do-not-leak");
            Environment.SetEnvironmentVariable(_allowed, "pass-me");
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_secret, null);
            Environment.SetEnvironmentVariable(_allowed, null);
        }

        [Fact]
        public void BaseEnvironment_IsGenericAndDoesNotLeakTheApiEnvironment()
        {
            var env = ProcessRunner.BaseEnvironment("/home/run");

            env["HOME"].Should().Be("/home/run");
            env.Should().ContainKey("PATH");
            env.Should().NotContainKey(_secret);
            env.Should().NotContainKey("MPLBACKEND");
        }

        [Fact]
        public void PythonExecutors_AddTheMatplotlibVariables()
        {
            var env = new EnvPythonExecutor(Options.Create(new PythonExecutionOptions())).Env("/home/run");

            env["MPLBACKEND"].Should().Be("Agg");
            env["MPLCONFIGDIR"].Should().Be("/home/run");
            env.Should().NotContainKey(_secret);
        }

        [Fact]
        public void ScriptExecutors_PassTheRealPathAndTheConfiguredVariablesOnly()
        {
            var options = new ScriptExecutionOptions { PassThroughVariables = [_allowed] };

            var env = new EnvScriptExecutor(Options.Create(options)).Env("/scripts/run");

            env["PATH"].Should().Be(Environment.GetEnvironmentVariable("PATH"));
            env[_allowed].Should().Be("pass-me");
            env.Should().NotContainKey(_secret);
        }

        private sealed class EnvPythonExecutor(IOptions<PythonExecutionOptions> options) : BasePythonExecutor(options)
        {
            public Dictionary<string, string?> Env(string homeDir) => buildEnvironment(homeDir);
            protected override Task<ExecutionResults?> prepareDependenciesAsync(IReadOnlyList<PythonDependency> dependencies, string runDir, CancellationToken ct) => Task.FromResult<ExecutionResults?>(null);
            protected override (string FileName, string[] Prefix) getLauncher(IReadOnlyList<PythonDependency> dependencies) => ("python", []);
        }

        private sealed class EnvScriptExecutor(IOptions<ScriptExecutionOptions> options) : BaseScriptExecutor(options)
        {
            public Dictionary<string, string?> Env(string workDir) => buildEnvironment(workDir);
            public override EScriptShell Shell => EScriptShell.Bash;
            protected override string fileExtension => ".sh";
            protected override (string FileName, string[] Args) getLauncher(string scriptPath) => ("bash", [scriptPath]);
        }
    }

    public class ScriptFilesTests : IDisposable
    {
        private readonly DirectoryInfo _workRoot = Directory.CreateTempSubdirectory("lamechain-scripts-test");

        public void Dispose() => _workRoot.Delete(recursive: true);

        private IOptions<ScriptExecutionOptions> options(Action<ScriptExecutionOptions>? configure = null)
        {
            var value = new ScriptExecutionOptions { WorkRoot = _workRoot.FullName };
            configure?.Invoke(value);
            return Options.Create(value);
        }

        [Fact]
        public async Task Bash_SavesWithLfLineEndingsAndWithoutBom()
        {
            var path = await new BashScriptExecutor(options()).SaveScriptAsync("echo one\r\necho two\r\n", ct: TestContext.Current.CancellationToken);

            path.Should().EndWith("script.sh");
            Path.GetDirectoryName(path)!.Should().StartWith(Path.Combine(_workRoot.FullName, "run_"));

            var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            bytes.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF });
            Encoding.UTF8.GetString(bytes).Should().Be("echo one\necho two\n");
        }

        [Fact]
        public async Task PowerShell_SavesWithBomAndTheEncodingHeader()
        {
            var path = await new PowerShellScriptExecutor(options()).SaveScriptAsync("Write-Output 'ñandú'", ct: TestContext.Current.CancellationToken);

            path.Should().EndWith("script.ps1");

            var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            bytes.Take(3).Should().Equal(new byte[] { 0xEF, 0xBB, 0xBF });

            var content = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            content.Should().StartWith(PowerShellScriptExecutor.Header).And.EndWith("Write-Output 'ñandú'");
        }

        [Fact]
        public async Task SaveScriptAsync_UsesTheGivenFolder()
        {
            var folder = Path.Combine(_workRoot.FullName, "SysAdmin", "k3f9a2");

            var path = await new BashScriptExecutor(options()).SaveScriptAsync("echo hi", folder, TestContext.Current.CancellationToken);

            path.Should().Be(Path.Combine(folder, "script.sh"));
        }

        [Fact]
        public async Task ExecuteAsync_WithEmptyScript_ReturnsInputError()
        {
            var result = await new BashScriptExecutor(options()).ExecuteAsync("  ", TestContext.Current.CancellationToken);

            result.Success.Should().BeFalse();
            result.Error!.Type.Should().Be("InputError");
        }

        [Fact]
        public async Task ExecuteFileAsync_WithMissingFile_ReturnsFileNotFound()
        {
            var result = await new BashScriptExecutor(options()).ExecuteFileAsync(Path.Combine(_workRoot.FullName, "nope.sh"), TestContext.Current.CancellationToken);

            result.Error!.Type.Should().Be("FileNotFound");
        }

        [Fact]
        public async Task ExecuteAsync_WithWrongShellPath_ReturnsLauncherError()
        {
            var executor = new BashScriptExecutor(options(o => o.BashPath = Path.Combine(_workRoot.FullName, "missing", "bash")));

            var result = await executor.ExecuteAsync("echo hi", TestContext.Current.CancellationToken);

            result.Success.Should().BeFalse();
            result.Error!.Type.Should().Be("LauncherError");
            result.ScriptPath.Should().EndWith("script.sh");
        }
    }

    /// <summary>
    /// Real executions: PowerShell only on Windows, Bash only outside Windows (skipped otherwise)
    /// </summary>
    public class ScriptExecutionTests : IDisposable
    {
        private const string _secret = "LAMECHAIN_SCRIPT_TEST_SECRET";
        private const string _allowed = "LAMECHAIN_SCRIPT_TEST_ALLOWED";
        private readonly DirectoryInfo _workRoot = Directory.CreateTempSubdirectory("lamechain-scripts-run-test");

        public ScriptExecutionTests()
        {
            Environment.SetEnvironmentVariable(_secret, "do-not-leak");
            Environment.SetEnvironmentVariable(_allowed, "pass-me");
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_secret, null);
            Environment.SetEnvironmentVariable(_allowed, null);
            _workRoot.Delete(recursive: true);
        }

        private IScriptExecutor executor(EScriptShell shell, TimeSpan? timeout = null)
        {
            var options = Options.Create(new ScriptExecutionOptions
            {
                WorkRoot = _workRoot.FullName,
                ExecutionTimeout = timeout ?? TimeSpan.FromSeconds(30),
                PassThroughVariables = [_allowed],
            });

            return shell == EScriptShell.PowerShell ? new PowerShellScriptExecutor(options) : new BashScriptExecutor(options);
        }

        private static void skipIfNotAvailable(EScriptShell shell)
        {
            if (shell == EScriptShell.PowerShell)
                Assert.SkipUnless(OperatingSystem.IsWindows(), "PowerShell execution tests run on Windows only");
            else
                Assert.SkipWhen(OperatingSystem.IsWindows(), "Bash execution tests run outside Windows only");
        }

        private static string script(EScriptShell shell, string powershell, string bash) => shell == EScriptShell.PowerShell ? powershell : bash;

        [Theory]
        [InlineData(EScriptShell.PowerShell)]
        [InlineData(EScriptShell.Bash)]
        public async Task ReturnsUtf8OutputAndTheExitCode(EScriptShell shell)
        {
            skipIfNotAvailable(shell);

            var result = await executor(shell).ExecuteAsync(script(shell,
                "Write-Output 'Configuración lista: ñandú'\nexit 3",
                "echo 'Configuración lista: ñandú'\nexit 3"), TestContext.Current.CancellationToken);

            result.Stdout.Should().Contain("Configuración lista: ñandú");
            result.ExitCode.Should().Be(3);
            result.Success.Should().BeFalse();
            result.Error.Should().BeNull("a script that runs and fails is not an executor error");
            result.Shell.Should().Be(shell);
        }

        [Theory]
        [InlineData(EScriptShell.PowerShell)]
        [InlineData(EScriptShell.Bash)]
        public async Task WritesErrorsToStderr(EScriptShell shell)
        {
            skipIfNotAvailable(shell);

            var result = await executor(shell).ExecuteAsync(script(shell,
                "Write-Error 'boom happened'",
                "echo 'boom happened' >&2"), TestContext.Current.CancellationToken);

            result.Stderr.Should().Contain("boom happened");
        }

        [Theory]
        [InlineData(EScriptShell.PowerShell)]
        [InlineData(EScriptShell.Bash)]
        public async Task DoesNotLeakTheApiEnvironment(EScriptShell shell)
        {
            skipIfNotAvailable(shell);

            var result = await executor(shell).ExecuteAsync(script(shell,
                $"Write-Output \"secret=[$env:{_secret}] allowed=[$env:{_allowed}]\"",
                $"echo \"secret=[${_secret}] allowed=[${_allowed}]\""), TestContext.Current.CancellationToken);

            result.Success.Should().BeTrue();
            result.Stdout.Should().Contain("secret=[] allowed=[pass-me]");
        }

        [Theory]
        [InlineData(EScriptShell.PowerShell)]
        [InlineData(EScriptShell.Bash)]
        public async Task Timeout_StopsTheScriptAndKeepsThePartialOutput(EScriptShell shell)
        {
            skipIfNotAvailable(shell);

            var result = await executor(shell, TimeSpan.FromSeconds(5)).ExecuteAsync(script(shell,
                "Write-Output 'started'\nStart-Sleep -Seconds 60\nWrite-Output 'never'",
                "echo started\nsleep 60\necho never"), TestContext.Current.CancellationToken);

            result.TimedOut.Should().BeTrue();
            result.Success.Should().BeFalse();
            result.Error!.Type.Should().Be("Timeout");
            result.Stdout.Should().Contain("started").And.NotContain("never");
            result.DurationMs.Should().BeLessThan(20_000);
        }
    }

    public class WithScriptExecutorTests
    {
        private static ServiceProvider build(Dictionary<string, string?> settings)
        {
            var services = new ServiceCollection();
            services.WithScriptExecutor(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
            return services.BuildServiceProvider();
        }

        [Theory]
        [InlineData("Bash", typeof(BashScriptExecutor))]
        [InlineData("PowerShell", typeof(PowerShellScriptExecutor))]
        public void RegistersTheConfiguredShell(string shell, Type expected)
        {
            using var provider = build(new() { ["ScriptExecutionOptions:Shell"] = shell });
            using var scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<IScriptExecutor>().Should().BeOfType(expected);
        }

        [Fact]
        public void WithoutSection_PicksTheShellOfTheOperatingSystem()
        {
            using var provider = build(new() { ["Other:Key"] = "value" });
            using var scope = provider.CreateScope();

            var executor = scope.ServiceProvider.GetRequiredService<IScriptExecutor>();

            executor.Shell.Should().Be(OperatingSystem.IsWindows() ? EScriptShell.PowerShell : EScriptShell.Bash);
        }
    }
}
