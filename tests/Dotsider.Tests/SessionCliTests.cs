using Dotsider.Infrastructure;
using System.Diagnostics;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// CLI integration tests for session commands using real Unix domain sockets.
/// </summary>
[TestClass]
public class SessionCliTests
{
    private static readonly string s_projectPath = Path.Combine(
        TestHelpers.GetRepoRoot(), "src", "Dotsider");

    private static readonly string s_buildConfig = DetectBuildConfig();

    private int _testPid;
    private TestDotsiderSocket _dotsiderSocket = null!;
    private TestRawJsonSocket _hex1bSocket = null!;

    private static string DetectBuildConfig()
    {
        var parts = AppContext.BaseDirectory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("bin", StringComparison.OrdinalIgnoreCase))
                return parts[i + 1];
        }

        return "Debug";
    }

    /// <summary>
    /// Prepares the fixture state before tests execute.
    /// </summary>
    [TestInitialize]
    public async ValueTask InitializeAsync()
    {
        _testPid = TestSocketIds.NextPid();

        var dotsiderPath = SessionDiscovery.GetDotsiderSocketPath(_testPid);
        _dotsiderSocket = new TestDotsiderSocket(dotsiderPath);

        // Register handlers for all protocol methods
        _dotsiderSocket.On("assembly-info", _ => new
        {
            FilePath = "/test/MyApp.dll",
            FileName = "MyApp.dll",
            FileSize = 12345L,
            AssemblyName = "MyApp",
            AssemblyVersion = "1.0.0.0",
            TargetFramework = ".NETCoreApp,Version=v10.0",
            Architecture = "AnyCPU",
            HasMetadata = true,
            TypeCount = 5,
            MethodCount = 15,
            AssemblyRefCount = 3
        });

        _dotsiderSocket.On("get-current-view", _ => new
        {
            Tab = 2,           // PE/Metadata (1-based, matching real protocol)
            PeSubTab = 0,      // Sections
            DynamicSubTab = 0, // Events
            AssemblyPath = "/test/MyApp.dll",
            NavigationDepth = 0,
            TracerState = "idle"
        });

        _dotsiderSocket.On("navigate", req => new
        {
            Message = $"Navigation to tab {req.TabId} queued"
        });

        _dotsiderSocket.On("get-trace-events", _ => new[]
        {
            new { Category = "jit", Name = "MethodLoad", Detail = "MyApp.Program.Main" },
            new { Category = "gc", Name = "GCStart", Detail = "Gen0" }
        });

        _dotsiderSocket.On("get-trace-counters", _ => new
        {
            CpuUsage = 5.2,
            WorkingSet = 42_000_000L,
            GcHeapSize = 8_000_000L
        });

        _dotsiderSocket.On("get-process-output", _ => new[]
        {
            new { Stream = "stdout", Text = "Hello, World!" },
            new { Stream = "stderr", Text = "Warning: test" }
        });

        _dotsiderSocket.On("start-trace", _ => new { Message = "Trace start queued" });
        _dotsiderSocket.On("stop-trace", _ => new { Message = "Trace stopped" });

        _dotsiderSocket.Start();

        // Set up hex1b socket for capture tests
        var hex1bPath = SessionDiscovery.GetHex1bSocketPath(_testPid);
        _hex1bSocket = new TestRawJsonSocket(hex1bPath);
        _hex1bSocket.OnRequest(request =>
        {
            var method = request.GetProperty("method").GetString();
            if (method == "capture")
            {
                var format = request.TryGetProperty("format", out var fmt)
                    ? fmt.GetString() : "text";
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    data = $"[captured-{format}]"
                });
            }

            return JsonSerializer.Serialize(new { success = false, error = "Unknown method" });
        });
        _hex1bSocket.Start();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Releases fixture state after tests complete.
    /// </summary>
    [TestCleanup]
    public async ValueTask DisposeAsync()
    {
        if (_dotsiderSocket is not null)
            await _dotsiderSocket.DisposeAsync();
        if (_hex1bSocket is not null)
            await _hex1bSocket.DisposeAsync();
    }

    /// <summary>
    /// Verifies sessions info returns assembly and view data.
    /// </summary>
    [TestMethod]
    public async Task Sessions_Info_ReturnsAssemblyAndViewData()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "info", _testPid.ToString());

        Assert.AreEqual(0, exitCode);
        Assert.Contains("MyApp.dll", stdout);
        Assert.Contains("MyApp", stdout);
        Assert.Contains("1.0.0.0", stdout);
        Assert.Contains("PE/Metadata", stdout); // current tab name from numeric ID
    }

    /// <summary>
    /// Verifies sessions info json mode returns json.
    /// </summary>
    [TestMethod]
    public async Task Sessions_Info_JsonMode_ReturnsJson()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "info", _testPid.ToString(), "--json");

        Assert.AreEqual(0, exitCode);
        var doc = JsonDocument.Parse(stdout);
        Assert.IsTrue(doc.RootElement.TryGetProperty("assemblyInfo", out var info));
        Assert.AreEqual("MyApp.dll", info.GetProperty("fileName").GetString());
        Assert.IsTrue(doc.RootElement.TryGetProperty("currentView", out var view));
        Assert.AreEqual(2, view.GetProperty("tab").GetInt32());
    }

    /// <summary>
    /// Verifies sessions view returns current view.
    /// </summary>
    [TestMethod]
    public async Task Sessions_View_ReturnsCurrentView()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "view", _testPid.ToString());

        Assert.AreEqual(0, exitCode);
        Assert.Contains("PE/Metadata", stdout);
        Assert.Contains("idle", stdout);
    }

    /// <summary>
    /// Verifies sessions navigate sends tab change.
    /// </summary>
    [TestMethod]
    public async Task Sessions_Navigate_SendsTabChange()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "navigate", _testPid.ToString(), "3");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("tab 3", stdout);
    }

    /// <summary>
    /// Verifies sessions capture returns screen content.
    /// </summary>
    [TestMethod]
    public async Task Sessions_Capture_ReturnsScreenContent()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "capture", _testPid.ToString());

        Assert.AreEqual(0, exitCode);
        Assert.Contains("[captured-text]", stdout);
    }

    /// <summary>
    /// Verifies sessions capture format option removed.
    /// </summary>
    [TestMethod]
    public async Task Sessions_Capture_FormatOptionRemoved()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "sessions", "capture", _testPid.ToString(), "--format", "svg");

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("--format", stderr);
    }

    /// <summary>
    /// Verifies sessions trace events returns events.
    /// </summary>
    [TestMethod]
    public async Task Sessions_TraceEvents_ReturnsEvents()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "trace", "events", _testPid.ToString());

        Assert.AreEqual(0, exitCode);
        Assert.Contains("[jit]", stdout);
        Assert.Contains("MethodLoad", stdout);
        Assert.Contains("[gc]", stdout);
    }

    /// <summary>
    /// Verifies sessions trace counters returns counters.
    /// </summary>
    [TestMethod]
    public async Task Sessions_TraceCounters_ReturnsCounters()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "trace", "counters", _testPid.ToString());

        Assert.AreEqual(0, exitCode);
        Assert.Contains("cpuUsage", stdout);
    }

    /// <summary>
    /// Verifies sessions trace output returns output.
    /// </summary>
    [TestMethod]
    public async Task Sessions_TraceOutput_ReturnsOutput()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "trace", "output", _testPid.ToString());

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Hello, World!", stdout);
        Assert.Contains("[err]", stdout);
    }

    /// <summary>
    /// Verifies sessions trace start queues trace.
    /// </summary>
    [TestMethod]
    public async Task Sessions_TraceStart_QueuesTrace()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "trace", "start", _testPid.ToString());

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Trace start queued", stdout);
    }

    /// <summary>
    /// Verifies sessions trace stop stops trace.
    /// </summary>
    [TestMethod]
    public async Task Sessions_TraceStop_StopsTrace()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "trace", "stop", _testPid.ToString());

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Trace stopped", stdout);
    }

    /// <summary>
    /// Verifies sessions navigate out of range tab returns error.
    /// </summary>
    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(9)]
    [DataRow(99)]
    public async Task Sessions_Navigate_OutOfRangeTab_ReturnsError(int tabId)
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "sessions", "navigate", _testPid.ToString(), tabId.ToString());

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("Tab must be 1-8", stderr);
    }

    /// <summary>
    /// Verifies that sessions info shows Traceable: yes for NativeAOT assemblies
    /// even when HasEntryPoint is false, since NativeAOT binaries can be traced directly.
    /// </summary>
    [TestMethod]
    public async Task Sessions_Info_NativeAot_ShowsTraceableYes()
    {
        // Override get-current-view to report NativeAOT without entry point
        _dotsiderSocket.On("get-current-view", _ => new
        {
            Tab = 1,
            PeSubTab = 0,
            DynamicSubTab = 0,
            AssemblyPath = "/test/NativeAot",
            NavigationDepth = 0,
            TracerState = "idle",
            HexIsDirty = false,
            HasEntryPoint = false,
            IsNativeAot = true,
            IsNetFramework = false
        });

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "info", _testPid.ToString());

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Traceable:  yes", stdout);

        // Restore original handler for other tests
        _dotsiderSocket.On("get-current-view", _ => new
        {
            Tab = 2,
            PeSubTab = 0,
            DynamicSubTab = 0,
            AssemblyPath = "/test/MyApp.dll",
            NavigationDepth = 0,
            TracerState = "idle"
        });
    }

    /// <summary>
    /// Verifies sessions info invalid pid returns error.
    /// </summary>
    [TestMethod]
    public async Task Sessions_Info_InvalidPid_ReturnsError()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "sessions", "info", "999111");

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("Error:", stderr);
    }

    // --- Helpers ---

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunDotsiderAsync(
        params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --no-build -c {s_buildConfig} --project \"{s_projectPath}\" -- {string.Join(' ', arguments.Select(QuoteArg))}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout, stderr);
    }

    private static string QuoteArg(string arg)
        => arg.Contains(' ') ? $"\"{arg}\"" : arg;
}
