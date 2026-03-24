using System.Text.Json;
using Dotsider.Core.Analysis;
using Dotsider.Diagnostics;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// MCP NavigationTools integration tests using real DotsiderDiagnosticsListeners
/// wired to real analyzers. These exercise the actual currentViewProvider and
/// getState paths that regressed in diff/nuget modes.
/// </summary>
[Collection("SampleAssemblies")]
public class NavigationToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    private readonly List<IAsyncDisposable> _disposables = [];

    // --- Diff mode: real listener with real AssemblyAnalyzers ---

    [Fact]
    public async Task GetCurrentView_DiffMode_ReturnsTabAndFilterMode()
    {
        var currentTab = 2;
        var filterMode = DiffFilterMode.AddedOnly;

        await using var listener = CreateDiffListener(
            samples.RichLibraryDll, samples.RichLibraryV2Dll,
            () => currentTab, () => filterMode);

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_current_view",
            new Dictionary<string, object?> { ["sessionId"] = listener.Pid },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);

        var doc = JsonDocument.Parse(text!);
        Assert.Equal("diff", doc.RootElement.GetProperty("mode").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("tab").GetInt32());
        Assert.Equal("addedOnly", doc.RootElement.GetProperty("filterMode").GetString());
    }

    [Fact]
    public async Task GetCurrentView_NugetMode_BrowsingPackage()
    {
        await using var listener = CreateNugetListener(samples.RichLibraryNupkg);

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_current_view",
            new Dictionary<string, object?> { ["sessionId"] = listener.Pid },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);

        var doc = JsonDocument.Parse(text!);
        Assert.Equal("nuget", doc.RootElement.GetProperty("mode").GetString());
        Assert.True(doc.RootElement.GetProperty("isBrowsingPackage").GetBoolean());
    }

    [Fact]
    public async Task GetCurrentView_NugetMode_DllSelected()
    {
        await using var listener = CreateNugetListener(
            samples.RichLibraryNupkg,
            selectDll: true, selectedDllTab: 3);

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_current_view",
            new Dictionary<string, object?> { ["sessionId"] = listener.Pid },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);

        var doc = JsonDocument.Parse(text!);
        Assert.Equal("nuget", doc.RootElement.GetProperty("mode").GetString());
        Assert.False(doc.RootElement.GetProperty("isBrowsingPackage").GetBoolean());
        Assert.Equal(4, doc.RootElement.GetProperty("tab").GetInt32());
    }

    [Fact]
    public async Task NavigateTo_DiffMode_FailsBecauseNoState()
    {
        await using var listener = CreateDiffListener(
            samples.RichLibraryDll, samples.RichLibraryV2Dll,
            () => 0, () => DiffFilterMode.All);

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        // Diff mode has getState => null, so navigate should fail
        var result = await client.CallToolAsync(
            "navigate_to",
            new Dictionary<string, object?> { ["sessionId"] = listener.Pid, ["tabId"] = 3 },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.Contains("No assembly is loaded", text);
    }

    [Fact]
    public async Task NavigateTo_NugetMode_NoDllSelected_Fails()
    {
        await using var listener = CreateNugetListener(samples.RichLibraryNupkg);

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "navigate_to",
            new Dictionary<string, object?> { ["sessionId"] = listener.Pid, ["tabId"] = 1 },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.Contains("No assembly is loaded", text);
    }

    // --- Live NuGet navigate: headless TUI + MCP ---

    [Fact]
    public async Task NavigateTo_LiveNuget_OpenedDll_ChangesTabAndVerifiesView()
    {
        var ct = TestCancellationToken;

        // Start a headless NuGet TUI with a real listener, exactly like Program.RunTui
        var (app, nugetState, listener) = await StartLiveNugetTuiAsync(
            samples.RichLibraryNupkg, ct);
        _disposables.Add(listener);

        // Open the first DLL in the package
        var entry = nugetState.Package.DllFiles[0];
        var analyzer = nugetState.Package.OpenDll(entry);
        nugetState.SelectedDllState = new DotsiderState(app, analyzer);
        nugetState.SelectedDllEntry = entry;
        nugetState.IsBrowsingPackage = false;
        app.Invalidate();

        Assert.Equal(TabId.General, nugetState.SelectedDllState.CurrentTab);

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        // Navigate to Strings tab via MCP (1-based: Strings = 4)
        var navResult = await client.CallToolAsync(
            "navigate_to",
            new Dictionary<string, object?>
            {
                ["sessionId"] = listener.Pid,
                ["tabId"] = TabId.Strings + 1
            },
            cancellationToken: ct);
        var navText = GetTextContent(navResult);
        Assert.NotNull(navText);
        Assert.DoesNotContain("Error", navText);

        // Wait for the render loop to drain the mutation queue
        await WaitUntilAsync(
            () => nugetState.SelectedDllState!.CurrentTab == TabId.Strings,
            TimeSpan.FromSeconds(5));

        Assert.Equal(TabId.Strings, nugetState.SelectedDllState.CurrentTab);

        // Verify get_current_view reflects the navigated tab
        var viewResult = await client.CallToolAsync(
            "get_current_view",
            new Dictionary<string, object?> { ["sessionId"] = listener.Pid },
            cancellationToken: ct);
        var viewText = GetTextContent(viewResult);
        Assert.NotNull(viewText);

        var doc = JsonDocument.Parse(viewText!);
        Assert.Equal("nuget", doc.RootElement.GetProperty("mode").GetString());
        Assert.False(doc.RootElement.GetProperty("isBrowsingPackage").GetBoolean());
        Assert.Equal(TabId.Strings + 1, doc.RootElement.GetProperty("tab").GetInt32());
    }

    // --- Disposal ---

    public override async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        foreach (var d in _disposables)
            await d.DisposeAsync();
        _disposables.Clear();

        await base.DisposeAsync();
    }

    // --- Helpers ---

    private static int s_nextPid = 999_901;

    private static RealListenerHandle CreateDiffListener(
        string leftDll, string rightDll,
        Func<int> currentTabProvider,
        Func<DiffFilterMode> filterModeProvider)
    {
        var pid = Interlocked.Increment(ref s_nextPid);
        var leftAnalyzer = new AssemblyAnalyzer(leftDll);
        var rightAnalyzer = new AssemblyAnalyzer(rightDll);

        var listener = new DotsiderDiagnosticsListener(
            () => null,
            assemblyInfoProvider: () => new
            {
                Mode = "diff",
                FileName = $"{leftAnalyzer.FileName} \u2194 {rightAnalyzer.FileName}",
                Left = new
                {
                    leftAnalyzer.FilePath,
                    leftAnalyzer.FileName,
                    leftAnalyzer.FileSize,
                    leftAnalyzer.AssemblyName,
                    leftAnalyzer.AssemblyVersion,
                    leftAnalyzer.TargetFramework,
                },
                Right = new
                {
                    rightAnalyzer.FilePath,
                    rightAnalyzer.FileName,
                    rightAnalyzer.FileSize,
                    rightAnalyzer.AssemblyName,
                    rightAnalyzer.AssemblyVersion,
                    rightAnalyzer.TargetFramework,
                },
            },
            currentViewProvider: () => new
            {
                Mode = "diff",
                Tab = currentTabProvider() + 1,
                FilterMode = filterModeProvider(),
            });
        listener.StartListening(overridePid: pid);

        return new RealListenerHandle(pid, listener, [leftAnalyzer, rightAnalyzer]);
    }

    private static RealListenerHandle CreateNugetListener(
        string nupkgPath,
        bool selectDll = false,
        int selectedDllTab = 0)
    {
        var pid = Interlocked.Increment(ref s_nextPid);
        var packageAnalyzer = new NuGetPackageAnalyzer(nupkgPath);

        string? selectedDllName = null;
        var isBrowsing = !selectDll;

        if (selectDll && packageAnalyzer.DllFiles.Count > 0)
            selectedDllName = packageAnalyzer.DllFiles[0].Name;

        var listener = new DotsiderDiagnosticsListener(
            () => null,
            assemblyInfoProvider: () => new
            {
                Mode = "nuget",
                packageAnalyzer.FilePath,
                packageAnalyzer.FileName,
                packageAnalyzer.PackageId,
                packageAnalyzer.PackageVersion,
                packageAnalyzer.Authors,
                packageAnalyzer.Description,
                DllCount = packageAnalyzer.DllFiles.Count,
                SelectedDll = selectedDllName,
            },
            currentViewProvider: () => new
            {
                Mode = "nuget",
                IsBrowsingPackage = isBrowsing,
                Tab = selectDll ? selectedDllTab + 1 : (int?)null,
                SelectedDll = selectedDllName,
            });
        listener.StartListening(overridePid: pid);

        return new RealListenerHandle(pid, listener, [packageAnalyzer]);
    }

    private async Task<(Hex1bApp app, NuGetState state, RealListenerHandle listener)>
        StartLiveNugetTuiAsync(string nupkgPath, CancellationToken ct)
    {
        var pid = Interlocked.Increment(ref s_nextPid);

        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(120, 30)
            .Build();
        _disposables.Add(terminal);

        NuGetState? nugetState = null;
        Hex1bApp? app = null;

        app = new Hex1bApp(
            ctx =>
            {
                nugetState ??= new NuGetState(app!, nupkgPath);
                var nugetApp = new NuGetApp(nugetState);
                return Task.FromResult<Hex1bWidget>(nugetApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = workload,
                EnableInputCoalescing = false
            });

        NuGetState? CapturedState() => nugetState;

        var listener = new DotsiderDiagnosticsListener(
            () => CapturedState()?.SelectedDllState,
            assemblyInfoProvider: () =>
            {
                var s = CapturedState();
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
                var s = CapturedState();
                if (s is null) return null;
                return new
                {
                    Mode = "nuget",
                    s.IsBrowsingPackage,
                    Tab = s.SelectedDllState is { } dll ? dll.CurrentTab + 1 : (int?)null,
                    SelectedDll = s.SelectedDllEntry?.Name,
                };
            });
        listener.StartListening(overridePid: pid);

        _ = app.RunAsync(ct);

        await WaitUntilAsync(
            () => nugetState is not null,
            TimeSpan.FromSeconds(5));

        var handle = new RealListenerHandle(pid, listener, []);
        return (app, nugetState!, handle);
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition, TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (condition()) return;
            await Task.Delay(100);
        }
        Assert.Fail($"Timed out after {timeout.TotalSeconds:F1}s");
    }

    /// <summary>
    /// Wraps a real DotsiderDiagnosticsListener with its PID and owned disposables.
    /// </summary>
    private sealed class RealListenerHandle(
        int pid,
        DotsiderDiagnosticsListener listener,
        IDisposable[] owned) : IAsyncDisposable
    {
        public int Pid => pid;

        public async ValueTask DisposeAsync()
        {
            await listener.DisposeAsync();
            foreach (var d in owned)
                d.Dispose();
        }
    }
}
