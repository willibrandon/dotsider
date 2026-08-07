using Dotsider.Core.Analysis;
using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using System.Diagnostics;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end tests verifying that dotsider sessions list discovers
/// instances running in nuget mode, and that the diagnostics socket
/// responds correctly to assembly-info, get-current-view, and CLI commands.
/// </summary>
[TestClass]
public class SessionNugetModeTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private static readonly string s_projectPath = Path.Combine(
        TestHelpers.GetRepoRoot(), "src", "Dotsider");

    private static readonly string s_buildConfig = DetectBuildConfig();

    private readonly SampleAssemblyFixture _samples = Samples;
    private int _nugetPid;

    // Mutable view state exposed to currentViewProvider
    private bool _isBrowsingPackage = true;
    private int? _selectedDllTab;
    private string? _selectedDllName;

    private NuGetPackageAnalyzer _packageAnalyzer = null!;
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
        _packageAnalyzer = new NuGetPackageAnalyzer(_samples.RichLibraryNupkg);

        _listener = new DotsiderDiagnosticsListener(
            () => null,
            assemblyInfoProvider: () => TestJsonResponse.Element(new
            {
                Mode = "nuget",
                _packageAnalyzer.FilePath,
                _packageAnalyzer.FileName,
                _packageAnalyzer.PackageId,
                _packageAnalyzer.PackageVersion,
                _packageAnalyzer.Authors,
                _packageAnalyzer.Description,
                DllCount = _packageAnalyzer.DllFiles.Count,
                SelectedDll = _selectedDllName,
            }),
            currentViewProvider: () => TestJsonResponse.Element(new
            {
                Mode = "nuget",
                IsBrowsingPackage = _isBrowsingPackage,
                Tab = _selectedDllTab is { } t ? t + 1 : (int?)null,
                SelectedDll = _selectedDllName,
            }));
        _nugetPid = TestSocketIds.NextPid();
        _listener.StartListening(overridePid: _nugetPid);

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
        _packageAnalyzer?.Dispose();
    }

    // --- Session discovery tests ---

    /// <summary>
    /// Verifies sessions list finds nuget mode instance.
    /// </summary>
    [TestMethod]
    public async Task SessionsList_FindsNugetModeInstance()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync("sessions", "list", "--json");

        Assert.AreEqual(0, exitCode);
        var sessions = JsonSerializer.Deserialize<JsonElement>(stdout);
        Assert.AreEqual(JsonValueKind.Array, sessions.ValueKind);

        var nugetSession = FindSessionByPid(sessions, _nugetPid);
        Assert.IsNotNull(nugetSession);
        Assert.AreEqual("nuget", nugetSession.Value.GetProperty("mode").GetString());
        Assert.Contains(".nupkg", nugetSession.Value.GetProperty("fileName").GetString()!);
    }

    /// <summary>
    /// Verifies sessions list table output shows nuget mode.
    /// </summary>
    [TestMethod]
    public async Task SessionsList_TableOutput_ShowsNugetMode()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync("sessions", "list");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Mode", stdout);
        Assert.Contains(_nugetPid.ToString(), stdout);
        Assert.Contains("nuget", stdout);
    }

    // --- Direct socket tests ---

    /// <summary>
    /// Verifies nuget listener responds with real package data.
    /// </summary>
    [TestMethod]
    public async Task NugetListener_RespondsWithRealPackageData()
    {
        var ct = CancellationToken.None;
        var response = await DotsiderClient.TryProbeAsync(
            SessionDiscovery.GetDotsiderSocketPath(_nugetPid), ct);

        Assert.IsNotNull(response);
        Assert.IsTrue(response.Success);

        var data = response.Data;
        Assert.IsNotNull(data);
        Assert.AreEqual("nuget", data.Value.GetProperty("mode").GetString());

        // Verify real package data is present
        Assert.AreEqual("RichLibrary", data.Value.GetProperty("packageId").GetString());
        Assert.AreEqual("2.5.1", data.Value.GetProperty("packageVersion").GetString());
        Assert.IsGreaterThan(0, data.Value.GetProperty("dllCount").GetInt32());
    }

    /// <summary>
    /// Verifies nuget listener returns current view browsing package.
    /// </summary>
    [TestMethod]
    public async Task NugetListener_ReturnsCurrentView_BrowsingPackage()
    {
        var ct = CancellationToken.None;
        var socketPath = SessionDiscovery.GetDotsiderSocketPath(_nugetPid);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);

        Assert.IsTrue(response.Success);
        var data = response.Data;
        Assert.IsNotNull(data);
        Assert.AreEqual("nuget", data.Value.GetProperty("mode").GetString());
        Assert.IsTrue(data.Value.GetProperty("isBrowsingPackage").GetBoolean());
        // Tab is null when browsing package — omitted from JSON (WhenWritingNull)
        Assert.IsFalse(data.Value.TryGetProperty("tab", out _));
    }

    /// <summary>
    /// Verifies nuget listener returns current view dll selected.
    /// </summary>
    [TestMethod]
    public async Task NugetListener_ReturnsCurrentView_DllSelected()
    {
        var ct = CancellationToken.None;
        var socketPath = SessionDiscovery.GetDotsiderSocketPath(_nugetPid);

        // Simulate DLL selection
        _isBrowsingPackage = false;
        _selectedDllTab = TabId.Strings;
        _selectedDllName = "RichLibrary.dll";

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);

        Assert.IsTrue(response.Success);
        var data = response.Data;
        Assert.IsNotNull(data);
        Assert.AreEqual("nuget", data.Value.GetProperty("mode").GetString());
        Assert.IsFalse(data.Value.GetProperty("isBrowsingPackage").GetBoolean());
        Assert.AreEqual(TabId.Strings + 1, data.Value.GetProperty("tab").GetInt32());
        Assert.AreEqual("RichLibrary.dll", data.Value.GetProperty("selectedDll").GetString());
    }

    /// <summary>
    /// Verifies nuget listener rejects unsupported methods.
    /// </summary>
    [TestMethod]
    public async Task NugetListener_RejectsUnsupportedMethods()
    {
        var ct = CancellationToken.None;
        var socketPath = SessionDiscovery.GetDotsiderSocketPath(_nugetPid);
        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "list-types" }, ct);

        // Should fail because nuget mode has no DotsiderState
        Assert.IsFalse(response.Success);
    }

    // --- CLI sessions info/view tests ---

    /// <summary>
    /// Verifies sessions info returns nuget mode data.
    /// </summary>
    [TestMethod]
    public async Task SessionsInfo_ReturnsNugetModeData()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "info", _nugetPid.ToString(), "--json");

        Assert.AreEqual(0, exitCode);
        var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;

        // assembly-info portion
        var info = root.GetProperty("assemblyInfo");
        Assert.AreEqual("nuget", info.GetProperty("mode").GetString());
        Assert.AreEqual("RichLibrary", info.GetProperty("packageId").GetString());
        Assert.IsGreaterThan(0, info.GetProperty("dllCount").GetInt32());

        // get-current-view portion
        var view = root.GetProperty("currentView");
        Assert.AreEqual("nuget", view.GetProperty("mode").GetString());
        Assert.IsTrue(view.TryGetProperty("isBrowsingPackage", out _));
    }

    /// <summary>
    /// Verifies sessions view returns nuget mode view state.
    /// </summary>
    [TestMethod]
    public async Task SessionsView_ReturnsNugetModeViewState()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "view", _nugetPid.ToString(), "--json");

        Assert.AreEqual(0, exitCode);
        var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;

        Assert.AreEqual("nuget", root.GetProperty("mode").GetString());
        Assert.IsTrue(root.TryGetProperty("isBrowsingPackage", out _));
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
        TestProcessEnvironment.RemoveCodeCoverageVariables(psi);

        var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout, stderr);
    }

    private static string QuoteArg(string arg)
        => arg.Contains(' ') ? $"\"{arg}\"" : arg;
}
