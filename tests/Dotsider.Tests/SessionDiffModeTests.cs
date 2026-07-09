using Dotsider.Core.Analysis;
using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using System.Diagnostics;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end tests verifying that dotsider sessions list discovers
/// instances running in diff mode, and that the diagnostics socket
/// responds correctly to assembly-info, get-current-view, and CLI commands.
/// </summary>
[TestClass]
public class SessionDiffModeTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private static readonly string s_projectPath = Path.Combine(
        TestHelpers.GetRepoRoot(), "src", "Dotsider");

    private static readonly string s_buildConfig = DetectBuildConfig();

    private readonly SampleAssemblyFixture _samples = Samples;
    private int _diffPid;

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

    /// <summary>
    /// Prepares the fixture state before tests execute.
    /// </summary>
    [TestInitialize]
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
                Tab = _currentTab + 1,
                FilterMode = _filterMode,
            });
        _diffPid = TestSocketIds.NextPid();
        _listener.StartListening(overridePid: _diffPid);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Releases fixture state after tests complete.
    /// </summary>
    [TestCleanup]
    public async ValueTask DisposeAsync()
    {
        if (_listener is not null)
            await _listener.DisposeAsync();
        _leftAnalyzer?.Dispose();
        _rightAnalyzer?.Dispose();
    }

    // --- Session discovery tests ---

    /// <summary>
    /// Verifies sessions list finds diff mode instance.
    /// </summary>
    [TestMethod]
    public async Task SessionsList_FindsDiffModeInstance()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync("sessions", "list", "--json");

        Assert.AreEqual(0, exitCode);
        var sessions = JsonSerializer.Deserialize<JsonElement>(stdout);
        Assert.AreEqual(JsonValueKind.Array, sessions.ValueKind);

        var diffSession = FindSessionByPid(sessions, _diffPid);
        Assert.IsNotNull(diffSession);
        Assert.AreEqual("diff", diffSession.Value.GetProperty("mode").GetString());
        Assert.Contains("\u2194", diffSession.Value.GetProperty("fileName").GetString()!);
    }

    /// <summary>
    /// Verifies sessions list table output shows diff mode.
    /// </summary>
    [TestMethod]
    public async Task SessionsList_TableOutput_ShowsDiffMode()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync("sessions", "list");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Mode", stdout);
        Assert.Contains(_diffPid.ToString(), stdout);
        Assert.Contains("diff", stdout);
    }

    // --- Direct socket tests ---

    /// <summary>
    /// Verifies diff listener responds with real analyzer data.
    /// </summary>
    [TestMethod]
    public async Task DiffListener_RespondsWithRealAnalyzerData()
    {
        var ct = CancellationToken.None;
        var response = await DotsiderClient.TryProbeAsync(
            SessionDiscovery.GetDotsiderSocketPath(_diffPid), ct);

        Assert.IsNotNull(response);
        Assert.IsTrue(response.Success);

        var data = response.Data as JsonElement?;
        Assert.IsNotNull(data);
        Assert.AreEqual("diff", data.Value.GetProperty("mode").GetString());

        // Verify real analyzer data is present
        var left = data.Value.GetProperty("left");
        var right = data.Value.GetProperty("right");
        Assert.AreEqual("RichLibrary", left.GetProperty("assemblyName").GetString());
        Assert.AreEqual("RichLibrary", right.GetProperty("assemblyName").GetString());
        Assert.AreNotEqual(
            left.GetProperty("assemblyVersion").GetString(),
            right.GetProperty("assemblyVersion").GetString());
    }

    /// <summary>
    /// Verifies diff listener returns current view.
    /// </summary>
    [TestMethod]
    public async Task DiffListener_ReturnsCurrentView()
    {
        var ct = CancellationToken.None;
        var socketPath = SessionDiscovery.GetDotsiderSocketPath(_diffPid);

        // Set a non-default tab before querying
        _currentTab = 2;
        _filterMode = DiffFilterMode.AddedOnly;

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);

        Assert.IsTrue(response.Success);
        var data = response.Data as JsonElement?;
        Assert.IsNotNull(data);
        Assert.AreEqual("diff", data.Value.GetProperty("mode").GetString());
        Assert.AreEqual(3, data.Value.GetProperty("tab").GetInt32());
        Assert.AreEqual("addedOnly", data.Value.GetProperty("filterMode").GetString());
    }

    /// <summary>
    /// Verifies diff listener rejects unsupported methods.
    /// </summary>
    [TestMethod]
    public async Task DiffListener_RejectsUnsupportedMethods()
    {
        var ct = CancellationToken.None;
        var socketPath = SessionDiscovery.GetDotsiderSocketPath(_diffPid);
        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "list-types" }, ct);

        // Should fail because diff mode has no DotsiderState
        Assert.IsFalse(response.Success);
    }

    // --- CLI sessions info/view tests ---

    /// <summary>
    /// Verifies sessions info returns diff mode data.
    /// </summary>
    [TestMethod]
    public async Task SessionsInfo_ReturnsDiffModeData()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "info", _diffPid.ToString(), "--json");

        Assert.AreEqual(0, exitCode);
        var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;

        // assembly-info portion
        var info = root.GetProperty("assemblyInfo");
        Assert.AreEqual("diff", info.GetProperty("mode").GetString());
        Assert.IsTrue(info.TryGetProperty("left", out _));
        Assert.IsTrue(info.TryGetProperty("right", out _));

        // get-current-view portion
        var view = root.GetProperty("currentView");
        Assert.AreEqual("diff", view.GetProperty("mode").GetString());
        Assert.IsTrue(view.TryGetProperty("tab", out _));
        Assert.IsTrue(view.TryGetProperty("filterMode", out _));
    }

    /// <summary>
    /// Verifies sessions view returns diff mode view state.
    /// </summary>
    [TestMethod]
    public async Task SessionsView_ReturnsDiffModeViewState()
    {
        _currentTab = 1;

        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "view", _diffPid.ToString(), "--json");

        Assert.AreEqual(0, exitCode);
        var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;

        Assert.AreEqual("diff", root.GetProperty("mode").GetString());
        Assert.AreEqual(2, root.GetProperty("tab").GetInt32());
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
