using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Coding;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Infrastructure
{
    public class PythonDependencyPolicyTests
    {
        [Fact]
        public void Select_DropsStandardLibraryModules()
        {
            var selection = PythonDependencyPolicy.Select(["json", "os", "datetime", "pandas"], new PythonExecutionOptions());

            selection.Errors.Should().BeEmpty();
            selection.Accepted.Select(d => d.Name).Should().Equal("pandas");
        }

        [Fact]
        public void Select_KeepsVersionSpecifiersAndExtras()
        {
            var selection = PythonDependencyPolicy.Select(["pandas >= 2.0, <3", "requests[socks]"], new PythonExecutionOptions());

            selection.Errors.Should().BeEmpty();
            selection.Accepted.Should().Equal(
                new PythonDependency("pandas", "pandas>=2.0,<3"),
                new PythonDependency("requests", "requests[socks]"));
        }

        [Theory]
        [InlineData("--index-url=https://evil.example/simple")]
        [InlineData("git+https://github.com/someone/something")]
        [InlineData("https://example.com/package.whl")]
        [InlineData("./local/package")]
        [InlineData("pandas; python_version > '3.8'")]
        [InlineData("pandas numpy")]
        public void Select_RejectsAnythingThatIsNotAPlainRequirement(string requirement)
        {
            var selection = PythonDependencyPolicy.Select([requirement], new PythonExecutionOptions());

            selection.Accepted.Should().BeEmpty();
            selection.Errors.Should().ContainSingle();
        }

        [Fact]
        public void Select_DeduplicatesByNormalizedName()
        {
            var selection = PythonDependencyPolicy.Select(["Scikit_Learn", "scikit-learn", "scikit.learn"], new PythonExecutionOptions());

            selection.Accepted.Should().ContainSingle().Which.Name.Should().Be("Scikit_Learn");
        }

        [Fact]
        public void Select_RejectsExcludedPackagesUsingNormalizedNames()
        {
            var options = new PythonExecutionOptions { ExcludedPackages = ["scikit-learn"] };

            var selection = PythonDependencyPolicy.Select(["scikit_learn>=1.4", "numpy"], options);

            selection.Accepted.Select(d => d.Name).Should().Equal("numpy");
            selection.Errors.Should().ContainSingle().Which.Should().Contain("excluded");
        }

        [Fact]
        public void Select_WithAllowList_RejectsUnlistedPackages()
        {
            var options = new PythonExecutionOptions { AllowedPackages = ["pandas", "matplotlib"] };

            var selection = PythonDependencyPolicy.Select(["pandas", "requests"], options);

            selection.Accepted.Select(d => d.Name).Should().Equal("pandas");
            selection.Errors.Should().ContainSingle().Which.Should().Contain("requests");
        }

        [Fact]
        public void Select_WithoutAllowList_AllowsAnyPackage()
        {
            var options = new PythonExecutionOptions { AllowedPackages = [] };

            var selection = PythonDependencyPolicy.Select(["pandas", "requests"], options);

            selection.Errors.Should().BeEmpty();
            selection.Accepted.Should().HaveCount(2);
        }

        [Fact]
        public void Select_IgnoresNullAndBlankEntries()
        {
            var selection = PythonDependencyPolicy.Select(null, new PythonExecutionOptions());
            var blanks = PythonDependencyPolicy.Select(["", "   "], new PythonExecutionOptions());

            selection.Accepted.Should().BeEmpty();
            blanks.Accepted.Should().BeEmpty();
            blanks.Errors.Should().BeEmpty();
        }
    }

    public class UvToolingTests
    {
        [Fact]
        public void RunPrefix_IncludesSafetyFlagsAndOneWithPerDependency()
        {
            var options = new PythonExecutionOptions { PythonVersion = "3.12", MinPackageAgeDays = 14 };

            var args = UvTooling.RunPrefix(options, [new PythonDependency("pandas", "pandas>=2"), new PythonDependency("matplotlib", "matplotlib")]);

            args.Take(3).Should().Equal("run", "--python", "3.12");
            args.Should().Contain(["--no-project", "--no-config", "--no-build", "--exclude-newer"]);
            args.Should().ContainInConsecutiveOrder("--with", "pandas>=2");
            args.Should().ContainInConsecutiveOrder("--with", "matplotlib");

            var cutoff = DateTime.Parse(args[Array.IndexOf(args, "--exclude-newer") + 1], null, System.Globalization.DateTimeStyles.AdjustToUniversal);
            cutoff.Should().Be(DateTime.UtcNow.Date.AddDays(-14));
        }

        [Fact]
        public void RunPrefix_WithoutMinPackageAge_OmitsExcludeNewer()
        {
            var args = UvTooling.RunPrefix(new PythonExecutionOptions { MinPackageAgeDays = 0 }, []);

            args.Should().NotContain("--exclude-newer");
            args.Should().NotContain("--with");
        }

        [Fact]
        public void BuildEnvironment_DoesNotLeakTheApiEnvironment()
        {
            Environment.SetEnvironmentVariable("LAMECHAIN_TEST_SECRET", "do-not-leak");

            try
            {
                var options = new PythonExecutionOptions { WorkRoot = Path.GetTempPath(), UvCacheDir = "/cache", UvPythonInstallDir = "/python" };

                var env = UvTooling.BuildEnvironment(options, "/home/run");

                env.Should().NotContainKey("LAMECHAIN_TEST_SECRET");
                env["UV_CACHE_DIR"].Should().Be("/cache");
                env["UV_PYTHON_INSTALL_DIR"].Should().Be("/python");
                env["HOME"].Should().Be("/home/run");
                env["MPLBACKEND"].Should().Be("Agg");
            }
            finally
            {
                Environment.SetEnvironmentVariable("LAMECHAIN_TEST_SECRET", null);
            }
        }
    }

    public class PythonHarnessTests
    {
        [Fact]
        public void WriteTo_WritesTheEmbeddedHarnessIntoTheRunFolder()
        {
            var runDir = Directory.CreateTempSubdirectory("lamechain-harness-test");

            try
            {
                var path = PythonHarness.WriteTo(runDir.FullName);

                path.Should().Be(Path.Combine(runDir.FullName, "lame_harness.py"));
                File.ReadAllText(path).Should().Contain("def run_harness(");
            }
            finally
            {
                runDir.Delete(recursive: true);
            }
        }
    }

    public class LocalPythonExecutorTests
    {
        [Fact]
        public async Task ExecuteAsync_WithEmptyCode_ReturnsInputError()
        {
            var executor = new LocalPythonExecutor(Options.Create(new PythonExecutionOptions { PythonExe = "python" }));

            var result = await executor.ExecuteAsync("   ", [], TestContext.Current.CancellationToken);

            result.Success.Should().BeFalse();
            result.Error!.Type.Should().Be("InputError");
        }

        [Fact]
        public async Task ExecuteAsync_WithRejectedDependency_ReturnsDependencyErrorWithoutRunningPython()
        {
            var executor = new LocalPythonExecutor(Options.Create(new PythonExecutionOptions { PythonExe = "this-python-does-not-exist" }));

            var result = await executor.ExecuteAsync("result = 1", ["--index-url=https://evil.example"], TestContext.Current.CancellationToken);

            result.Error!.Type.Should().Be("DependencyError");
        }

        [Fact]
        public async Task ExecuteAsync_WithWrongPythonPath_ReturnsLauncherError()
        {
            var workRoot = Directory.CreateTempSubdirectory("lamechain-executor-test");

            try
            {
                var executor = new LocalPythonExecutor(Options.Create(new PythonExecutionOptions
                {
                    PythonExe = Path.Combine(workRoot.FullName, "missing", "python.exe"),
                    WorkRoot = workRoot.FullName
                }));

                var result = await executor.ExecuteAsync("result = 1", [], TestContext.Current.CancellationToken);

                result.Success.Should().BeFalse();
                result.Error!.Type.Should().Be("LauncherError");
            }
            finally
            {
                workRoot.Delete(recursive: true);
            }
        }
    }
}
