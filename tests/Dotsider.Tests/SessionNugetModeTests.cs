using System.Diagnostics;
using System.Text.Json;
using Dotsider.Core.Analysis;
using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end tests verifying that dotsider sessions list discovers
/// instances running in nuget mode, and that the diagnostics socket
/// responds correctly to assembly-info, get-current-view, and CLI commands.
/// </summary>
[Collection("SampleAssemblies")]
public class SessionNugetModeTests(SampleAssemblyFixture samples) : IAsyncLifetime
{
    private static readonly string s_projectPath = Path.Combine(
        TestHelpers.GetRepoRoot(), "src", "Dotsider");

    private static readonly string s_buildConfig = DetectBuildConfig();

    // Use a high PID that won't collide with real processes
    private const int NugetPid = 888_002;

    private readonly SampleAssemblyFixture _samples = samples;

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
    public async ValueTask InitializeAsync()
    {
        _packageAnalyzer = new NuGetPackageAnalyzer(_samples.RichLibraryNupkg);

        _listener = new DotsiderDiagnosticsListener(
            () => null,
            assemblyInfoProvider: () => new
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
            },
            currentViewProvider: () => new
            {
                Mode = "nuget",
                IsBrowsingPackage = _isBrowsingPackage,
                Tab = _selectedDllTab is { } t ? t + 1 : (int?)null,
                SelectedDll = _selectedDllName,
            });
        _listener.StartListening(overridePid: NugetPid);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Releases fixture state after tests complete.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await _listener.DisposeAsync();
        _packageAnalyzer.Dispose();
    }

    // --- Session discovery tests ---

    /// <summary>
    /// Verifies sessions list finds nuget mode instance.
    /// </summary>
    [Fact]
    public async Task SessionsList_FindsNugetModeInstance()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync("sessions", "list", "--json");

        Assert.Equal(0, exitCode);
        var sessions = JsonSerializer.Deserialize<JsonElement>(stdout);
        Assert.Equal(JsonValueKind.Array, sessions.ValueKind);

        var nugetSession = FindSessionByPid(sessions, NugetPid);
        Assert.NotNull(nugetSession);
        Assert.Equal("nuget", nugetSession.Value.GetProperty("mode").GetString());
        Assert.Contains(".nupkg", nugetSession.Value.GetProperty("fileName").GetString());
    }

    /// <summary>
    /// Verifies sessions list table output shows nuget mode.
    /// </summary>
    [Fact]
    public async Task SessionsList_TableOutput_ShowsNugetMode()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync("sessions", "list");

        Assert.Equal(0, exitCode);
        Assert.Contains("Mode", stdout);
        Assert.Contains("nuget", stdout);
    }

    // --- Direct socket tests ---

    /// <summary>
    /// Verifies nuget listener responds with real package data.
    /// </summary>
    [Fact]
    public async Task NugetListener_RespondsWithRealPackageData()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await DotsiderClient.TryProbeAsync(
            SessionDiscovery.GetDotsiderSocketPath(NugetPid), ct);

        Assert.NotNull(response);
        Assert.True(response.Success);

        var data = response.Data as JsonElement?;
        Assert.NotNull(data);
        Assert.Equal("nuget", data.Value.GetProperty("mode").GetString());

        // Verify real package data is present
        Assert.Equal("RichLibrary", data.Value.GetProperty("packageId").GetString());
        Assert.Equal("2.5.1", data.Value.GetProperty("packageVersion").GetString());
        Assert.True(data.Value.GetProperty("dllCount").GetInt32() > 0);
    }

    /// <summary>
    /// Verifies nuget listener returns current view browsing package.
    /// </summary>
    [Fact]
    public async Task NugetListener_ReturnsCurrentView_BrowsingPackage()
    {
        var ct = TestContext.Current.CancellationToken;
        var socketPath = SessionDiscovery.GetDotsiderSocketPath(NugetPid);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);

        Assert.True(response.Success);
        var data = response.Data as JsonElement?;
        Assert.NotNull(data);
        Assert.Equal("nuget", data.Value.GetProperty("mode").GetString());
        Assert.True(data.Value.GetProperty("isBrowsingPackage").GetBoolean());
        // Tab is null when browsing package — omitted from JSON (WhenWritingNull)
        Assert.False(data.Value.TryGetProperty("tab", out _));
    }

    /// <summary>
    /// Verifies nuget listener returns current view dll selected.
    /// </summary>
    [Fact]
    public async Task NugetListener_ReturnsCurrentView_DllSelected()
    {
        var ct = TestContext.Current.CancellationToken;
        var socketPath = SessionDiscovery.GetDotsiderSocketPath(NugetPid);

        // Simulate DLL selection
        _isBrowsingPackage = false;
        _selectedDllTab = TabId.Strings;
        _selectedDllName = "RichLibrary.dll";

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);

        Assert.True(response.Success);
        var data = response.Data as JsonElement?;
        Assert.NotNull(data);
        Assert.Equal("nuget", data.Value.GetProperty("mode").GetString());
        Assert.False(data.Value.GetProperty("isBrowsingPackage").GetBoolean());
        Assert.Equal(TabId.Strings + 1, data.Value.GetProperty("tab").GetInt32());
        Assert.Equal("RichLibrary.dll", data.Value.GetProperty("selectedDll").GetString());
    }

    /// <summary>
    /// Verifies nuget listener rejects unsupported methods.
    /// </summary>
    [Fact]
    public async Task NugetListener_RejectsUnsupportedMethods()
    {
        var ct = TestContext.Current.CancellationToken;
        var socketPath = SessionDiscovery.GetDotsiderSocketPath(NugetPid);
        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "list-types" }, ct);

        // Should fail because nuget mode has no DotsiderState
        Assert.False(response.Success);
    }

    // --- CLI sessions info/view tests ---

    /// <summary>
    /// Verifies sessions info returns nuget mode data.
    /// </summary>
    [Fact]
    public async Task SessionsInfo_ReturnsNugetModeData()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "info", NugetPid.ToString(), "--json");

        Assert.Equal(0, exitCode);
        var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;

        // assembly-info portion
        var info = root.GetProperty("assemblyInfo");
        Assert.Equal("nuget", info.GetProperty("mode").GetString());
        Assert.Equal("RichLibrary", info.GetProperty("packageId").GetString());
        Assert.True(info.GetProperty("dllCount").GetInt32() > 0);

        // get-current-view portion
        var view = root.GetProperty("currentView");
        Assert.Equal("nuget", view.GetProperty("mode").GetString());
        Assert.True(view.TryGetProperty("isBrowsingPackage", out _));
    }

    /// <summary>
    /// Verifies sessions view returns nuget mode view state.
    /// </summary>
    [Fact]
    public async Task SessionsView_ReturnsNugetModeViewState()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "sessions", "view", NugetPid.ToString(), "--json");

        Assert.Equal(0, exitCode);
        var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;

        Assert.Equal("nuget", root.GetProperty("mode").GetString());
        Assert.True(root.TryGetProperty("isBrowsingPackage", out _));
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
