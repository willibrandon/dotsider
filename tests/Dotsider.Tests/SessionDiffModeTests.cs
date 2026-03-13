using System.Diagnostics;
using System.Text.Json;
using Dotsider.Core.Analysis;
using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end tests verifying that dotsider sessions list discovers
/// instances running in diff mode, and that the diagnostics socket
/// responds correctly to assembly-info, get-current-view, and CLI commands.
/// </summary>
[Collection("SampleAssemblies")]
public class SessionDiffModeTests(SampleAssemblyFixture samples) : IAsyncLifetime
{
    private static readonly string s_projectPath = Path.Combine(
        TestHelpers.GetRepoRoot(), "src", "Dotsider");

    private static readonly string s_buildConfig = DetectBuildConfig();

    // Use a high PID that won't collide with real processes
    private const int DiffPid = 888_001;

    private readonly SampleAssemblyFixture _samples = samples;

    // Mutable view state exposed to currentViewProvider
    private int _currentTab;
    private DiffFilterMode _filterMode = DiffFilterMode.All;

    private AssemblyAnalyzer _leftAnalyzer = null!;
    private AssemblyAnalyzer _rightAnalyzer = null!;
    private DotsiderDiagnosticsListener _listener = null!;

    private static string DetectBuildConfig()
    {
        var parts = AppContext.BaseDirectory.Split(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("bin", StringComparison.OrdinalIgnoreCase))
                return parts[i + 1];
        }

        return "Debug";
    }

    public async ValueTask InitializeAsync()
    {
        _leftAnalyzer = new AssemblyAnalyzer(_samples.RichLibraryDll);
        _rightAnalyzer = new AssemblyAnalyzer(_samples.RichLibraryV2Dll);

        _listener = new DotsiderDiagnosticsListener(
            () => null,
            assemblyInfoProvider: () => new
            {
                Mode = "diff",
                FileName = $"{_leftAnalyzer.FileName} \u2194 {_rightAnalyzer.FileName}",
                Left = new
                {
                    _leftAnalyzer.FilePath,
                    _leftAnalyzer.FileName,
                    _leftAnalyzer.FileSize,
                    _leftAnalyzer.AssemblyName,
                    _leftAnalyzer.AssemblyVersion,
                    _leftAnalyzer.TargetFramework,
                },
                Right = new
                {
                    _rightAnalyzer.FilePath,
                    _rightAnalyzer.FileName,
                    _rightAnalyzer.FileSize,
                    _rightAnalyzer.AssemblyName,
                    _rightAnalyzer.AssemblyVersion,
                    _rightAnalyzer.TargetFramework,
                },
            },
            currentViewProvider: () => new
            {
                Mode = "diff",
                Tab = _currentTab,
                FilterMode = _filterMode,
            });
        _listener.StartListening(overridePid: DiffPid);

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await _listener.DisposeAsync();
        _leftAnalyzer.Dispose();
        _rightAnalyzer.Dispose();
    }

    // --- Session discovery tests ---

    [Fact]
    public async Task SessionsList_FindsDiffModeInstance()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync("sessions", "list", "--json");

        Assert.Equal(0, exitCode);
        var sessions = JsonSerializer.Deserialize<JsonElement>(stdout);
        Assert.Equal(JsonValueKind.Array, sessions.ValueKind);

        var diffSession = FindSessionByPid(sessions, DiffPid);
        Assert.NotNull(diffSession);
        Assert.Equal("diff", diffSession.Value.GetProperty("mode").GetString());
        Assert.Contains("\u2194", diffSession.Value.GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task SessionsList_TableOutput_ShowsDiffMode()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync("sessions", "list");

        Assert.Equal(0, exitCode);
        Assert.Contains("Mode", stdout);
        Assert.Contains("diff", stdout);
    }

    // --- Direct socket tests ---

    [Fact]
    public async Task DiffListener_RespondsWithRealAnalyzerData()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await DotsiderClient.TryProbeAsync(
            SessionDiscovery.GetDotsiderSocketPath(DiffPid), ct);

        Assert.NotNull(response);
        Assert.True(response.Success);

        var data = response.Data as JsonElement?;
        Assert.NotNull(data);
        Assert.Equal("diff", data.Value.GetProperty("mode").GetString());

        // Verify real analyzer data is present
        var left = data.Value.GetProperty("left");
        var right = data.Value.GetProperty("right");
        Assert.Equal("RichLibrary", left.GetProperty("assemblyName").GetString());
        Assert.Equal("RichLibrary", right.GetProperty("assemblyName").GetString());
        Assert.NotEqual(
            left.GetProperty("assemblyVersion").GetString(),
            right.GetProperty("assemblyVersion").GetString());
    }

    [Fact]
    public async Task DiffListener_ReturnsCurrentView()
    {
        var ct = TestContext.Current.CancellationToken;
        var socketPath = SessionDiscovery.GetDotsiderSocketPath(DiffPid);

        // Set a non-default tab before querying
        _currentTab = 2;
        _filterMode = DiffFilterMode.AddedOnly;

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);

        Assert.True(response.Success);
        var data = response.Data as JsonElement?;
        Assert.NotNull(data);
        Assert.Equal("diff", data.Value.GetProperty("mode").GetString());
        Assert.Equal(2, data.Value.GetProperty("tab").GetInt32());
        Assert.Equal("addedOnly", data.Value.GetProperty("filterMode").GetString());
    }

    [Fact]
    public async Task DiffListener_RejectsUnsupportedMethods()
    {
        var ct = TestContext.Current.CancellationToken;
        var socketPath = SessionDiscovery.GetDotsiderSocketPath(DiffPid);
        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "list-types" }, ct);

        // Should fail because diff mode has no DotsiderState
        Assert.False(response.Success);
    }

    // --- CLI sessions info/view tests ---

    [Fact]
    public async Task SessionsInfo_ReturnsDiffModeData()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "info", DiffPid.ToString(), "--json");

        Assert.Equal(0, exitCode);
        var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;

        // assembly-info portion
        var info = root.GetProperty("assemblyInfo");
        Assert.Equal("diff", info.GetProperty("mode").GetString());
        Assert.True(info.TryGetProperty("left", out _));
        Assert.True(info.TryGetProperty("right", out _));

        // get-current-view portion
        var view = root.GetProperty("currentView");
        Assert.Equal("diff", view.GetProperty("mode").GetString());
        Assert.True(view.TryGetProperty("tab", out _));
        Assert.True(view.TryGetProperty("filterMode", out _));
    }

    [Fact]
    public async Task SessionsView_ReturnsDiffModeViewState()
    {
        _currentTab = 1;

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "view", DiffPid.ToString(), "--json");

        Assert.Equal(0, exitCode);
        var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;

        Assert.Equal("diff", root.GetProperty("mode").GetString());
        Assert.Equal(1, root.GetProperty("tab").GetInt32());
    }

    // --- Helpers ---

    private static JsonElement? FindSessionByPid(JsonElement sessions, int pid)
    {
        foreach (var session in sessions.EnumerateArray())
        {
            if (session.GetProperty("pid").GetInt32() == pid)
                return session;
        }

        return null;
    }

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
