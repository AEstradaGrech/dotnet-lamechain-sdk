using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Scripting;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Infrastructure
{
    public class ScriptFormatTests : IDisposable
    {
        private const string _psWithParams = """
            # Shows the memory of a process
            [CmdletBinding()]
            param(
                [Parameter(Mandatory = $true, HelpMessage = 'Name (without .exe)')]
                [string]$ProcessName,
                [int]$Top = 5
            )

            Write-Output "Received: $ProcessName / $Top"
            """;

        private readonly DirectoryInfo _workRoot = Directory.CreateTempSubdirectory("lamechain-format-test");

        public void Dispose() => _workRoot.Delete(recursive: true);

        private IOptions<ScriptExecutionOptions> options() => Options.Create(new ScriptExecutionOptions { WorkRoot = _workRoot.FullName });

        [Fact]
        public void PreparePowerShell_WithoutParamBlock_PutsTheHeaderFirst()
        {
            ScriptFormat.PreparePowerShell("Write-Output 'hi'").Should().StartWith(ScriptFormat.PowerShellHeader);
        }

        [Fact]
        public void PreparePowerShell_WithParamBlock_PutsTheHeaderRightAfterIt()
        {
            var prepared = ScriptFormat.PreparePowerShell(_psWithParams);

            prepared.Should().StartWith("# Shows the memory of a process");
            prepared.IndexOf("param(").Should().BeLessThan(prepared.IndexOf(ScriptFormat.PowerShellHeader));
            prepared.IndexOf(ScriptFormat.PowerShellHeader).Should().BeLessThan(prepared.IndexOf("Write-Output"));
        }

        [Theory]
        [InlineData("#!/usr/bin/env bash\nhost=\"$1\"\nport=\"${2}\"", 2, 2)]
        [InlineData("#!/usr/bin/env bash\ndf -h | awk '{print $1, $5}'", 0, 0)]
        [InlineData("#!/usr/bin/env bash\nps aux | awk \"{print \\$1}\"", 0, 0)]
        [InlineData("#!/usr/bin/env bash\n# usage: $0 <host> $1\nfor h in \"$@\"; do echo $h; done", 0, null)]
        public void AnalyzeParameters_Bash(string script, int required, int? max)
        {
            ScriptFormat.AnalyzeParameters(EScriptShell.Bash, script).Should().Be((required, max));
        }

        [Theory]
        [InlineData(_psWithParams, 1, 2)]
        [InlineData("param([Parameter(Mandatory = $false)][string]$Name)\nWrite-Output $Name", 0, 1)]
        [InlineData("Write-Output 'no parameters'", 0, 0)]
        [InlineData("Write-Output $args[0]", 0, null)]
        public void AnalyzeParameters_PowerShell(string script, int required, int? max)
        {
            ScriptFormat.AnalyzeParameters(EScriptShell.PowerShell, script).Should().Be((required, max));
        }

        [Fact]
        public async Task SaveScriptAsync_ForAnotherShell_UsesTheFormatOfThatShell()
        {
            // PowerShell executor (API on Windows) saving a bash script the user asked for
            var bashPath = await new PowerShellScriptExecutor(options()).SaveScriptAsync("echo one\r\necho two", EScriptShell.Bash, ct: TestContext.Current.CancellationToken);
            // Bash executor (API in Docker) saving a PowerShell script
            var psPath = await new BashScriptExecutor(options()).SaveScriptAsync("Write-Output 'ñandú'", EScriptShell.PowerShell, ct: TestContext.Current.CancellationToken);

            bashPath.Should().EndWith("script.sh");
            var bashBytes = await File.ReadAllBytesAsync(bashPath, TestContext.Current.CancellationToken);
            bashBytes.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF });
            Encoding.UTF8.GetString(bashBytes).Should().Be("echo one\necho two");

            psPath.Should().EndWith("script.ps1");
            (await File.ReadAllBytesAsync(psPath, TestContext.Current.CancellationToken)).Take(3).Should().Equal(new byte[] { 0xEF, 0xBB, 0xBF });
            (await File.ReadAllTextAsync(psPath, TestContext.Current.CancellationToken)).Should().StartWith(ScriptFormat.PowerShellHeader);
        }

        [Fact]
        public async Task SaveScriptAsync_WithAFolderOutsideWorkRoot_ThrowsWithoutCreatingIt()
        {
            var outside = Path.Combine(Path.GetTempPath(), $"lamechain-outside-{Guid.NewGuid():N}");

            var act = () => new BashScriptExecutor(options()).SaveScriptAsync("echo hi", folder: outside, ct: TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*outside*");
            Directory.Exists(outside).Should().BeFalse();
        }

        [Fact]
        public void PowerShellParameters_ReadsNamesMandatoryAndSwitches()
        {
            var parameters = ScriptFormat.PowerShellParameters("""
                param(
                    [Parameter(Mandatory = $true, HelpMessage = 'Name [without .exe], e.g. dotnet')]
                    [string]$ProcessName,
                    [int]$Top = 5,
                    [switch]$Detailed
                )
                Write-Output $ProcessName
                """)!;

            parameters.Select(p => (p.Name, p.Mandatory, p.IsSwitch)).Should().Equal(
                ("ProcessName", true, false), ("Top", false, false), ("Detailed", false, true));
        }

        [Fact]
        public void BashParameterNames_ReadsTheVariableOfEachPosition()
        {
            var names = ScriptFormat.BashParameterNames("""
                #!/usr/bin/env bash
                set -euo pipefail
                folder="$1"
                days="${2:-7}"
                local pattern=$3   # optional
                output="$4/report.txt"
                folder_again="$1"
                """);

            names.Should().BeEquivalentTo(new Dictionary<int, string> { [1] = "folder", [2] = "days", [3] = "pattern" },
                "only plain assignments count ('$4/report.txt' is not the parameter itself) and the first one of each position wins");
        }

        [Fact]
        public void PowerShellParameters_WithoutParamBlock_IsNull()
        {
            ScriptFormat.PowerShellParameters("Write-Output $args[0]").Should().BeNull();
        }

        [Fact]
        public async Task PowerShell_ExecuteFileAsync_PassesTheArgumentsByName()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "PowerShell execution tests run on Windows only");

            var executor = new PowerShellScriptExecutor(options());
            var path = await executor.SaveScriptAsync("""
                param(
                    [Parameter(Mandatory = $true)][string]$Name,
                    [int]$Count = 1,
                    [switch]$Loud
                )
                Write-Output "name=[$Name] count=[$Count] loud=[$Loud]"
                """, ct: TestContext.Current.CancellationToken);

            // Out of order on purpose (by name the order does not matter). The value with spaces and '$' must arrive as it is
            var on = await executor.ExecuteFileAsync(path, [new("Count", "3"), new("Loud", "true"), new("$Name", "C:\\Program Files\\$weird value")], TestContext.Current.CancellationToken);
            var off = await executor.ExecuteFileAsync(path, [new("Name", "x"), new("Loud", "false")], TestContext.Current.CancellationToken);

            on.Stderr.Should().BeEmpty();
            on.Success.Should().BeTrue();
            on.Stdout.Should().Contain("name=[C:\\Program Files\\$weird value] count=[3] loud=[True]");
            off.Stdout.Should().Contain("name=[x] count=[1] loud=[False]");
        }

        [Fact]
        public async Task Bash_ExecuteFileAsync_PassesTheValuesInOrderWithoutInterpretingThem()
        {
            Assert.SkipWhen(OperatingSystem.IsWindows(), "Bash execution tests run outside Windows only");

            var executor = new BashScriptExecutor(options());
            var path = await executor.SaveScriptAsync("echo \"1=[$1] 2=[$2]\"", ct: TestContext.Current.CancellationToken);

            var result = await executor.ExecuteFileAsync(path, [new("folder", "/var/log with spaces"), new("pattern", "$HOME; echo injected")], TestContext.Current.CancellationToken);

            result.Success.Should().BeTrue();
            result.Stdout.Trim().Should().Be("1=[/var/log with spaces] 2=[$HOME; echo injected]");
        }

        [Fact]
        public async Task PowerShell_ScriptWithParamBlock_BindsTheArgumentsWhenItRuns()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "PowerShell execution tests run on Windows only");

            var path = await new PowerShellScriptExecutor(options()).SaveScriptAsync(_psWithParams, ct: TestContext.Current.CancellationToken);

            var psi = new ProcessStartInfo("powershell.exe") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", path, "dotnet" })
                psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi)!;
            var output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var errors = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);

            errors.Should().BeEmpty();
            output.Should().Contain("Received: dotnet / 5");
        }
    }
}
