using System.Diagnostics;

namespace Dotsider.Tests;

/// <summary>
/// CLI routing parity for ReadyToRun images: the shared correlation query, the R2R stats, the symbol
/// table, and native disassembly all light up through the process entry point over the real fixture.
/// An overloaded name lists candidates and exits non-zero rather than guessing; disassembling a
/// multi-range method renders every block (its import-named call targets appear), not just the hot one.
/// </summary>
[TestClass]
public class ReadyToRunRoutingTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private const string SkipReason = "ReadyToRun crossgen2 publish did not run on this leg.";

    private static readonly string s_projectPath = Path.Combine(
        TestHelpers.GetRepoRoot(), "src", "Dotsider");

    private static readonly string s_buildConfig = TestProcessEnvironment.CurrentBuildConfiguration;

    /// <summary>Bare <c>--r2r-correlate</c> prints the ReadyToRun stats over the image.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task R2rCorrelate_Bare_PrintsStats()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var (exit, stdout, _) = await RunDotsiderAsync("analyze", Samples.ReadyToRunConsoleDll!, "--r2r-correlate");

        Assert.AreEqual(0, exit);
        Assert.Contains("ReadyToRun", stdout);
        Assert.Contains("Precompiled", stdout);
    }

    /// <summary>A unique method resolves through the CLI to its native and IL.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task R2rCorrelate_UniqueName_ResolvesMethod()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var (exit, stdout, _) = await RunDotsiderAsync(
            "analyze", Samples.ReadyToRunConsoleDll!, "--r2r-correlate", "Greeter.get_Name");

        Assert.AreEqual(0, exit);
        Assert.Contains("get_Name", stdout);
    }

    /// <summary>An overloaded name lists candidates and exits non-zero rather than picking first.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task R2rCorrelate_Overloaded_ListsCandidatesAndFails()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var (exit, stdout, stderr) = await RunDotsiderAsync(
            "analyze", Samples.ReadyToRunConsoleDll!, "--r2r-correlate", "Greet");

        Assert.AreNotEqual(0, exit);
        Assert.Contains("ambiguous", (stdout + stderr).ToLowerInvariant());
    }

    /// <summary>The symbol table lists the ReadyToRun-derived symbols.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Symbols_ListsReadyToRunSymbols()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var (exit, stdout, _) = await RunDotsiderAsync("analyze", Samples.ReadyToRunConsoleDll!, "--symbols");

        Assert.AreEqual(0, exit);
        Assert.Contains("Greeter", stdout);
    }

    /// <summary>Disassembling a multi-range method renders all blocks with import-named call targets.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Disasm_MultiRangeMethod_RendersAllBlocks()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var (exit, stdout, _) = await RunDotsiderAsync(
            "analyze", Samples.ReadyToRunConsoleDll!, "--disasm", "MoveNext");

        Assert.AreEqual(0, exit);
        // The import resolver names the indirect call; the multi-range body shows a funclet/cold block.
        Assert.Contains("Console.WriteLine", stdout);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunDotsiderAsync(
        params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --no-build -c {s_buildConfig} --project \"{s_projectPath}\" -- "
                + string.Join(' ', arguments.Select(QuoteArg)),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        TestProcessEnvironment.RemoveCodeCoverageVariables(psi);

        var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = await process.StandardError.ReadToEndAsync(CancellationToken.None);
        await process.WaitForExitAsync(CancellationToken.None);
        return (process.ExitCode, stdout, stderr);
    }

    private static string QuoteArg(string arg) => arg.Contains(' ') ? $"\"{arg}\"" : arg;
}
