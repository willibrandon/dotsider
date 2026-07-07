using Dotsider.Infrastructure;
using System.Diagnostics;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// CLI integration tests for session commands using real Unix domain sockets.
/// </summary>
public class SessionCliTests : IAsyncLifetime
{
    private static readonly string s_projectPath = Path.Combine(
        TestHelpers.GetRepoRoot(), "src", "Dotsider");

    private static readonly string s_buildConfig = DetectBuildConfig();

    // Use a high PID that won't collide with real processes
    private const int TestPid = 999_777;

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
    public async ValueTask InitializeAsync()
    {
        var dotsiderPath = SessionDiscovery.GetDotsiderSocketPath(TestPid);
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
        var hex1bPath = SessionDiscovery.GetHex1bSocketPath(TestPid);
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
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await _dotsiderSocket.DisposeAsync();
        await _hex1bSocket.DisposeAsync();
    }

    /// <summary>
    /// Verifies sessions info returns assembly and view data.
    /// </summary>
    [Fact]
    public async Task Sessions_Info_ReturnsAssemblyAndViewData()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "info", TestPid.ToString());

        Assert.Equal(0, exitCode);
        Assert.Contains("MyApp.dll", stdout);
        Assert.Contains("MyApp", stdout);
        Assert.Contains("1.0.0.0", stdout);
        Assert.Contains("PE/Metadata", stdout); // current tab name from numeric ID
    }

    /// <summary>
    /// Verifies sessions info json mode returns json.
    /// </summary>
    [Fact]
    public async Task Sessions_Info_JsonMode_ReturnsJson()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "info", TestPid.ToString(), "--json");

        Assert.Equal(0, exitCode);
        var doc = JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.TryGetProperty("assemblyInfo", out var info));
        Assert.Equal("MyApp.dll", info.GetProperty("fileName").GetString());
        Assert.True(doc.RootElement.TryGetProperty("currentView", out var view));
        Assert.Equal(2, view.GetProperty("tab").GetInt32());
    }

    /// <summary>
    /// Verifies sessions view returns current view.
    /// </summary>
    [Fact]
    public async Task Sessions_View_ReturnsCurrentView()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "view", TestPid.ToString());

        Assert.Equal(0, exitCode);
        Assert.Contains("PE/Metadata", stdout);
        Assert.Contains("idle", stdout);
    }

    /// <summary>
    /// Verifies sessions navigate sends tab change.
    /// </summary>
    [Fact]
    public async Task Sessions_Navigate_SendsTabChange()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "navigate", TestPid.ToString(), "3");

        Assert.Equal(0, exitCode);
        Assert.Contains("tab 3", stdout);
    }

    /// <summary>
    /// Verifies sessions capture returns screen content.
    /// </summary>
    [Fact]
    public async Task Sessions_Capture_ReturnsScreenContent()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "capture", TestPid.ToString());

        Assert.Equal(0, exitCode);
        Assert.Contains("[captured-text]", stdout);
    }

    /// <summary>
    /// Verifies sessions capture format option removed.
    /// </summary>
    [Fact]
    public async Task Sessions_Capture_FormatOptionRemoved()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "sessions", "capture", TestPid.ToString(), "--format", "svg");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("--format", stderr);
    }

    /// <summary>
    /// Verifies sessions trace events returns events.
    /// </summary>
    [Fact]
    public async Task Sessions_TraceEvents_ReturnsEvents()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "trace", "events", TestPid.ToString());

        Assert.Equal(0, exitCode);
        Assert.Contains("[jit]", stdout);
        Assert.Contains("MethodLoad", stdout);
        Assert.Contains("[gc]", stdout);
    }

    /// <summary>
    /// Verifies sessions trace counters returns counters.
    /// </summary>
    [Fact]
    public async Task Sessions_TraceCounters_ReturnsCounters()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "trace", "counters", TestPid.ToString());

        Assert.Equal(0, exitCode);
        Assert.Contains("cpuUsage", stdout);
    }

    /// <summary>
    /// Verifies sessions trace output returns output.
    /// </summary>
    [Fact]
    public async Task Sessions_TraceOutput_ReturnsOutput()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "trace", "output", TestPid.ToString());

        Assert.Equal(0, exitCode);
        Assert.Contains("Hello, World!", stdout);
        Assert.Contains("[err]", stdout);
    }

    /// <summary>
    /// Verifies sessions trace start queues trace.
    /// </summary>
    [Fact]
    public async Task Sessions_TraceStart_QueuesTrace()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "trace", "start", TestPid.ToString());

        Assert.Equal(0, exitCode);
        Assert.Contains("Trace start queued", stdout);
    }

    /// <summary>
    /// Verifies sessions trace stop stops trace.
    /// </summary>
    [Fact]
    public async Task Sessions_TraceStop_StopsTrace()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "trace", "stop", TestPid.ToString());

        Assert.Equal(0, exitCode);
        Assert.Contains("Trace stopped", stdout);
    }

    /// <summary>
    /// Verifies sessions navigate out of range tab returns error.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(99)]
    public async Task Sessions_Navigate_OutOfRangeTab_ReturnsError(int tabId)
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "sessions", "navigate", TestPid.ToString(), tabId.ToString());

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Tab must be 1-8", stderr);
    }

    /// <summary>
    /// Verifies that sessions info shows Traceable: yes for NativeAOT assemblies
    /// even when HasEntryPoint is false, since NativeAOT binaries can be traced directly.
    /// </summary>
    [Fact]
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
            "sessions", "info", TestPid.ToString());

        Assert.Equal(0, exitCode);
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
    [Fact]
    public async Task Sessions_Info_InvalidPid_ReturnsError()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "sessions", "info", "999111");

        Assert.NotEqual(0, exitCode);
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
