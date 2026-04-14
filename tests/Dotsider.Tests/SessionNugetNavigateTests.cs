using System.Text.Json;
using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Live end-to-end tests for NuGet mode with a headless TUI.
/// Starts a headless NuGetApp with a real DotsiderDiagnosticsListener wired
/// exactly like Program.RunTui NuGet mode. Verifies navigation, mutation
/// draining, get-current-view, search, and start-trace via the diagnostics socket.
/// </summary>
[Collection("SampleAssemblies")]
public class SessionNugetNavigateTests(SampleAssemblyFixture samples) : IAsyncDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _app;
    private NuGetState? _nugetState;
    private DotsiderDiagnosticsListener? _listener;

    /// <summary>
    /// Starts a headless NuGet TUI with the diagnostics socket listener,
    /// reproducing the full production stack from Program.RunTui NuGet mode.
    /// </summary>
    private async Task<(Hex1bApp app, string socketPath)> StartNugetTuiWithDiagnosticsAsync(
        string nupkgPath, CancellationToken ct)
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(120, 30)
            .Build();

        _app = new Hex1bApp(
            ctx =>
            {
                _nugetState ??= new NuGetState(_app!, nupkgPath);

                var nugetApp = new NuGetApp(_nugetState);
                return Task.FromResult<Hex1bWidget>(nugetApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
            });

        NuGetState? CapturedNugetState() => _nugetState;

        _listener = new DotsiderDiagnosticsListener(
            () => CapturedNugetState()?.SelectedDllState,
            assemblyInfoProvider: () =>
            {
                var s = CapturedNugetState();
                if (s is null) return null;
                return new
                {
                    Mode = "nuget",
                    s.Package.FilePath,
                    s.Package.FileName,
                    s.Package.PackageId,
                    s.Package.PackageVersion,
                    s.Package.Authors,
                    s.Package.Description,
                    DllCount = s.Package.DllFiles.Count,
                    SelectedDll = s.SelectedDllState?.Analyzer.FileName,
                };
            },
            currentViewProvider: () =>
            {
                var s = CapturedNugetState();
                if (s is null) return null;
                return new
                {
                    Mode = "nuget",
                    s.IsBrowsingPackage,
                    Tab = s.SelectedDllState is { } dll ? dll.CurrentTab + 1 : (int?)null,
                    SelectedDll = s.SelectedDllEntry?.Name,
                };
            });
        _listener.StartListening();

        _ = _app.RunAsync(ct);
        await Task.Delay(100, ct);

        await TestHelpers.WaitUntilAsync(
            () => _nugetState is not null,
            TimeSpan.FromSeconds(10));

        return (_app, _listener.SocketPath!);
    }

    /// <summary>
    /// Opens the first DLL in the NuGet package, simulating the user pressing Enter.
    /// </summary>
    private void OpenFirstDll()
    {
        var entry = _nugetState!.Package.DllFiles[0];
        var analyzer = _nugetState.Package.OpenDll(entry);
        _nugetState.SelectedDllState?.Dispose();
        _nugetState.SelectedDllState = new DotsiderState(_app!, analyzer);
        _nugetState.SelectedDllEntry = entry;
        _nugetState.IsBrowsingPackage = false;
        _app!.Invalidate();
    }

    /// <summary>
    /// Verifies navigate via socket changes dll tab.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Navigate_ViaSocket_ChangesDllTab()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartNugetTuiWithDiagnosticsAsync(samples.RichLibraryNupkg, ct);

        // Open a DLL so SelectedDllState is available
        OpenFirstDll();

        // Verify initial tab is 0 (General)
        Assert.Equal(TabId.General, _nugetState!.SelectedDllState!.CurrentTab);

        // Navigate to Strings tab via the socket (1-based: Strings = 4)
        var navResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate", TabId = TabId.Strings + 1 }, ct);
        Assert.True(navResponse.Success);

        // Wait for the render loop to drain the mutation queue
        await TestHelpers.WaitUntilAsync(
            () => _nugetState.SelectedDllState!.CurrentTab == TabId.Strings,
            TimeSpan.FromSeconds(10));

        Assert.Equal(TabId.Strings, _nugetState.SelectedDllState.CurrentTab);
    }

    /// <summary>
    /// Verifies get current view reflects navigated tab.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task GetCurrentView_ReflectsNavigatedTab()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartNugetTuiWithDiagnosticsAsync(samples.RichLibraryNupkg, ct);

        OpenFirstDll();

        // Navigate to PE/Metadata tab (1-based: PE/Metadata = 2)
        var navResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate", TabId = TabId.PeMetadata + 1 }, ct);
        Assert.True(navResponse.Success);

        await TestHelpers.WaitUntilAsync(
            () => _nugetState!.SelectedDllState!.CurrentTab == TabId.PeMetadata,
            TimeSpan.FromSeconds(10));

        // get-current-view should report the navigated tab
        var viewResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.True(viewResponse.Success);

        var data = viewResponse.Data as JsonElement?;
        Assert.NotNull(data);
        Assert.Equal("nuget", data.Value.GetProperty("mode").GetString());
        Assert.False(data.Value.GetProperty("isBrowsingPackage").GetBoolean());
        Assert.Equal(TabId.PeMetadata + 1, data.Value.GetProperty("tab").GetInt32());
        Assert.Equal(
            _nugetState!.SelectedDllEntry!.Name,
            data.Value.GetProperty("selectedDll").GetString());
    }

    /// <summary>
    /// Verifies get current view before dll opened shows browsing package.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task GetCurrentView_BeforeDllOpened_ShowsBrowsingPackage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartNugetTuiWithDiagnosticsAsync(samples.RichLibraryNupkg, ct);

        // Don't open a DLL — should still be browsing the package
        var viewResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.True(viewResponse.Success);

        var data = viewResponse.Data as JsonElement?;
        Assert.NotNull(data);
        Assert.Equal("nuget", data.Value.GetProperty("mode").GetString());
        Assert.True(data.Value.GetProperty("isBrowsingPackage").GetBoolean());
        // Tab is null when browsing package — omitted from JSON (WhenWritingNull)
        Assert.False(data.Value.TryGetProperty("tab", out _));
    }

    /// <summary>
    /// Verifies search via socket succeeds.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Search_ViaSocket_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartNugetTuiWithDiagnosticsAsync(samples.RichLibraryNupkg, ct);

        OpenFirstDll();

        // Search should succeed through the NuGet listener's getState → SelectedDllState pipeline
        var searchResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "search", Query = "test" }, ct);
        Assert.True(searchResponse.Success);
    }

    /// <summary>
    /// Verifies start trace fails for library dll.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task StartTrace_FailsForLibraryDll()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartNugetTuiWithDiagnosticsAsync(samples.RichLibraryNupkg, ct);

        OpenFirstDll();

        // Library DLLs have no entry point — start-trace should fail
        var traceResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "start-trace" }, ct);
        Assert.False(traceResponse.Success);
        Assert.Contains("entry point", traceResponse.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies navigate before dll opened fails.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Navigate_BeforeDllOpened_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartNugetTuiWithDiagnosticsAsync(samples.RichLibraryNupkg, ct);

        // Don't open a DLL — navigate should fail because getState returns null
        var navResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate", TabId = TabId.Strings + 1 }, ct);
        Assert.False(navResponse.Success);
    }

    /// <summary>
    /// Releases fixture state after tests complete.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (_listener is not null)
            await _listener.DisposeAsync();
        _nugetState?.Dispose();
        if (_terminal is not null)
            await _terminal.DisposeAsync();
    }
}
