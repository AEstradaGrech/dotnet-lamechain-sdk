using Dotnet.OllamaSharp.LameChain.SDK.Extensions;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Coding;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Text;

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

    public class OsvMaliciousPackageCheckerTests
    {
        private static readonly PythonDependency[] _dependencies =
            [new PythonDependency("Credential_SDK", "Credential_SDK"), new PythonDependency("numpy", "numpy>=2")];

        [Fact]
        public async Task CheckAsync_ReportsOnlyMaliciousAdvisories()
        {
            var handler = new StubHandler(HttpStatusCode.OK, """
                {"results":[
                    {"vulns":[{"id":"MAL-2024-5000","modified":"2024-10-24T01:01:58Z"}]},
                    {"vulns":[{"id":"GHSA-2fc2-6r4j-p65h","modified":"2024-10-01T21:25:39Z"},{"id":"PYSEC-2017-1","modified":"2023-11-08T03:58:54Z"}]}
                ]}
                """);

            var report = await new OsvMaliciousPackageChecker(new StubFactory(handler)).CheckAsync(_dependencies, TestContext.Current.CancellationToken);

            report.Error.Should().BeNull();
            report.Malicious.Should().ContainSingle();
            report.Malicious[0].Name.Should().Be("Credential_SDK");
            report.Malicious[0].AdvisoryIds.Should().Equal("MAL-2024-5000");

            handler.Requests.Should().ContainSingle().Which.Should().Be("https://osv.test/v1/querybatch");
            handler.Bodies[0].Should().Contain("\"name\":\"credential-sdk\"").And.Contain("\"ecosystem\":\"PyPI\"");
        }

        [Fact]
        public async Task CheckAsync_WhenTheServiceFails_ReturnsErrorInsteadOfThrowing()
        {
            var handler = new StubHandler(HttpStatusCode.InternalServerError, "oops");

            var report = await new OsvMaliciousPackageChecker(new StubFactory(handler)).CheckAsync(_dependencies, TestContext.Current.CancellationToken);

            report.Error.Should().NotBeNullOrEmpty();
            report.Malicious.Should().BeEmpty();
        }

        [Fact]
        public async Task CheckAsync_WithUnexpectedResultsCount_ReturnsError()
        {
            var handler = new StubHandler(HttpStatusCode.OK, """{"results":[{}]}""");

            var report = await new OsvMaliciousPackageChecker(new StubFactory(handler)).CheckAsync(_dependencies, TestContext.Current.CancellationToken);

            report.Error.Should().Contain("Unexpected");
        }

        [Fact]
        public async Task CheckAsync_WithoutDependencies_DoesNotCallTheService()
        {
            var handler = new StubHandler(HttpStatusCode.OK, "{}");

            var report = await new OsvMaliciousPackageChecker(new StubFactory(handler)).CheckAsync([], TestContext.Current.CancellationToken);

            report.Malicious.Should().BeEmpty();
            handler.Requests.Should().BeEmpty();
        }

        private sealed class StubHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
        {
            public List<string> Requests { get; } = [];
            public List<string> Bodies { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request.RequestUri!.ToString());
                Bodies.Add(request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));

                return new HttpResponseMessage(status) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
            }
        }

        private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("https://osv.test/") };
        }
    }

    public class PythonUvExecutorTests : IDisposable
    {
        private readonly DirectoryInfo _workRoot = Directory.CreateTempSubdirectory("lamechain-uv-test");
        private readonly Mock<IMaliciousPackageChecker> _checker = new();

        public void Dispose() => _workRoot.Delete(recursive: true);

        // uv path that does not exist: if the executor gets to launch uv, the result is a LauncherError
        private PythonUvExecutor executor(bool check = true, bool require = false) => new(Options.Create(new PythonExecutionOptions
        {
            UseUv = true,
            UvPath = Path.Combine(_workRoot.FullName, "missing", "uv"),
            WorkRoot = _workRoot.FullName,
            CheckMaliciousPackages = check,
            RequireMaliciousPackagesCheck = require
        }), _checker.Object);

        [Fact]
        public async Task ExecuteAsync_WithMaliciousPackage_BlocksBeforeInstalling()
        {
            _checker.Setup(c => c.CheckAsync(It.IsAny<IReadOnlyList<PythonDependency>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MaliciousPackageReport([new MaliciousPackage("credential-sdk", ["MAL-2024-5000"])], null));

            var result = await executor().ExecuteAsync("result = 1", ["credential-sdk"], TestContext.Current.CancellationToken);

            result.Error!.Type.Should().Be("MaliciousPackage");
            result.Error.Message.Should().Contain("credential-sdk").And.Contain("MAL-2024-5000");
        }

        [Fact]
        public async Task ExecuteAsync_WhenTheCheckFailsAndIsRequired_AbortsTheInstallation()
        {
            _checker.Setup(c => c.CheckAsync(It.IsAny<IReadOnlyList<PythonDependency>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MaliciousPackageReport([], "service unavailable"));

            var result = await executor(require: true).ExecuteAsync("result = 1", ["pandas"], TestContext.Current.CancellationToken);

            result.Error!.Type.Should().Be("DependencyError");
            result.Error.Message.Should().Contain("service unavailable");
        }

        [Fact]
        public async Task ExecuteAsync_WhenTheCheckFailsAndIsNotRequired_GoesOnWithTheInstallation()
        {
            _checker.Setup(c => c.CheckAsync(It.IsAny<IReadOnlyList<PythonDependency>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MaliciousPackageReport([], "service unavailable"));

            var result = await executor(require: false).ExecuteAsync("result = 1", ["pandas"], TestContext.Current.CancellationToken);

            result.Error!.Type.Should().Be("LauncherError");
        }

        [Fact]
        public async Task ExecuteAsync_WithCheckDisabled_DoesNotCallTheChecker()
        {
            var result = await executor(check: false).ExecuteAsync("result = 1", ["pandas"], TestContext.Current.CancellationToken);

            result.Error!.Type.Should().Be("LauncherError");
            _checker.Verify(c => c.CheckAsync(It.IsAny<IReadOnlyList<PythonDependency>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WithStdlibOnlyDependencies_DoesNotCallTheChecker()
        {
            await executor().ExecuteAsync("import json\nresult = 1", ["json", "os"], TestContext.Current.CancellationToken);

            _checker.Verify(c => c.CheckAsync(It.IsAny<IReadOnlyList<PythonDependency>>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    public class WithPythonExecutorTests
    {
        private static ServiceProvider build(Dictionary<string, string?> settings)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.WithPythonExecutor(config);

            return services.BuildServiceProvider();
        }

        [Fact]
        public void UvMode_RegistersTheUvExecutorAndTheMaliciousPackagesChecker()
        {
            using var provider = build(new() { ["PythonExecutionOptions:UseUv"] = "true" });
            using var scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<IPythonExecutor>().Should().BeOfType<PythonUvExecutor>();
            scope.ServiceProvider.GetRequiredService<IMaliciousPackageChecker>().Should().BeOfType<OsvMaliciousPackageChecker>();
        }

        [Fact]
        public void UvMode_WithCheckDisabled_DoesNotRegisterTheChecker()
        {
            using var provider = build(new() { ["PythonExecutionOptions:UseUv"] = "true", ["PythonExecutionOptions:CheckMaliciousPackages"] = "false" });

            provider.GetService<IMaliciousPackageChecker>().Should().BeNull();
        }

        [Fact]
        public void LocalMode_RegistersTheLocalExecutor()
        {
            using var provider = build(new() { ["PythonExecutionOptions:UseUv"] = "false", ["PythonExecutionOptions:PythonExe"] = "python" });
            using var scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<IPythonExecutor>().Should().BeOfType<LocalPythonExecutor>();
        }

        [Fact]
        public void LocalMode_WithoutPythonExe_FailsOnStartup()
        {
            var act = () => build(new() { ["PythonExecutionOptions:UseUv"] = "false" });

            act.Should().Throw<InvalidOperationException>().WithMessage("*PythonExe*");
        }

        [Fact]
        public void MissingSection_FailsOnStartup()
        {
            var act = () => build(new() { ["Other:Key"] = "value" });

            act.Should().Throw<InvalidOperationException>().WithMessage("*PythonExecutionOptions*");
        }
    }
}
