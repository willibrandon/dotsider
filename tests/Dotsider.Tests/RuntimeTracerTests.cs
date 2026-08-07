using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Runtime Tracer.
/// </summary>
[TestClass]
public sealed class RuntimeTracerTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private RuntimeTracer? _tracer;
    private Hex1bApp? _app;
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;

    private RuntimeTracer CreateTracer(
        string assemblyPath,
        IReadOnlyList<string>? arguments = null)
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();
        _app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = _workload });
        _tracer = new RuntimeTracer(
            assemblyPath,
            arguments ?? [],
            () => _app.Invalidate());
        return _tracer;
    }

    /// <summary>
    /// Verifies trace-host launch information uses discrete arguments.
    /// The target path and every literal application argument remain separate.
    /// No command-line string or shell interpretation is involved.
    /// </summary>
    [TestMethod]
    public void CreateTraceHostStartInfo_ManagedDll_UsesArgumentList()
    {
        var assemblyPath = Samples.HelloWorldDll;
        using var tracer = new RuntimeTracer(
            assemblyPath,
            ["--fx-version", "value with spaces", "", "a&b"],
            static () => { });

        var startInfo = tracer.CreateTraceHostStartInfo();

        Assert.IsTrue(Path.IsPathFullyQualified(startInfo.FileName));
        Assert.IsTrue(File.Exists(startInfo.FileName));
        Assert.AreEqual("", startInfo.Arguments);
        Assert.IsFalse(startInfo.UseShellExecute);
        Assert.IsTrue(startInfo.RedirectStandardInput);
        Assert.IsTrue(startInfo.RedirectStandardOutput);
        Assert.IsTrue(startInfo.RedirectStandardError);
        Assert.AreEqual("exec", startInfo.ArgumentList[0]);
        Assert.EndsWith("dotsider-tracehost.dll", startInfo.ArgumentList[1]);
        AssertArguments([
            assemblyPath,
            "--fx-version",
            "value with spaces",
            "",
            "a&b"
        ], [.. startInfo.ArgumentList.Skip(2)]);
    }

    /// <summary>
    /// Verifies executable target paths remain literal trace-host arguments.
    /// Shell metacharacters and quotes retain their exact token boundaries.
    /// The Native AOT parent launches only the bundled trace host directly.
    /// </summary>
    [TestMethod]
    public void CreateTraceHostStartInfo_ExecutableTarget_PreservesLiteralArguments()
    {
        var appHostPath = CreateExecutableTestFile();
        using var tracer = new RuntimeTracer(
            appHostPath,
            ["$(whoami)", "quote\"slash\\"],
            static () => { });

        var startInfo = tracer.CreateTraceHostStartInfo();

        AssertArguments(
            [Path.GetFullPath(appHostPath), "$(whoami)", "quote\"slash\\"],
            [.. startInfo.ArgumentList.Skip(2)]);

        File.Delete(appHostPath);
    }

    /// <summary>
    /// Verifies constructor input is copied and cannot mutate a later launch.
    /// </summary>
    [TestMethod]
    public void Constructor_MutableArguments_DefensivelyCopies()
    {
        var arguments = new List<string> { "original" };
        using var tracer = new RuntimeTracer(Samples.HelloWorldDll, arguments, static () => { });

        arguments[0] = "changed";
        arguments.Add("extra");

        AssertArguments(
            [Samples.HelloWorldDll, "original"],
            [.. tracer.CreateTraceHostStartInfo().ArgumentList.Skip(2)]);
    }

    /// <summary>
    /// Verifies null argument elements are rejected at the API boundary.
    /// </summary>
    [TestMethod]
    public void Constructor_NullArgumentElement_Throws()
    {
        string[] arguments = [null!];

        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new RuntimeTracer(Samples.HelloWorldDll, arguments, static () => { }));

        Assert.AreEqual("arguments", exception.ParamName);
    }

    /// <summary>
    /// Verifies missing targets fail before the trace host is launched.
    /// This avoids waiting for the diagnostics connection timeout.
    /// The exception retains the fully resolved missing file path.
    /// </summary>
    [TestMethod]
    public void Constructor_MissingTarget_ThrowsFileNotFoundException()
    {
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            $"dotsider-missing-{Guid.NewGuid():N}.dll");

        var exception = Assert.ThrowsExactly<FileNotFoundException>(
            () => new RuntimeTracer(missingPath, [], static () => { }));

        Assert.AreEqual(Path.GetFullPath(missingPath), exception.FileName);
    }

    /// <summary>
    /// Verifies unsupported direct-launch files fail at the API boundary.
    /// Windows rejects command scripts and Unix rejects non-executable files.
    /// This prevents ambiguous or shell-dependent process launches.
    /// </summary>
    [TestMethod]
    public void Constructor_UnsupportedDirectLaunchTarget_ThrowsArgumentException()
    {
        var extension = OperatingSystem.IsWindows() ? ".cmd" : ".txt";
        var path = Path.Combine(
            Path.GetTempPath(),
            $"dotsider-unsupported-{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, "test");

        try
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new RuntimeTracer(path, [], static () => { }));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies an incomplete deployment is rejected before tracing starts.
    /// The message directs users to restore the bundled trace host payload.
    /// </summary>
    [TestMethod]
    public void GetUnavailableReason_MissingTraceHost_ExplainsReinstallation()
    {
        var baseDirectory = Directory.CreateTempSubdirectory("dotsider-tracehost-missing-");
        try
        {
            var reason = RuntimeTracer.GetUnavailableReason(
                baseDirectory.FullName,
                dotNetBasePath: null);

            Assert.IsNotNull(reason);
            Assert.Contains("missing or incomplete", reason);
            Assert.Contains("Reinstall dotsider", reason);
        }
        finally
        {
            baseDirectory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a complete helper without a compatible runtime is rejected.
    /// The message names the minimum runtime needed by Dynamic analysis.
    /// </summary>
    [TestMethod]
    public void GetUnavailableReason_MissingRuntime_ExplainsRequirement()
    {
        var baseDirectory = CreateTraceHostTestLayout();
        try
        {
            var reason = RuntimeTracer.GetUnavailableReason(
                baseDirectory.FullName,
                dotNetBasePath: null);

            Assert.IsNotNull(reason);
            Assert.Contains(".NET 10 runtime or later", reason);
        }
        finally
        {
            baseDirectory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a complete helper and compatible runtime enable tracing.
    /// Later major runtimes satisfy the trace host roll-forward policy.
    /// </summary>
    [TestMethod]
    [DataRow("10.0.0")]
    [DataRow("11.0.1")]
    public void GetUnavailableReason_CompatibleRuntime_ReturnsNull(string runtimeVersion)
    {
        var baseDirectory = CreateTraceHostTestLayout();
        var dotNetDirectory = Directory.CreateTempSubdirectory("dotsider-dotnet-runtime-");
        try
        {
            File.WriteAllText(
                Path.Combine(
                    dotNetDirectory.FullName,
                    OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"),
                "test");
            Directory.CreateDirectory(Path.Combine(
                dotNetDirectory.FullName,
                "shared",
                "Microsoft.NETCore.App",
                runtimeVersion));

            var reason = RuntimeTracer.GetUnavailableReason(
                baseDirectory.FullName,
                dotNetDirectory.FullName);

            Assert.IsNull(reason);
        }
        finally
        {
            baseDirectory.Delete(recursive: true);
            dotNetDirectory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Waits for the tracer to reach Exited or Error. If the process hangs
    /// under EventPipe (known issue on Windows CI), stops it after the timeout.
    /// The caller's timeout plus the 10-second recovery wait must fit inside
    /// the test's <c>[Timeout]</c>, or MSTest aborts the test before this
    /// recovery ever runs.
    /// </summary>
    private static async Task WaitForExitAsync(RuntimeTracer tracer, TimeSpan timeout)
    {
        try
        {
            await TestHelpers.WaitUntilAsync(
                () => tracer.ProcessState is TraceProcessState.Exited or TraceProcessState.Error,
                timeout);
        }
        catch (TimeoutException)
        {
            tracer.Stop();
            await TestHelpers.WaitUntilAsync(
                () => tracer.ProcessState is TraceProcessState.Exited or TraceProcessState.Error,
                TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>
    /// Verifies launch hello world transitions to running.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_TransitionsToRunning()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        Assert.AreEqual(TraceProcessState.Idle, tracer.ProcessState);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState is TraceProcessState.Running or TraceProcessState.Exited
                or TraceProcessState.Error,
            TimeSpan.FromSeconds(30));
        Assert.IsTrue(tracer.ProcessState is TraceProcessState.Running or TraceProcessState.Exited,
            $"Expected Running or Exited but got {tracer.ProcessState}: {tracer.ErrorMessage}");
    }

    /// <summary>
    /// Verifies launch hello world exits successfully.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_ExitsSuccessfully()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        // ExitCode is set by the Process.Exited handler which fires asynchronously —
        // wait for it rather than reading immediately after state transition.
        await TestHelpers.WaitUntilAsync(() => tracer.ExitCode is not null, TimeSpan.FromSeconds(5));
        Assert.AreEqual(0, tracer.ExitCode);
    }

    /// <summary>
    /// Verifies the Exited state never precedes its process exit code.
    /// EventPipe can finish just before macOS reports process termination.
    /// The public lifecycle snapshot must still become observable atomically.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ShortLivedProcess_ExitedState_AlreadyHasExitCode()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);

        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));

        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        Assert.AreEqual(0, tracer.ExitCode);
    }

    /// <summary>
    /// Verifies launch hello world captures events.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_CapturesEvents()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        var events = tracer.GetEvents();
        Assert.IsNotEmpty(events);
    }

    /// <summary>
    /// Verifies launch hello world captures counters.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_CapturesCounters()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        // Counters arrive every ~1s — wait up to 10s
        await TestHelpers.WaitUntilAsync(
            () => tracer.GetLatestCounters() != null
                || tracer.ProcessState == TraceProcessState.Error,
            TimeSpan.FromSeconds(30));
        Assert.AreNotEqual(TraceProcessState.Error, tracer.ProcessState);
        var counters = tracer.GetLatestCounters();
        Assert.IsNotNull(counters);
    }

    /// <summary>
    /// Verifies launch hello world captures output.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_CapturesOutput()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        var output = tracer.GetOutput();
        Assert.IsNotEmpty(output);
    }

    /// <summary>
    /// Verifies real EventPipe launch preserves every literal application argument.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_LiteralArguments_PreserveExactBoundaries()
    {
        string[] arguments =
        [
            "--fx-version",
            "value with spaces",
            "",
            "a&b",
            "$(whoami)",
            "quote\"slash\\"
        ];
        var tracer = CreateTracer(Samples.HelloWorldDll, arguments);

        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));

        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        var output = tracer.GetOutput()
            .Where(static line => line.Text.StartsWith("ARG[", StringComparison.Ordinal))
            .Select(static line => line.Text)
            .ToArray();
        var expected = arguments
            .Select(static (argument, index) => $"ARG[{index}]={argument}")
            .ToArray();
        AssertArguments(expected, output);
    }

    /// <summary>
    /// Verifies launch hello world summary has events.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_SummaryHasEvents()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        var summary = tracer.GetSummary();
        Assert.IsGreaterThan(0, summary.TotalEvents);
    }

    /// <summary>
    /// Verifies launch hello world process id is set.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_ProcessIdIsSet()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessId != null,
            TimeSpan.FromSeconds(10));
        Assert.IsNotNull(tracer.ProcessId);
    }

    /// <summary>
    /// Verifies launch hello world elapsed increases.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_ElapsedIncreases()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState is TraceProcessState.Running or TraceProcessState.Exited
                or TraceProcessState.Error,
            TimeSpan.FromSeconds(30));
        Assert.AreNotEqual(TraceProcessState.Error, tracer.ProcessState);
        var elapsed1 = tracer.Elapsed;
        if (tracer.ProcessState == TraceProcessState.Running)
        {
            await Task.Delay(500, CancellationToken.None);
            Assert.IsGreaterThan(elapsed1, tracer.Elapsed);
        }
    }

    /// <summary>
    /// Verifies error message null on successful run.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ErrorMessage_NullOnSuccessfulRun()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        Assert.IsNull(tracer.ErrorMessage);
    }

    /// <summary>
    /// Verifies summary total exceptions matches event count.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Summary_TotalExceptions_MatchesEventCount()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        var summary = tracer.GetSummary();
        var exceptionEvents = summary.EventsByCategory
            .GetValueOrDefault(TraceEventCategory.Exception);
        Assert.AreEqual(exceptionEvents, summary.TotalExceptions);
    }

    /// <summary>
    /// Verifies complex app short lived still captures events.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ComplexApp_ShortLived_StillCapturesEvents()
    {
        var tracer = CreateTracer(Samples.ComplexAppDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(45));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);

        var events = tracer.GetEvents();

        await TestHelpers.WaitUntilAsync(() => tracer.ExitCode is not null, TimeSpan.FromSeconds(5));
        Assert.AreEqual(0, tracer.ExitCode);
        Assert.IsNotEmpty(events);
    }

    // --- Lifecycle edge cases ---

    /// <summary>
    /// Verifies stop during startup unblocks within five seconds.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Stop_DuringStartup_UnblocksWithinFiveSeconds()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        tracer.Stop();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState is TraceProcessState.Exited or TraceProcessState.Error,
            TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Verifies dispose while running cleans up.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Dispose_WhileRunning_CleansUp()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState is TraceProcessState.Running or TraceProcessState.Exited
                or TraceProcessState.Error,
            TimeSpan.FromSeconds(30));
        tracer.Dispose();
        _tracer = null; // prevent double-dispose in test cleanup
    }

    /// <summary>
    /// Verifies dispose called twice no throw.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Dispose_CalledTwice_NoThrow()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Dispose();
        tracer.Dispose(); // should not throw
        _tracer = null;
    }

    /// <summary>
    /// Verifies event categories contain expected types.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task EventCategories_ContainExpectedTypes()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        var events = tracer.GetEvents();
        var categories = events.Select(e => e.Category).Distinct().ToHashSet();
        // HelloWorld triggers GC and JIT at minimum
        Assert.IsGreaterThan(0, categories.Count);
    }

    /// <summary>
    /// Verifies jit events overloaded methods disambiguated by token.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task JitEvents_OverloadedMethods_DisambiguatedByToken()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        // Wait for the specific Format JIT events — ProcessState can transition
        // to Exited before all events are flushed from the EventPipe buffer.
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        await TestHelpers.WaitUntilAsync(
            () => tracer.GetEvents().Count(e => e.Category == TraceEventCategory.JIT
                && e.Detail.EndsWith(".Format") && e.MetadataToken > 0) >= 2,
            TimeSpan.FromSeconds(10));

        var jitEvents = tracer.GetEvents()
            .Where(e => e.Category == TraceEventCategory.JIT)
            .ToList();
        Assert.IsNotEmpty(jitEvents);

        var formatEvents = jitEvents
            .Where(e => e.Detail.EndsWith(".Format"))
            .ToList();
        Assert.IsGreaterThanOrEqualTo(2, formatEvents.Count, $"Expected >=2 Formatter.Format JIT events, got {formatEvents.Count}");

        // Tokens must be distinct (the whole point of disambiguation)
        var distinctTokens = formatEvents.Select(e => e.MetadataToken).Distinct().ToList();
        Assert.IsGreaterThanOrEqualTo(2, distinctTokens.Count, $"Overloaded JIT events should have distinct tokens, got: " +
            $"{string.Join(", ", formatEvents.Select(e => $"0x{e.MetadataToken:X8}"))}");

        // Verify token-based lookup resolves each to a different MethodDefInfo,
        // while name-based lookup would collapse them to the same method.
        using var analyzer = new AssemblyAnalyzer(Samples.HelloWorldDll);
        var evt1 = formatEvents[0];
        var evt2 = formatEvents.First(e => e.MetadataToken != evt1.MetadataToken);

        var byToken1 = analyzer.MethodDefs.FirstOrDefault(m => m.Token == evt1.MetadataToken);
        var byToken2 = analyzer.MethodDefs.FirstOrDefault(m => m.Token == evt2.MetadataToken);
        Assert.IsNotNull(byToken1);
        Assert.IsNotNull(byToken2);
        Assert.AreNotEqual(byToken1.Token, byToken2.Token);

        // Name-based lookup returns the same method for both (the disambiguation gap)
        Assert.IsTrue(DynamicAnalysisView.TryParseJitDetail(evt1.Detail, out var declType, out var methName));
        var byName = analyzer.MethodDefs
            .Where(m => m.DeclaringType == declType && m.Name == methName)
            .ToList();
        Assert.IsGreaterThanOrEqualTo(2, byName.Count, "Analyzer should have >=2 MethodDefs with the same DeclaringType+Name");
        var firstByName = byName[0];
        // Without token, FirstOrDefault always returns the same method regardless of which event
        Assert.AreEqual(firstByName, analyzer.MethodDefs.FirstOrDefault(
            m => m.DeclaringType == declType && m.Name == methName));
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _tracer?.Dispose();
        _app?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
    }

    private static void AssertArguments(
        string[] expected,
        string[] actual)
    {
        Assert.HasCount(expected.Length, actual);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.AreEqual(
                expected[index],
                actual[index],
                $"Argument {index} differs.");
        }
    }

    private static DirectoryInfo CreateTraceHostTestLayout()
    {
        var baseDirectory = Directory.CreateTempSubdirectory("dotsider-tracehost-layout-");
        var traceHostDirectory = Directory.CreateDirectory(
            Path.Combine(baseDirectory.FullName, "tracehost"));
        File.WriteAllText(
            Path.Combine(traceHostDirectory.FullName, "dotsider-tracehost.deps.json"),
            "{}");
        File.WriteAllText(
            Path.Combine(traceHostDirectory.FullName, "dotsider-tracehost.dll"),
            "test");
        File.WriteAllText(
            Path.Combine(traceHostDirectory.FullName, "dotsider-tracehost.runtimeconfig.json"),
            "{}");
        return baseDirectory;
    }

    private static string CreateExecutableTestFile()
    {
        var extension = OperatingSystem.IsWindows() ? ".exe" : "";
        var path = Path.Combine(
            Path.GetTempPath(),
            $"dotsider-executable-{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, "test");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return path;
    }
}
