using Dotsider.Core.Analysis.Models;
using Dotsider.TraceHost;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;

namespace Dotsider.Tests;

/// <summary>
/// Verifies the framework-dependent runtime trace host boundary.
/// Covers launch hardening, private diagnostics IPC, and bounded output.
/// Exercises the control channel independently of a traced workload.
/// </summary>
[TestClass]
public sealed class EventPipeRuntimeTracerTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies managed DLLs use the exact host running TraceHost.
    /// The resolved host is absolute and bypasses process search paths.
    /// Arguments remain discrete tokens without shell interpretation.
    /// </summary>
    [TestMethod]
    public void CreateStartInfo_ManagedDll_UsesCurrentProcessHost()
    {
        using var tracer = CreateTracer(Samples.HelloWorldDll);

        var startInfo = tracer.CreateStartInfo("diagnostic-port");

        Assert.AreEqual(Environment.ProcessPath, startInfo.FileName);
        Assert.IsTrue(Path.IsPathFullyQualified(startInfo.FileName));
        AssertArguments(
            ["exec", Samples.HelloWorldDll, "literal argument"],
            startInfo.ArgumentList);
    }

    /// <summary>
    /// Verifies inherited switches cannot disable the diagnostics handshake.
    /// Existing diagnostic-port suspension overrides are also removed.
    /// The requested reverse diagnostic port remains the sole endpoint.
    /// </summary>
    [TestMethod]
    public void ConfigureDiagnosticsEnvironment_DisablingSwitches_RemovesOverrides()
    {
        var environment = new Dictionary<string, string?>
        {
            ["DOTNET_DefaultDiagnosticPortSuspend"] = "0",
            ["DOTNET_EnableDiagnostics"] = "0",
            ["DOTNET_EnableDiagnostics_IPC"] = "0",
            ["COMPlus_EnableDiagnostics"] = "0",
            ["COMPlus_EnableDiagnostics_IPC"] = "0"
        };

        EventPipeRuntimeTracer.ConfigureDiagnosticsEnvironment(environment, "test-port");

        Assert.AreEqual("test-port", environment["DOTNET_DiagnosticPorts"]);
        Assert.IsFalse(environment.ContainsKey("DOTNET_DefaultDiagnosticPortSuspend"));
        Assert.IsFalse(environment.ContainsKey("DOTNET_EnableDiagnostics"));
        Assert.IsFalse(environment.ContainsKey("DOTNET_EnableDiagnostics_IPC"));
        Assert.IsFalse(environment.ContainsKey("COMPlus_EnableDiagnostics"));
        Assert.IsFalse(environment.ContainsKey("COMPlus_EnableDiagnostics_IPC"));
    }

    /// <summary>
    /// Verifies Unix diagnostic sockets reside in a private temp directory.
    /// The directory grants access only to the current user.
    /// Its compact socket name stays within Unix-domain path limits.
    /// </summary>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public void CreateDiagnosticPort_Unix_UsesPrivateTempDirectory()
    {
        var port = EventPipeRuntimeTracer.CreateDiagnosticPort(out var directoryPath);
        Assert.IsNotNull(directoryPath);
        try
        {
            Assert.AreEqual(Path.Combine(directoryPath, "p"), port);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), directoryPath);

            const UnixFileMode expected =
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            const UnixFileMode allPermissions =
                expected
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupWrite
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherWrite
                | UnixFileMode.OtherExecute;
            Assert.AreEqual(expected, File.GetUnixFileMode(directoryPath) & allPermissions);
        }
        finally
        {
            Directory.Delete(directoryPath);
        }
    }

    /// <summary>
    /// Verifies a target cannot exhaust host memory with one endless line.
    /// Oversized output is bounded and marked while later lines remain intact.
    /// CRLF input produces exactly one logical line terminator.
    /// </summary>
    [TestMethod]
    public void ReadOutput_OversizedLine_TruncatesAndContinues()
    {
        var captured = new List<OutputLine>();
        using var tracer = CreateTracer(Samples.HelloWorldDll, captured.Add);
        var text = $"{new string('x', 70_000)}\r\nnext\nlast";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        using var reader = new StreamReader(stream, Encoding.UTF8);

        tracer.ReadOutput(reader, isStdErr: false, Stopwatch.StartNew());

        Assert.HasCount(3, captured);
        Assert.HasCount((64 * 1024) + " … [truncated]".Length, captured[0].Text);
        Assert.EndsWith(" … [truncated]", captured[0].Text);
        Assert.AreEqual("next", captured[1].Text);
        Assert.AreEqual("last", captured[2].Text);
    }

    /// <summary>
    /// Verifies unknown control messages do not disable graceful shutdown.
    /// Monitoring continues until the recognized stop command arrives.
    /// Commands after stop are not consumed or acted upon.
    /// </summary>
    [TestMethod]
    public async Task MonitorAsync_UnknownCommandThenStop_StopsOnce()
    {
        using var reader = new StringReader("unknown\nstop\nstop\n");
        var stopCount = 0;

        await TraceHostControlChannel.MonitorAsync(reader, () => stopCount++);

        Assert.AreEqual(1, stopCount);
    }

    /// <summary>
    /// Verifies parent disconnection stops the traced process.
    /// End-of-stream remains a graceful shutdown signal.
    /// Unknown commands before EOF do not change that behavior.
    /// </summary>
    [TestMethod]
    public async Task MonitorAsync_EndOfInput_StopsOnce()
    {
        using var reader = new StringReader("unknown\n");
        var stopCount = 0;

        await TraceHostControlChannel.MonitorAsync(reader, () => stopCount++);

        Assert.AreEqual(1, stopCount);
    }

    private static EventPipeRuntimeTracer CreateTracer(
        string assemblyPath,
        Action<OutputLine>? outputCaptured = null) =>
        new(
            assemblyPath,
            ["literal argument"],
            static () => { },
            static _ => { },
            static _ => { },
            outputCaptured ?? (static _ => { }));

    private static void AssertArguments(
        string[] expected,
        Collection<string> actual)
    {
        Assert.HasCount(expected.Length, actual);
        for (var index = 0; index < expected.Length; index++)
            Assert.AreEqual(expected[index], actual[index]);
    }
}
