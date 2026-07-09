using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Nu Get Mode View.
/// </summary>
[TestClass]
public class NuGetModeViewTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private NuGetState? _state;

    private (Hex1bTerminal terminal, Hex1bApp app) CreateNuGetApp()
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(120, 30)
            .Build();
        NuGetApp? nugetApp = null;
        _hex1bApp = new Hex1bApp(
            ctx =>
            {
                _state ??= new NuGetState(_hex1bApp!, Samples.RichLibraryNupkg);
                nugetApp ??= new NuGetApp(_state);
                return Task.FromResult<Hex1bWidget>(nugetApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
            });
        return (_terminal, _hex1bApp);
    }

    /// <summary>
    /// Verifies nu get app launches shows package info.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task NuGetApp_Launches_ShowsPackageInfo()
    {
        var (terminal, app) = CreateNuGetApp();
        var ct = CancellationToken.None;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary") || s.ContainsText("nupkg"), TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        await runTask.ContinueWith(_ => { }, ct);
    }

    /// <summary>
    /// Verifies nu get app shows file list.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task NuGetApp_ShowsFileList()
    {
        var (terminal, app) = CreateNuGetApp();
        var ct = CancellationToken.None;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s =>
                s.ContainsText(".dll") || s.ContainsText(".nuspec") || s.ContainsText("DLL"),
                TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        await runTask.ContinueWith(_ => { }, ct);
    }

    /// <summary>
    /// Verifies quit key exits nu get app.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task QuitKey_ExitsNuGetApp()
    {
        var (terminal, app) = CreateNuGetApp();
        var ct = CancellationToken.None;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary") || s.ContainsText("nupkg"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Q)
            .Build()
            .ApplyAsync(terminal, ct);

        var completed = await Task.WhenAny(runTask, Task.Delay(5000, ct));
        Assert.AreEqual(runTask, completed);
    }

    /// <summary>
    /// Verifies enter on dll row opens dll inspector.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Enter_OnDllRow_OpensDllInspector()
    {
        var (terminal, app) = CreateNuGetApp();
        var ct = CancellationToken.None;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary.dll"), TimeSpan.FromSeconds(10))
            // Focus the DLL row and press Enter
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => !_state!.IsBrowsingPackage, TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsFalse(_state!.IsBrowsingPackage);
        Assert.IsNotNull(_state.SelectedDllState);
        Assert.IsNotNull(_state.SelectedDllEntry);

        await runTask.ContinueWith(_ => { }, ct);
    }

    /// <summary>
    /// Verifies search activates and dismisses.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Search_ActivatesAndDismisses()
    {
        var (terminal, app) = CreateNuGetApp();
        var ct = CancellationToken.None;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary") || s.ContainsText("DLL"), TimeSpan.FromSeconds(10))
            // Activate search
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.BrowserSearch.IsActive, TimeSpan.FromSeconds(10))
            // Dismiss with Esc
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => !_state!.BrowserSearch.IsActive, TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsFalse(_state!.BrowserSearch.IsActive);

        await runTask.ContinueWith(_ => { }, ct);
    }

    /// <summary>
    /// Verifies dll inspector depth limit shows error in hints bar.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task DllInspector_DepthLimit_ShowsErrorInHintsBar()
    {
        var (terminal, app) = CreateNuGetApp();
        var ct = CancellationToken.None;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);
        var depthLimitHit = false;

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary.dll"), TimeSpan.FromSeconds(10))
            // Enter DLL inspector
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => !_state!.IsBrowsingPackage, TimeSpan.FromSeconds(10))
            // Push assemblies to hit the depth limit, then verify the error renders
            .WaitUntil(s =>
            {
                if (!depthLimitHit)
                {
                    var dllState = _state!.SelectedDllState!;
                    for (var i = 0; i < DotsiderState.MaxNavigationDepth; i++)
                    {
                        var path = i % 2 == 0 ? Samples.RichLibraryDll : Samples.EmptyLibDll;
                        dllState.PushAssembly(path);
                    }
                    // This push should fail with depth limit
                    dllState.PushAssembly(Samples.ComplexAppDll);
                    _state.App.Invalidate();
                    depthLimitHit = true;
                }

                return s.ContainsText("depth limit");
            }, TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Contains("depth limit", _state!.SelectedDllState!.NavigationError!);

        await runTask.ContinueWith(_ => { }, ct);
    }

    /// <summary>
    /// Verifies the NuGet DLL inspector unwinds the PE → IL → Hex chain on
    /// two Esc presses without ejecting back to the package browser. Mirrors
    /// the behavior already covered for standalone dotsider in
    /// <see cref="DotsiderStateTests.NavigateToIlMethod_ThenHex_ThenBack_RestoresIl"/>.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task NuGet_EscBack_FromPeIlHexChain_TwoEscsReturnToPe()
    {
        var (terminal, app) = CreateNuGetApp();
        var ct = CancellationToken.None;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);
        var chainPrepared = false;

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary.dll"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => !_state!.IsBrowsingPackage, TimeSpan.FromSeconds(10))
            .WaitUntil(s =>
            {
                // Set up a PE → IL → Hex chain on the inner DLL state, then drive
                // real Esc keys through the NuGet Escape handler.
                var dllState = _state!.SelectedDllState!;
                if (!chainPrepared)
                {
                    dllState.CurrentTab = TabId.PeMetadata;
                    dllState.PeSubTab = PeSubTabId.MethodDef;
                    var method = dllState.Analyzer.MethodDefs.First(m => m.Rva > 0);
                    dllState.NavigateToIlMethod(method);
                    dllState.NavigateToHexOffset(method.Rva);
                    chainPrepared = true;
                }

                return dllState.CurrentTab == TabId.HexDump && dllState.CrossViewBackTarget is not null;
            }, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Data Interpretation"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state!.SelectedDllState!.CurrentTab == TabId.IlInspector,
                TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state!.SelectedDllState!.CurrentTab == TabId.PeMetadata,
                TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        // Two Escs landed back on PE Metadata with the original sub-tab — and
        // the user is still inside the DLL inspector (not ejected to the package
        // browser, which is the pre-fix fall-through branch in NuGet's Esc handler).
        Assert.IsNotNull(_state!.SelectedDllState);
        Assert.IsFalse(_state.IsBrowsingPackage);
        Assert.AreEqual(TabId.PeMetadata, _state.SelectedDllState.CurrentTab);
        Assert.AreEqual(PeSubTabId.MethodDef, _state.SelectedDllState.PeSubTab);
        Assert.IsNull(_state.SelectedDllState.CrossViewBackTarget);

        await runTask.ContinueWith(_ => { }, ct);
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
    }
}
