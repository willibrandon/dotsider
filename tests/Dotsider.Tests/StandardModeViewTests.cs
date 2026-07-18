using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Standard Mode View.
/// </summary>
/// <param name="testContext">The current test context.</param>
[TestClass]
public class StandardModeViewTests(TestContext testContext) : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;

    private readonly TestContext _testContext = testContext;

    private (Hex1bTerminal terminal, Hex1bApp app) CreateDotsiderApp(string dllPath, int? initialTab = null)
        => CreateDotsiderAppCore(dllPath, initialTab, enableMouse: false, enableInputCoalescing: false);

    private (Hex1bTerminal terminal, Hex1bApp app) CreateDotsiderAppWithDimensions(
        string dllPath,
        int width,
        int height,
        int? initialTab = null)
        => CreateDotsiderAppCore(dllPath, initialTab, enableMouse: false,
            enableInputCoalescing: false, width, height);

    /// <summary>
    /// Variant of <see cref="CreateDotsiderApp"/> that turns on mouse support, matching the
    /// production <c>EnableMouse = true</c> knob set at <c>Program.cs:209/319/376</c>. Used
    /// by tests that drive mouse wheel, track click, or thumb drag input — without mouse mode
    /// those events would not exercise the production code path.
    /// </summary>
    /// <param name="dllPath">The sample assembly to open.</param>
    /// <param name="initialTab">Optional starting tab id.</param>
    /// <param name="enableInputCoalescing">
    /// Whether to enable input coalescing. Defaults to <see langword="false"/> for
    /// deterministic event-per-frame ordering (the simplest model for most assertions).
    /// Tests that need to exercise the production race where a key arrives in the same
    /// coalesced batch as the click that grabbed scrollbar focus
    /// (<c>Tab6_AfterScrollbarDrag_RightArrow_AdvancesSelection</c>,
    /// <c>Tab6_NoOpScrollbarClick_RightArrow_AdvancesSelection</c>) opt in by passing
    /// <see langword="true"/>, which matches the
    /// <see cref="Hex1b.Hex1bAppOptions.EnableInputCoalescing"/> production default.
    /// </param>
    private (Hex1bTerminal terminal, Hex1bApp app) CreateDotsiderAppWithMouse(
        string dllPath, int? initialTab = null, bool enableInputCoalescing = false)
        => CreateDotsiderAppCore(dllPath, initialTab, enableMouse: true,
            enableInputCoalescing: enableInputCoalescing);

    private (Hex1bTerminal terminal, Hex1bApp app) CreateDotsiderAppCore(
        string dllPath,
        int? initialTab,
        bool enableMouse,
        bool enableInputCoalescing,
        int width = 120,
        int height = 30)
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(width, height)
            .Build();
        DotsiderApp? dotsiderApp = null;
        _hex1bApp = new Hex1bApp(
            ctx =>
            {
                if (_state is null)
                {
                    _state = new DotsiderState(_hex1bApp!, dllPath);
                    if (initialTab.HasValue)
                        _state.CurrentTab = initialTab.Value;
                }
                dotsiderApp ??= new DotsiderApp(_state);
                return Task.FromResult<Hex1bWidget>(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = enableInputCoalescing,
                EnableMouse = enableMouse,
            });
        return (_terminal, _hex1bApp);
    }

    /// <summary>
    /// Waits until the dep-graph vertical <see cref="Hex1b.Nodes.ScrollbarNode"/> has settled and
    /// returns its track geometry as the rightmost column, top row, and height. Reads the live
    /// focus ring, which the render thread rebuilds each frame; a concurrent rebuild surfaces as
    /// <see cref="InvalidOperationException"/> mid-enumeration and is treated as "not settled yet"
    /// so the poll retries. Deriving the click target from the real bounds keeps the track tests
    /// independent of the exact graph layout, which varies with content.
    /// </summary>
    /// <param name="auto">The automator whose default timeout bounds the wait.</param>
    /// <returns>The scrollbar column, the track's top row, and the track height in rows.</returns>
    private async Task<(int Column, int TrackTop, int TrackHeight)> WaitForScrollbarTrackAsync(
        Hex1bTerminalAutomator auto)
    {
        var column = -1;
        var trackTop = -1;
        var trackHeight = 0;
        await auto.WaitUntilAsync(_ =>
        {
            Hex1b.Nodes.ScrollbarNode? scrollbar;
            try { scrollbar = _hex1bApp!.Focusables.OfType<Hex1b.Nodes.ScrollbarNode>().FirstOrDefault(); }
            catch (InvalidOperationException) { return false; }
            if (scrollbar is null || scrollbar.Bounds.Height <= 0) return false;
            var bounds = scrollbar.Bounds;
            column = bounds.X + bounds.Width - 1;
            trackTop = bounds.Y;
            trackHeight = bounds.Height;
            return true;
        }, description: "dep-graph scrollbar track to settle");
        return (column, trackTop, trackHeight);
    }

    /// <summary>
    /// Verifies app launches shows assembly name.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task App_Launches_ShowsAssemblyName()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab2 shows metadata.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab2_ShowsMetadata()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Type("2") // Key 2 → PE/Metadata (TabId 1)
            .WaitUntil(s => s.ContainsText("Sections") || s.ContainsText("TypeDef"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab3 shows il inspector.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab3_ShowsIlInspector()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3) // Tab 3 — IL / Native
            .WaitUntil(s => s.ContainsText("Select a method") || s.ContainsText("IL"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab3 keeps the managed IL Inspector label for ordinary managed assemblies.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab3_Label_ManagedAssembly_IsIlInspector()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("IL Inspector"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab3 is labeled as disassembly for a native-only AOT view.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab3_Label_NativeAot_IsDisassembly()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.NativeAotConsoleExe!);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Disassembly"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab3 is labeled as disassembly for a raw SDK browser-wasm runtime module.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab3_Label_Wasm_IsDisassembly()
    {
        var wasmPath = GetWasmNativePath();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(wasmPath);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Disassembly"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies the Wasm General tab keeps the references section visible in a
    /// short terminal even though the WebAssembly summary is taller than the pane.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task General_Wasm_ShortTerminal_KeepsAssemblyReferencesVisible()
    {
        var wasmPath = GetWasmNativePath();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderAppWithDimensions(wasmPath, width: 110, height: 20);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("dotnet.native.wasm"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Wasm32"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly References"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies the Wasm General tab does not reserve a large blank area
    /// between the content-sized summary and the references section.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task General_Wasm_TallTerminal_DoesNotPadBeforeAssemblyReferences()
    {
        var wasmPath = GetWasmNativePath();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderAppWithDimensions(wasmPath, width: 160, height: 50);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        int sourceLinkLine = -1;
        int refsLine = -1;
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("dotnet.native.wasm"), TimeSpan.FromSeconds(10))
            .WaitUntil(s =>
            {
                var sourceLink = s.FindText("Source Link");
                var references = s.FindText("Assembly References");
                if (sourceLink.Count == 0 || references.Count == 0)
                    return false;

                sourceLinkLine = sourceLink[0].Line;
                refsLine = references[0].Line;
                return true;
            }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsInRange(1, 4, refsLine - sourceLinkLine);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies the PE/Metadata tab routes raw Wasm modules to WebAssembly section rows.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab1_Wasm_ShowsWebAssemblySections()
    {
        var wasmPath = GetWasmNativePath();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(wasmPath, TabId.PeMetadata);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Payload Offset"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("code") || s.ContainsText("type"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies the Disassembly tab presents raw Wasm functions through Wasm-specific groups.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab3_Wasm_ShowsWasmFunctionGroups()
    {
        var wasmPath = GetWasmNativePath();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(wasmPath, TabId.IlInspector);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("(imports)"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("(exports)") || s.ContainsText("(functions)"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab3 is labeled as IL plus native for ReadyToRun images.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab3_Label_ReadyToRun_IsIlAndNative()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, "ReadyToRun crossgen2 publish did not run on this leg.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.ReadyToRunConsoleDll!);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("IL + Native"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab4 shows strings.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab4_ShowsStrings()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D4) // Tab 4 — Strings
            .WaitUntil(s => s.ContainsText("Offset") || s.ContainsText("Value") || s.ContainsText("User Strings"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab5 shows hex dump.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab5_ShowsHexDump()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5) // Key 5 → Hex Dump (TabId 4)
            .WaitUntil(s => s.ContainsText("4D 5A") || s.ContainsText("00000"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab5 starts in normal mode read only.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab5_StartsInNormalMode_ReadOnly()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(HexEditMode.Normal, _state!.HexMode);
        Assert.IsTrue(_state.HexEditorState.IsReadOnly);
        Assert.IsFalse(_state.HexIsDirty);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab5 i key enters insert mode.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab5_IKey_EntersInsertMode()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await Task.Delay(100, cts.Token);
        Assert.AreEqual(HexEditMode.Insert, _state!.HexMode);
        Assert.IsFalse(_state.HexEditorState.IsReadOnly);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab5 esc from insert returns to normal.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab5_EscFromInsert_ReturnsToNormal()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(HexEditMode.Normal, _state!.HexMode);
        Assert.IsTrue(_state.HexEditorState.IsReadOnly);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab5 esc from insert with confirmed search exits insert first.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab5_EscFromInsert_WithConfirmedSearch_ExitsInsertFirst()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            // Start a search and confirm it
            .Key(Hex1bKey.OemQuestion) // '/' — activate search
            .WaitUntil(_ => _state!.Search[TabId.HexDump].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.M)
            .Key(Hex1bKey.Z)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Search[TabId.HexDump].IsConfirmed, TimeSpan.FromSeconds(10))
            // Enter insert mode
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
            // First Esc should exit insert mode, NOT dismiss search
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Insert mode must be exited
        Assert.AreEqual(HexEditMode.Normal, _state!.HexMode);
        Assert.IsTrue(_state.HexEditorState.IsReadOnly);
        // Search should still be active (not dismissed by this Esc)
        Assert.IsTrue(_state.Search[TabId.HexDump].IsActive);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab5 normal mode vim keys navigate.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab5_NormalMode_VimKeysNavigate()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var cursorBefore = _state!.HexEditorState.Cursor.Position;

        // Press 'l' to move right in normal mode
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.L)
            .WaitUntil(_ => _state.HexEditorState.Cursor.Position != cursorBefore, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreNotEqual(cursorBefore, _state.HexEditorState.Cursor.Position);
        // Document should NOT be modified — we're in normal mode
        Assert.IsFalse(_state.HexIsDirty);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab5 insert mode s key does not toggle size.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab5_InsertMode_SKey_DoesNotToggleSize()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var sizesBefore = _state!.HumanReadableSizes;

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.S)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(sizesBefore, _state.HumanReadableSizes);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab5 insert mode q key does not quit.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab5_InsertMode_QKey_DoesNotQuit()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Q) // Should NOT quit — we're in insert mode
            .Ctrl().Key(Hex1bKey.C) // This quits
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // If Q had quit, runTask would already be completed before Ctrl+C
        // The fact that we reach here means the app was still running
        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab5 insert mode number keys do not switch tabs.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab5_InsertMode_NumberKeys_DoNotSwitchTabs()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D1) // Should NOT switch to tab 1
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(TabId.HexDump, _state!.CurrentTab);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab5 normal mode no insert indicator.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab5_NormalMode_NoInsertIndicator()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            // Verify normal mode does not show INSERT indicator
            .WaitUntil(s => !s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(HexEditMode.Normal, _state!.HexMode);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab5 ctrl s saves with correct file name.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab5_CtrlS_SavesWithCorrectFileName()
    {
        // Work on a disposable copy so we don't modify the shared fixture assembly
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempDll = Path.Combine(tempDir, "HelloWorld.dll");
        File.Copy(Samples.HelloWorldDll, tempDll);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
            var (terminal, app) = CreateDotsiderApp(tempDll);
            var runTask = app.RunAsync(cts.Token);
            await Task.Delay(100, cts.Token);

            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
                .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
                .Key(Hex1bKey.D5)
                .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
                // Enter insert mode, skip past MZ header into DOS stub padding,
                // then type two nibbles to complete a byte edit without breaking PE
                .Key(Hex1bKey.I)
                .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
                .Key(Hex1bKey.RightArrow).Key(Hex1bKey.RightArrow)
                .Key(Hex1bKey.RightArrow).Key(Hex1bKey.RightArrow)
                .Key(Hex1bKey.F)
                .Key(Hex1bKey.F)
                .WaitUntil(_ => _state!.HexIsDirty, TimeSpan.FromSeconds(10))
                // Return to normal mode, then save
                .Key(Hex1bKey.Escape)
                .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
                .Ctrl().Key(Hex1bKey.S)
                .WaitUntil(_ => _state!.HexNotification != null, TimeSpan.FromSeconds(10))
                .Ctrl().Key(Hex1bKey.C)
                .Build()
                .ApplyAsync(terminal, cts.Token);

            // FilePath must be the original, not the .tmp fallback
            Assert.AreEqual(tempDll, _state!.Analyzer.FilePath);
            Assert.DoesNotContain(".tmp", _state.Analyzer.FileName);
            Assert.Contains("written", _state.HexNotification!);
            Assert.Contains("HelloWorld.dll", _state.HexNotification!);
            // No temp file should remain
            Assert.IsFalse(File.Exists(tempDll + ".tmp"));

            cts.Cancel();
        await runTask;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// Verifies tab6 shows dep graph.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_ShowsDepGraph()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6) // Tab 6 — Dep Graph
            .WaitUntil(s => s.ContainsText("Newtonsoft") || s.ContainsText("System.Runtime"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// A completed dependency-graph build failure replaces the transient build status with the
    /// stable, generic error and does not disclose the underlying exception through the terminal.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_GraphBuildFault_ShowsStableError()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _testContext.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);

        try
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
                .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
                .Build()
                .ApplyAsync(terminal, cts.Token);

            Assert.IsNotNull(_state);
            _state.GraphBuilder = (_, _) =>
                throw new InvalidOperationException("sensitive graph-build details");

            await new Hex1bTerminalInputSequenceBuilder()
                .Key(Hex1bKey.D6)
                .WaitUntil(_ => _state.GraphNavigationError is not null,
                    TimeSpan.FromSeconds(10))
                .WaitUntil(s => s.ContainsText("Cannot build dependency graph"),
                    TimeSpan.FromSeconds(10))
                .Build()
                .ApplyAsync(terminal, cts.Token);

            await _state.GraphBuildTask.WaitAsync(cts.Token);

            using var snapshot = terminal.CreateSnapshot();
            var screenText = snapshot.GetScreenText();
            Assert.Contains("Cannot build dependency graph", screenText);
            Assert.DoesNotContain("Building dependency graph...", screenText);
            Assert.DoesNotContain("sensitive graph-build details", screenText);
        }
        finally
        {
            cts.Cancel();
            await runTask;
        }
    }

    /// <summary>
    /// Opening the AppLocalRollForward sample on the Dep Graph tab must not render the
    /// <c>! </c> IdentityMismatch marker for <c>Microsoft.Diagnostics.NETCore.Client</c>.
    /// The sample's transitive AssemblyRef from <c>Microsoft.Diagnostics.Tracing.TraceEvent</c>
    /// targets v0.2.10.10501 while NuGet deploys v0.2.13.11903 next to it, so the AppLocal
    /// probe must roll forward (same well-known framework PKT, equal-or-higher version) and
    /// the cached graph must collapse onto a single resolved node keyed at the deployed version.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_AppLocalRollForward_NoIdentityMismatchMarker()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _testContext.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(Samples.AppLocalRollForwardDll);
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(_ => _state is { GraphBuildInProgress: true } or { CachedGraph: not null },
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsNotNull(_state);
        await _state.GraphBuildTask.WaitAsync(cts.Token);
        Assert.IsNull(_state.GraphNavigationError);
        Assert.IsNotNull(_state.CachedGraph);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Microsoft.Diagnostics.NETCore.Client"),
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var snapshot = terminal.CreateSnapshot();
        Assert.IsFalse(snapshot.ContainsText("! Microsoft.Diagnostics.NETCore.Client"),
            "AppLocal roll-forward must suppress the IdentityMismatch marker on the Dep Graph tab.");

        var graph = _state!.CachedGraph;
        Assert.IsNotNull(graph);
        var clientNodes = graph.Value.Nodes
            .Where(n => n.Name == "Microsoft.Diagnostics.NETCore.Client")
            .ToList();
        var clientNode = Assert.ContainsSingle(clientNodes);
        Assert.IsFalse(clientNode.Unresolved);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab7 shows size map.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab7_ShowsSizeMap()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D7) // Tab 7 — Size Map
            .WaitUntil(s => !s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab6 shows node and edge counts.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_ShowsNodeAndEdgeCounts()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:") && s.ContainsText("Edges:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var graph = _state!.CachedGraph;
        Assert.IsNotNull(graph);
        Assert.IsGreaterThan(0, graph.Value.Nodes.Count);
        Assert.IsGreaterThan(0, graph.Value.Edges.Count);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab6 search shows match count.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_SearchShowsMatchCount()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.OemQuestion) // '/' — activate search
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.S).Key(Hex1bKey.Y).Key(Hex1bKey.S) // "sys"
            .Key(Hex1bKey.Enter) // Confirm
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsConfirmed, TimeSpan.FromSeconds(10))
            // The match count is recomputed during the render pass, so wait for
            // the frame that carries it rather than racing the confirm handler.
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].MatchCount > 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsGreaterThan(0, _state!.Search[TabId.DepGraph].MatchCount, "Search for 'sys' should match System.* dependencies");

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab6 match navigation cycles graph selected node.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_MatchNavigation_CyclesGraphSelectedNode()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to dep graph and search for "sys"
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.S).Key(Hex1bKey.Y).Key(Hex1bKey.S)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsConfirmed, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Press 'n' to navigate to first match
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.N)
            .WaitUntil(_ => _state!.GraphMatchIndex >= 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var firstIndex = _state!.GraphMatchIndex;
        Assert.IsGreaterThanOrEqualTo(0, firstIndex);

        // Press 'n' again — should advance to next match
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.N)
            .WaitUntil(_ => _state.GraphMatchIndex != firstIndex
                            || _state.Search[TabId.DepGraph].MatchCount == 1,
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        if (_state.Search[TabId.DepGraph].MatchCount > 1)
            Assert.AreNotEqual(firstIndex, _state.GraphMatchIndex);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab6 arrow keys work after search confirm.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_ArrowKeys_WorkAfterSearchConfirm()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to dep graph, search for "sys", confirm
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.S).Key(Hex1bKey.Y).Key(Hex1bKey.S)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsConfirmed, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Arrow keys should work immediately — focus restored to Interactable
        Assert.AreEqual(-1, _state!.GraphSelectedIndex);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.GraphSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(0, _state.GraphSelectedIndex);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab7 arrow keys work after search confirm.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab7_ArrowKeys_WorkAfterSearchConfirm()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to Size Map, search for "rich", confirm
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("Total:"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.Search[TabId.SizeMap].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.R).Key(Hex1bKey.I).Key(Hex1bKey.C).Key(Hex1bKey.H)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Search[TabId.SizeMap].IsConfirmed, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Arrow keys should work immediately — focus restored to Interactable
        Assert.AreEqual(-1, _state!.TreemapSelectedIndex);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.TreemapSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(0, _state.TreemapSelectedIndex);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab6 startup focus arrow keys work without tab switch.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_StartupFocus_ArrowKeysWorkWithoutTabSwitch()
    {
        // Start directly on Dep Graph tab — tests the initial focus predicate
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll, initialTab: TabId.DepGraph);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(-1, _state!.GraphSelectedIndex);

        // Arrow keys should work immediately without switching tabs first
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.GraphSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(0, _state.GraphSelectedIndex);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab7 startup focus arrow keys work without tab switch.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab7_StartupFocus_ArrowKeysWorkWithoutTabSwitch()
    {
        // Start directly on Size Map tab — tests the initial focus predicate
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll, initialTab: TabId.SizeMap);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Total:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(-1, _state!.TreemapSelectedIndex);

        // Arrow keys should work immediately without switching tabs first
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.TreemapSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(0, _state.TreemapSelectedIndex);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab6 arrow keys cycle selected index.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_ArrowKeys_CycleSelectedIndex()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to dep graph tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(-1, _state!.GraphSelectedIndex);

        // Press Right to select first node
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.GraphSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(0, _state.GraphSelectedIndex);

        // Press Right again — should advance
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.GraphSelectedIndex == 1, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(1, _state.GraphSelectedIndex);

        // Press Left — should go back
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.LeftArrow)
            .WaitUntil(_ => _state.GraphSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(0, _state.GraphSelectedIndex);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Pressing <c>f</c> on the Dep Graph toggles framework filtering. The status line
    /// reports hidden counts, and the state flag flips. Root stays visible by contract —
    /// verified by asserting the nodes cached for the filtered view still include the root id.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_FilterToggle_HidesFrameworkAssembliesAndKeepsRootVisible()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsFalse(_state!.DepGraphHideFramework);
        var graph = _state.CachedGraph;
        Assert.IsNotNull(graph);
        var rootId = graph.Value.Nodes.First(n => n.IsRoot).Id;
        var navigation = _state.GraphNavigation;
        Assert.IsNotNull(navigation);
        Assert.Contains(
            n => navigation.TryGetValue(n.Id, out var context)
                && context.IsFrameworkAssembly,
            graph.Value.Nodes);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.F)
            .WaitUntil(_ => _state!.DepGraphHideFramework, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Hidden:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsTrue(_state.DepGraphHideFramework);
        // Root id is still in the cached graph after toggle (underlying graph not rebuilt).
        Assert.Contains(n => n.Id == rootId && n.IsRoot, graph.Value.Nodes);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Pressing <c>d</c> on the Dep Graph tab toggles scope between <c>All</c> and
    /// <c>DirectOnly</c>. The status line reflects the active scope and selection / match
    /// indices reset on each change so stale indices cannot survive into a smaller view.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_ScopeToggle_SwitchesBetweenAllAndDirectOnly()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(Views.DependencyGraphScope.All, _state!.DepGraphScope);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D)
            .WaitUntil(_ => _state!.DepGraphScope == Views.DependencyGraphScope.DirectOnly,
                TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("scope: direct"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(Views.DependencyGraphScope.DirectOnly, _state.DepGraphScope);
        Assert.AreEqual(-1, _state.GraphSelectedIndex);
        Assert.AreEqual(-1, _state.GraphMatchIndex);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D)
            .WaitUntil(_ => _state!.DepGraphScope == Views.DependencyGraphScope.All,
                TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("d: scope all"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(Views.DependencyGraphScope.All, _state.DepGraphScope);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Pressing <c>End</c> on the Dep Graph must reach the bottom of the content even when
    /// a node is currently selected. Previously a per-frame selection-follow rule pulled the
    /// scroll back to the selected node's Y before the clamp, so <c>End</c> never actually
    /// showed the last row. Regression test: select a node near the top, press <c>End</c>,
    /// and assert the scroll offset matches the full <c>ContentHeight - viewport</c> range.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_End_ScrollsToBottom_EvenWhenNodeSelected()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.GraphSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsNotNull(_state!.CachedGraphRenderLayout);
        Assert.IsNotNull(_state.CachedGraphRenderLayoutKey);
        var layout = _state.CachedGraphRenderLayout!;
        var key = _state.CachedGraphRenderLayoutKey!.Value;
        var expectedMax = Math.Max(0, layout.ContentHeight - key.Height);
        Assert.IsGreaterThan(0, expectedMax, "viewport must be smaller than content for this test");

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.End)
            .WaitUntil(_ => _state!.DepGraphScrollY == expectedMax, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(expectedMax, _state.DepGraphScrollY);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Pure helper test: with no cached layout the inputs are <c>(0, 1, 0)</c> so the
    /// scrollbar widget can be constructed on the very first frame without a null check.
    /// <see cref="Hex1b.Nodes.ScrollbarNode.IsScrollable"/> returns <see langword="false"/>
    /// for this shape and the bar renders nothing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ComputeScrollbarInputs_BeforeLayoutReady_ReturnsSafeDefaults()
    {
        var (_, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        Assert.IsNull(state.CachedGraphRenderLayout);
        Assert.IsNull(state.CachedGraphRenderLayoutKey);

        var inputs = DependencyGraphView.ComputeScrollbarInputs(state);

        Assert.AreEqual((0, 1, 0), inputs);
    }

    /// <summary>
    /// Pure helper test: <c>End</c> queues <c>int.MaxValue</c> into <c>DepGraphScrollY</c>;
    /// once a layout is cached the helper clamps that to <c>ContentHeight - Height</c> so the
    /// scrollbar receives a valid offset rather than an out-of-range stub.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ComputeScrollbarInputs_ClampsScrollYToMax_AfterEnd()
    {
        var (_, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        using var state = new DotsiderState(app, Samples.RichLibraryDll);

        const int contentHeight = 200;
        const int viewportHeight = 25;
        state.CachedGraphRenderLayout = new GraphRenderLayout(
            [], [], new Dictionary<string, int>(StringComparer.Ordinal), contentHeight);
        state.CachedGraphRenderLayoutKey = new GraphRenderLayoutKey(
            NodesRef: null,
            Scope: DependencyGraphScope.All,
            HideFramework: false,
            Width: 100,
            Height: viewportHeight);
        state.DepGraphScrollY = int.MaxValue;

        var (c, v, o) = DependencyGraphView.ComputeScrollbarInputs(state);

        Assert.AreEqual(contentHeight, c);
        Assert.AreEqual(viewportHeight, v);
        Assert.AreEqual(contentHeight - viewportHeight, o);
    }

    /// <summary>
    /// The scrollbar replaces the textual <c>Scroll: N/M</c> suffix the status line used to
    /// carry. Asserts the suffix is gone so reviewers don't have to grep for it.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_StatusLine_DoesNotContainScrollSuffix()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var snapshot = terminal.CreateSnapshot();
        Assert.IsFalse(snapshot.ContainsText("Scroll:"),
            "Status line should not carry a textual Scroll: suffix anymore.");

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// When the laid-out graph is taller than the viewport, the scrollbar's thumb glyph
    /// (<c>▉</c>) renders at least once in the rightmost column inside the graph viewport.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_Scrollbar_RendersAtRightEdge_WhenContentExceedsViewport()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .WaitUntil(_ => _state!.CachedGraphRenderLayout is not null
                && _state.CachedGraphRenderLayoutKey is { } k
                && _state.CachedGraphRenderLayout.ContentHeight > k.Height,
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Wait for the scrollbar widget to actually paint a thumb cell in the rightmost
        // column. Polled because the snapshot-and-invalidate path may need one extra frame
        // after the first layout build.
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                for (int y = 0; y < s.Height; y++)
                {
                    if (s.GetCell(s.Width - 1, y).Character == "▉") return true;
                }
                return false;
            }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var snapshot = terminal.CreateSnapshot();
        var rightCol = snapshot.Width - 1;
        var hasThumb = false;
        for (int y = 0; y < snapshot.Height; y++)
        {
            if (snapshot.GetCell(rightCol, y).Character == "▉")
            {
                hasThumb = true;
                break;
            }
        }
        Assert.IsTrue(hasThumb, "Expected at least one scrollbar thumb cell in the rightmost column.");

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// When the laid-out graph fits inside the viewport, the scrollbar's gutter is reserved
    /// (FixedWidth(1)) but renders no thumb glyphs — <see cref="Hex1b.Nodes.ScrollbarNode"/>
    /// paints nothing when <c>ContentSize &lt;= ViewportSize</c>.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_Scrollbar_HiddenWhenContentFits()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .WaitUntil(_ => _state!.CachedGraphRenderLayout is not null
                && _state.CachedGraphRenderLayoutKey is { } k
                && _state.CachedGraphRenderLayout.ContentHeight <= k.Height,
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var snapshot = terminal.CreateSnapshot();
        var rightCol = snapshot.Width - 1;
        for (int y = 0; y < snapshot.Height; y++)
        {
            var ch = snapshot.GetCell(rightCol, y).Character;
            Assert.AreNotEqual("▉", ch);
        }

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Mouse wheel down over the graph surface advances <see cref="DotsiderState.DepGraphScrollY"/>
    /// via the new <c>MouseButton.ScrollDown</c> binding.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_MouseWheelDown_AdvancesScrollY()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderAppWithMouse(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(0, _state!.DepGraphScrollY);

        await new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(40, 10)
            .ScrollDown()
            .WaitUntil(_ => _state.DepGraphScrollY > 0, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsGreaterThan(0, _state.DepGraphScrollY);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Wheel up at the top is clamped to zero by <c>SetScroll</c>'s lower bound.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_MouseWheelUp_AtTop_StaysAtZero()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderAppWithMouse(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .MouseMoveTo(40, 10)
            .ScrollUp()
            .ScrollUp()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(0, _state!.DepGraphScrollY);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Wheel down at the bottom is clamped to <c>max</c> by <c>SetScroll</c>'s upper bound.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_MouseWheelDown_AtBottom_DoesNotExceedMax()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderAppWithMouse(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.End)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsNotNull(_state!.CachedGraphRenderLayout);
        Assert.IsNotNull(_state.CachedGraphRenderLayoutKey);
        var max = Math.Max(0,
            _state.CachedGraphRenderLayout!.ContentHeight - _state.CachedGraphRenderLayoutKey!.Value.Height);
        Assert.IsGreaterThan(0, max, "viewport must be smaller than content for this test");

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => _state.DepGraphScrollY == max, TimeSpan.FromSeconds(5))
            .MouseMoveTo(40, 10)
            .ScrollDown()
            .ScrollDown()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(max, _state.DepGraphScrollY);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Track click below the thumb pages forward by <c>viewportSize - 1</c>, hex1b's
    /// <see cref="Hex1b.Nodes.ScrollbarNode"/> step. Assert exact equality on the page size,
    /// which is determined by hex1b alone.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_TrackClick_PagesScrollY_ByViewportMinusOne()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderAppWithMouse(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsNotNull(_state!.CachedGraphRenderLayout);
        Assert.IsNotNull(_state.CachedGraphRenderLayoutKey);
        var viewport = _state.CachedGraphRenderLayoutKey!.Value.Height;
        var max = Math.Max(0, _state.CachedGraphRenderLayout!.ContentHeight - viewport);
        Assert.IsGreaterThanOrEqualTo(viewport - 1, max, "viewport must allow at least one page step");
        Assert.AreEqual(0, _state.DepGraphScrollY);

        // Derive the click target from the live scrollbar bounds rather than a fixed cell: the
        // graph layout — and therefore the track's position and length — varies with content.
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var (sbCol, trackTop, trackHeight) = await WaitForScrollbarTrackAsync(auto);

        // With the offset at 0 the thumb is anchored at the track top, and because the content
        // exceeds the viewport the thumb is shorter than the track — so the bottom track row is
        // always below the thumb. A click there pages forward by exactly viewportSize - 1.
        await auto.WaitUntilAsync(s => s.GetCell(sbCol, trackTop).Character == "▉",
            description: "scrollbar thumb to paint at the track top");
        var trackBottom = trackTop + trackHeight - 1;

        await new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(sbCol, trackBottom)
            .Click()
            .WaitUntil(_ => _state.DepGraphScrollY > 0, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(viewport - 1, _state.DepGraphScrollY);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Thumb drag mutates <see cref="DotsiderState.DepGraphScrollY"/>. Specifically guards
    /// the composition fix: if the scrollbar were nested inside the Interactable, drag events
    /// would never reach <see cref="Hex1b.Nodes.ScrollbarNode"/> and this test would fail.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_ThumbDrag_UpdatesScrollY()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderAppWithMouse(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(0, _state!.DepGraphScrollY);

        // Grab the thumb at its real position (the track top at offset 0) and drag down within the
        // track, rather than assuming fixed rows: any downward movement maps to a positive offset.
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var (sbCol, trackTop, trackHeight) = await WaitForScrollbarTrackAsync(auto);
        await auto.WaitUntilAsync(s => s.GetCell(sbCol, trackTop).Character == "▉",
            description: "scrollbar thumb to paint at the track top");

        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(sbCol, trackTop, sbCol, trackTop + trackHeight - 1)
            .WaitUntil(_ => _state.DepGraphScrollY > 0, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsGreaterThan(0, _state.DepGraphScrollY);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Regression for the focus-return contract: after a thumb drag, the next
    /// <c>RightArrow</c> keystroke advances <see cref="DotsiderState.GraphSelectedIndex"/>.
    /// Exercises the production race by enabling input coalescing (matching the hex1b
    /// default production uses) and sending the RightArrow with no inter-step wait — the
    /// drag's mouse-down/move/up and the RightArrow all coalesce into the same processing
    /// batch. If focus restoration were deferred (e.g. via <c>RequestFocus</c>) the
    /// RightArrow would route to the still-focused <c>ScrollbarNode</c> before the next
    /// render runs, and selection would not advance. Synchronous bounce inside
    /// <c>VScrollbar.OnScroll</c> via <c>FocusWhere</c> is what makes this test pass.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_AfterScrollbarDrag_RightArrow_AdvancesSelection()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderAppWithMouse(Samples.RichLibraryDll, enableInputCoalescing: true);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsNotNull(_state!.CachedGraphRenderLayout);
        var beforeIndex = _state.GraphSelectedIndex;

        var sbCol = 119;
        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(sbCol, 8, sbCol, 14)
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.GraphSelectedIndex != beforeIndex, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreNotEqual(beforeIndex, _state.GraphSelectedIndex);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// No-op click case: the user clicks the thumb at its current position so
    /// <c>ScrollbarWidget.OnScroll</c> never fires (the offset doesn't change). The OnScroll
    /// path's synchronous bounce can't help — only the build-time scrollbar focus bounce at
    /// the top of the dep-graph view's widget builder can. This test pins down the scenario
    /// concretely: locate a painted thumb cell from the rendered right-edge gutter, confirm
    /// the live <see cref="Hex1b.Nodes.ScrollbarNode"/>'s bounds contain that cell (so the
    /// click hit-tests to the scrollbar and focuses it), click without moving, then press
    /// <c>RightArrow</c>. If selection advances and scroll position is unchanged, the
    /// build-time bounce restored focus to the graph between the no-op click and the
    /// keypress.
    /// <para>
    /// Coalescing is OFF for this test on purpose: the build-time bounce runs at the start
    /// of the next render frame, so it needs at least one frame to elapse between the click
    /// and the RightArrow. Without coalescing, every input event triggers a render, which
    /// gives the bounce a frame. Closing the residual same-batch race (no-op click + key
    /// arriving inside one coalesced batch under 5ms) requires a hex1b API surface change —
    /// either an OnFocusChanged hook on <c>ScrollbarNode</c> or public access to the current
    /// hovered node — neither of which is available today. Real-user reaction times leave
    /// dozens of frames between the click and the next key, so this test exercises the
    /// production path users actually hit.
    /// </para>
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_NoOpScrollbarClick_RightArrow_AdvancesSelection()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderAppWithMouse(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .WaitUntil(_ => _state!.CachedGraphRenderLayout is not null
                && _state.CachedGraphRenderLayoutKey is { } k
                && _state.CachedGraphRenderLayout.ContentHeight > k.Height,
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Wait for an actually-painted scrollbar cell at the rightmost column. Glyphs are
        // either the thumb (▉) or the track (│). Need a thumb cell specifically because a
        // click on the thumb starts a drag-grab without changing the offset (no OnScroll); a
        // track click pages forward by viewport-1 (would fire OnScroll, defeating the test).
        var sbCol = terminal.CreateSnapshot().Width - 1;
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                for (int y = 0; y < s.Height; y++)
                {
                    if (s.GetCell(sbCol, y).Character == "▉") return true;
                }
                return false;
            }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Locate the topmost thumb cell so the click hits the thumb (no-op when at offset 0).
        var snapshot = terminal.CreateSnapshot();
        int sbRow = -1;
        for (int y = 0; y < snapshot.Height; y++)
        {
            if (snapshot.GetCell(sbCol, y).Character == "▉")
            {
                sbRow = y;
                break;
            }
        }
        Assert.IsGreaterThanOrEqualTo(0, sbRow, "could not find a scrollbar thumb cell at the rightmost column");

        // Verify that the live ScrollbarNode's bounds actually contain the click target —
        // so the input router's hit-test will route the click to the scrollbar (and not, say,
        // a status-row text cell that happens to draw a │). This is the key check the review
        // called out: without it, the test could pass for the wrong reason.
        var scrollbar = _hex1bApp!.Focusables.OfType<Hex1b.Nodes.ScrollbarNode>().FirstOrDefault();
        Assert.IsNotNull(scrollbar);
        var bounds = scrollbar!.Bounds;
        Assert.IsTrue(sbCol >= bounds.X && sbCol < bounds.X + bounds.Width &&
            sbRow >= bounds.Y && sbRow < bounds.Y + bounds.Height, $"ScrollbarNode bounds {bounds} do not contain target cell ({sbCol}, {sbRow}).");

        var beforeIndex = _state!.GraphSelectedIndex;
        var beforeScrollY = _state.DepGraphScrollY;
        Assert.AreEqual(0, beforeScrollY);

        // Click the thumb at its current position. Mouse-down → Focus(scrollbar) →
        // ScrollbarNode.HandleDrag returns a thumb-grab handler. Mouse-up at the same spot
        // → drag ends with no movement → no offset change → OnScroll never fires. Without
        // the build-time bounce, focus would stay on the scrollbar and the RightArrow that
        // arrives next in the coalesced batch would not advance graph selection.
        await new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(sbCol, sbRow)
            .Click()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.GraphSelectedIndex != beforeIndex, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Scroll position is unchanged → confirms the click was a true no-op (OnScroll never
        // fired). Selection advanced → confirms the build-time bounce restored focus to the
        // graph before the RightArrow routed.
        Assert.AreEqual(beforeScrollY, _state.DepGraphScrollY);
        Assert.AreNotEqual(beforeIndex, _state.GraphSelectedIndex);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Pins the assumption behind the <c>OnScroll → FocusWhere(n => n is InteractableNode)</c>
    /// predicate: the dep-graph view contains exactly one <see cref="Hex1b.Nodes.InteractableNode"/>
    /// in the focus ring (the search bar uses TextBox, not Interactable). If a future change
    /// introduces a second Interactable to this view, this test fails and the predicate must
    /// be tightened before merging.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_DepGraphView_ContainsExactlyOneInteractable()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var interactableCount = app.Focusables.Count(n => n is Hex1b.Nodes.InteractableNode);
        Assert.AreEqual(1, interactableCount);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Toggling the framework filter forces a layout rebuild with a different
    /// <c>ContentHeight</c>. The snapshot-and-invalidate path in
    /// <c>GetOrBuildRenderLayout</c> must surface the new geometry to the scrollbar within
    /// two frames; otherwise a stale thumb size lingers indefinitely.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_LayoutRebuild_ScrollbarReflectsNewContentHeight_WithinTwoFrames()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsNotNull(_state!.CachedGraphRenderLayout);
        var beforeHeight = _state.CachedGraphRenderLayout!.ContentHeight;

        // Toggle framework filter → layout key changes → next render rebuilds.
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.F)
            .WaitUntil(_ => _state.CachedGraphRenderLayout is not null
                && _state.CachedGraphRenderLayout.ContentHeight != beforeHeight,
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var afterHeight = _state.CachedGraphRenderLayout!.ContentHeight;
        Assert.AreNotEqual(beforeHeight, afterHeight);

        // The snapshot-and-invalidate path schedules one extra frame so the next builder
        // sees the new layout. ComputeScrollbarInputs is what the widget builder uses,
        // so reading it here confirms the scrollbar input matches the new geometry.
        var (c, _, _) = DependencyGraphView.ComputeScrollbarInputs(_state);
        Assert.AreEqual(afterHeight, c);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Pre-layout <c>End</c> is a no-op (<c>SetScroll</c> drops scroll-down requests when no
    /// clamp data is available). Post-layout <c>End</c> still scrolls to the bottom. Documents
    /// the deliberate behavior change called out in the plan.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab6_End_PreLayout_IsNoOp_PostLayout_ScrollsToBottom()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll, initialTab: (int)TabId.DepGraph);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        // Press End immediately, before "Nodes:" appears (i.e. before a layout was built).
        // SetScroll's pre-layout branch keeps DepGraphScrollY at 0.
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.End)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(0, _state!.DepGraphScrollY);

        // Now wait for layout, press End again, and confirm it scrolls to the bottom.
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsNotNull(_state.CachedGraphRenderLayout);
        Assert.IsNotNull(_state.CachedGraphRenderLayoutKey);
        var max = Math.Max(0,
            _state.CachedGraphRenderLayout!.ContentHeight - _state.CachedGraphRenderLayoutKey!.Value.Height);
        Assert.IsGreaterThan(0, max, "viewport must be smaller than content for this test");

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.End)
            .WaitUntil(_ => _state.DepGraphScrollY == max, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(max, _state.DepGraphScrollY);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab7 arrow keys cycle selected index.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab7_ArrowKeys_CycleSelectedIndex()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to Size Map tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("Total:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(-1, _state!.TreemapSelectedIndex);

        // Press Right to select first item
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.TreemapSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(0, _state.TreemapSelectedIndex);

        // Press Right again — should advance
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.TreemapSelectedIndex == 1, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(1, _state.TreemapSelectedIndex);

        // Press Left — should go back
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.LeftArrow)
            .WaitUntil(_ => _state.TreemapSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(0, _state.TreemapSelectedIndex);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab7 shows breadcrumb and total size.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab7_ShowsBreadcrumbAndTotalSize()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("RichLibrary") && s.ContainsText("Total:"),
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsNotNull(_state!.CachedSizeTree);
        Assert.IsGreaterThan(0, _state.CachedSizeTree.Size);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab7 escape pops breadcrumb.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab7_Escape_PopsBreadcrumb()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to tab 7, let treemap render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("RichLibrary") && s.ContainsText("Total:"),
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Programmatically drill down into first child namespace
        var root = _state!.CachedSizeTree!;
        var firstChild = root.Children[0];
        _state.TreemapBreadcrumb.Push(root);
        _state.TreemapCurrentLevel = firstChild;
        _hex1bApp!.Invalidate();

        // Wait for breadcrumb to show the drill-down path (root > child)
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText(firstChild.Name), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.ContainsSingle(_state.TreemapBreadcrumb);

        // Press Escape to go up
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state.TreemapBreadcrumb.Count == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsEmpty(_state.TreemapBreadcrumb);
        Assert.AreEqual(root, _state.TreemapCurrentLevel);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab7 search match navigation updates hovered item.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab7_SearchMatchNavigation_UpdatesHoveredItem()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to tab 7 and search for a namespace
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("Total:"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.OemQuestion) // '/' — activate search
            .WaitUntil(_ => _state!.Search[TabId.SizeMap].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.R).Key(Hex1bKey.I).Key(Hex1bKey.C).Key(Hex1bKey.H) // "rich"
            .Key(Hex1bKey.Enter) // Confirm
            .WaitUntil(_ => _state!.Search[TabId.SizeMap].IsConfirmed, TimeSpan.FromSeconds(10))
            // The match count is recomputed during the render pass, so wait for
            // the frame that carries it rather than racing the confirm handler.
            .WaitUntil(_ => _state!.Search[TabId.SizeMap].MatchCount > 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsGreaterThan(0, _state!.Search[TabId.SizeMap].MatchCount, "Search for 'rich' should match RichLibrary namespace");

        // Press 'n' to navigate to first match
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.N)
            .WaitUntil(_ => _state.TreemapMatchIndex >= 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsGreaterThanOrEqualTo(0, _state.TreemapMatchIndex);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab7 enter prefers search match over stale selection.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab7_Enter_PrefersSearchMatchOverStaleSelection()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        // Navigate to Size Map, select first item with arrow key
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("Total:"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.TreemapSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await Task.Delay(100, cts.Token);
        var currentLevel = _state!.TreemapCurrentLevel ?? _state.CachedSizeTree!;
        Assert.AreEqual(0, _state.TreemapSelectedIndex);

        // Find a drillable child at index != 0 whose name differs from child 0
        var child0Name = currentLevel.Children[0].Name;
        string? searchTerm = null;
        for (var i = 1; i < currentLevel.Children.Count; i++)
        {
            var child = currentLevel.Children[i];
            if (child.Children.Count == 0) continue;
            // Use enough of the name to get a unique-ish match, but not child 0
            var candidate = child.Name.Length > 3 ? child.Name[..4] : child.Name;
            if (!child0Name.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            {
                searchTerm = candidate.ToLowerInvariant();
                break;
            }
        }

        if (searchTerm is null)
        {
            cts.Cancel();
            await runTask;
            return;
        }

        // Search for the non-zero child, confirm, navigate to match
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state.Search[TabId.SizeMap].IsActive, TimeSpan.FromSeconds(10))
            .Type(searchTerm)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state.Search[TabId.SizeMap].IsConfirmed, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        if (_state.Search[TabId.SizeMap].MatchCount == 0)
        {
            cts.Cancel();
            await runTask;
            return;
        }

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.N)
            .WaitUntil(_ => _state.TreemapMatchIndex >= 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await Task.Delay(100, cts.Token);

        // Stale selection is still 0, but search match points elsewhere
        Assert.AreEqual(0, _state.TreemapSelectedIndex);
        Assert.IsGreaterThanOrEqualTo(0, _state.TreemapMatchIndex);

        // Press Enter — should drill into search match, not stale selection at index 0
        var previousLevel = _state.TreemapCurrentLevel;
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state.TreemapCurrentLevel != previousLevel
                            || _state.TreemapBreadcrumb.Count > 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await Task.Delay(100, cts.Token);

        // Verify we drilled into the search match (name contains query), not child 0
        if (_state.TreemapCurrentLevel != previousLevel)
        {
            Assert.Contains(searchTerm, _state.TreemapCurrentLevel!.Name,
                StringComparison.OrdinalIgnoreCase);
        }

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab8 library shows no entry point.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab8_Library_ShowsNoEntryPoint()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8) // Tab 8 — Dynamic
            .WaitUntil(s => s.ContainsText("entry point") || s.ContainsText("library"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab8 exe shows launch prompt.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab8_Exe_ShowsLaunchPrompt()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8) // Tab 8 — Dynamic
            .WaitUntil(s => s.ContainsText("Enter") || s.ContainsText("Launch") || s.ContainsText("EventPipe"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab8 exe idle view shows assembly info and providers.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab8_Exe_IdleView_ShowsAssemblyInfoAndProviders()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Verify idle view shows assembly info and provider list
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly:"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Entry Point:"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Providers:"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("CLR Runtime"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsNull(_state!.Tracer);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab8 sub tab navigation arrow keys cycle.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab8_SubTabNavigation_ArrowKeysCycle()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        // Launch process and wait for exit so sub-tabs are visible
        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Assembly Name");
        await auto.KeyAsync(Hex1bKey.D8, cts.Token);
        await auto.WaitUntilAsync(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"));
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state!.Tracer?.ProcessState
            is TraceProcessState.Exited or TraceProcessState.Error,
            timeout: TimeSpan.FromSeconds(30));

        // Starts on Events sub-tab
        Assert.AreEqual(DynamicSubTabId.Events, _state!.DynamicSubTab);

        // Right → Counters
        await auto.RightAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state!.DynamicSubTab == DynamicSubTabId.Counters);
        Assert.AreEqual(DynamicSubTabId.Counters, _state.DynamicSubTab);

        // Right → Output
        await auto.RightAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state.DynamicSubTab == DynamicSubTabId.Output);
        Assert.AreEqual(DynamicSubTabId.Output, _state.DynamicSubTab);

        // Right → Summary
        await auto.RightAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state.DynamicSubTab == DynamicSubTabId.Summary);
        Assert.AreEqual(DynamicSubTabId.Summary, _state.DynamicSubTab);

        // Right at max → stays on Summary (no wrap)
        await auto.RightAsync(cts.Token);
        Assert.AreEqual(DynamicSubTabId.Summary, _state.DynamicSubTab);

        // Left → back to Output
        await auto.LeftAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state.DynamicSubTab == DynamicSubTabId.Output);
        Assert.AreEqual(DynamicSubTabId.Output, _state.DynamicSubTab);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab8 category filter keys update state.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab8_CategoryFilterKeys_UpdateState()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        // Launch, wait for exit, stay on Events sub-tab
        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Assembly Name");
        await auto.KeyAsync(Hex1bKey.D8, cts.Token);
        await auto.WaitUntilAsync(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"));
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state!.Tracer?.ProcessState
            is TraceProcessState.Exited or TraceProcessState.Error,
            timeout: TimeSpan.FromSeconds(30));

        Assert.IsNull(_state!.DynamicCategoryFilter);

        // g → GC filter
        await auto.KeyAsync(Hex1bKey.G, cts.Token);
        await auto.WaitUntilAsync(_ => _state!.DynamicCategoryFilter == TraceEventCategory.GC);
        Assert.AreEqual(TraceEventCategory.GC, _state.DynamicCategoryFilter);

        // j → JIT filter
        await auto.KeyAsync(Hex1bKey.J, cts.Token);
        await auto.WaitUntilAsync(_ => _state.DynamicCategoryFilter == TraceEventCategory.JIT);
        Assert.AreEqual(TraceEventCategory.JIT, _state.DynamicCategoryFilter);

        // e → Exception filter
        await auto.KeyAsync(Hex1bKey.E, cts.Token);
        await auto.WaitUntilAsync(_ => _state.DynamicCategoryFilter == TraceEventCategory.Exception);
        Assert.AreEqual(TraceEventCategory.Exception, _state.DynamicCategoryFilter);

        // l → Loader filter
        await auto.KeyAsync(Hex1bKey.L, cts.Token);
        await auto.WaitUntilAsync(_ => _state.DynamicCategoryFilter == TraceEventCategory.Loader);
        Assert.AreEqual(TraceEventCategory.Loader, _state.DynamicCategoryFilter);

        // t → Threading filter
        await auto.KeyAsync(Hex1bKey.T, cts.Token);
        await auto.WaitUntilAsync(_ => _state.DynamicCategoryFilter == TraceEventCategory.Threading);
        Assert.AreEqual(TraceEventCategory.Threading, _state.DynamicCategoryFilter);

        // h → HTTP filter
        await auto.KeyAsync(Hex1bKey.H, cts.Token);
        await auto.WaitUntilAsync(_ => _state.DynamicCategoryFilter == TraceEventCategory.Http);
        Assert.AreEqual(TraceEventCategory.Http, _state.DynamicCategoryFilter);

        // Esc → clears filter
        await auto.EscapeAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state.DynamicCategoryFilter is null);
        Assert.IsNull(_state.DynamicCategoryFilter);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab8 ctrl k stops running process.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab8_CtrlK_StopsRunningProcess()
    {
        // MinimalApi is a web server that stays alive until killed,
        // so Ctrl+K is the only way to reach Exited within the timeout.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.MinimalApiDll);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        // Navigate to Dynamic tab and launch
        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Assembly Name");
        await auto.KeyAsync(Hex1bKey.D8, cts.Token);
        await auto.WaitUntilAsync(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"));
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state!.Tracer?.ProcessState == TraceProcessState.Running);

        Assert.IsNotNull(_state!.Tracer);
        Assert.AreEqual(TraceProcessState.Running, _state.Tracer!.ProcessState);

        // Ctrl+K to stop — the web server would run indefinitely without this
        await auto.Ctrl().KeyAsync(Hex1bKey.K, cts.Token);
        await auto.WaitUntilAsync(_ => _state.Tracer!.ProcessState
            is TraceProcessState.Exited or TraceProcessState.Error);

        Assert.IsTrue(_state.Tracer!.ProcessState
            is TraceProcessState.Exited or TraceProcessState.Error);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab8 enter reruns after exit.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab8_Enter_RerunsAfterExit()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        // Launch and wait for process to finish (Exited or Error)
        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Assembly Name");
        await auto.KeyAsync(Hex1bKey.D8, cts.Token);
        await auto.WaitUntilAsync(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"));
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state!.Tracer?.ProcessState
            is TraceProcessState.Exited or TraceProcessState.Error,
            timeout: TimeSpan.FromSeconds(30));

        var firstTracer = _state!.Tracer;
        Assert.IsNotNull(firstTracer);

        // Press Enter to re-run
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state.Tracer != firstTracer);

        // A new tracer was created
        Assert.IsNotNull(_state.Tracer);
        Assert.AreNotEqual(firstTracer, _state.Tracer);

        // Wait for the re-run to exit successfully
        await auto.WaitUntilAsync(_ => _state.Tracer!.ProcessState
            is TraceProcessState.Exited or TraceProcessState.Error,
            timeout: TimeSpan.FromSeconds(15));

        Assert.AreEqual(TraceProcessState.Exited, _state.Tracer!.ProcessState);
        await auto.WaitUntilAsync(_ => _state.Tracer!.ExitCode is not null,
            description: "exit code to be captured");
        Assert.AreEqual(0, _state.Tracer.ExitCode);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab8 search after process exit no global binding conflict.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab8_SearchAfterProcessExit_NoGlobalBindingConflict()
    {
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        // Navigate to Dynamic tab and launch the process
        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Assembly Name");
        await auto.KeyAsync(Hex1bKey.D8, cts.Token);
        await auto.WaitUntilAsync(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"));
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state!.Tracer?.ProcessState
            is TraceProcessState.Exited or TraceProcessState.Error,
            timeout: TimeSpan.FromSeconds(30));

        // Process has exited — activating search must not crash with
        // "Global binding conflict: Enter is already registered"
        await auto.KeyAsync(Hex1bKey.OemQuestion, cts.Token); // '/' — activate search
        await auto.WaitUntilAsync(_ => _state!.Search[TabId.Dynamic].IsActive);

        Assert.IsTrue(_state!.Search[TabId.Dynamic].IsActive);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies general enter on reference drills into assembly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task General_EnterOnReference_DrillsIntoAssembly()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            // Focus starts on the dependency table; DownArrow ensures a row is selected, Enter drills
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Enter)
            // After drill-down, the title bar should no longer show "HelloWorld.dll"
            .WaitUntil(s => !s.ContainsText("HelloWorld.dll"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab3 arrow keys work immediately.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab3_ArrowKeysWorkImmediately()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3) // Tab 3 — IL Inspector
            .WaitUntil(s => s.ContainsText("▶") || s.ContainsText("▼"), TimeSpan.FromSeconds(10))
            // Arrow keys should work immediately without clicking first —
            // DownArrow moves table focus, which toggles expansion on namespace/type rows
            .Key(Hex1bKey.DownArrow)
            .WaitUntil(s => s.ContainsText(".ctor") || s.ContainsText("Main"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab3 disassembly pane scrolls.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab3_DisassemblyPaneScrolls()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        // Navigate to IL tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("▶") || s.ContainsText("▼"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Select ToTitleCase programmatically (139 bytes of IL, overflows viewport)
        var toTitleCase = _state!.Analyzer.MethodDefs.First(m => m.Name == "ToTitleCase");
        var typeDef = _state.Analyzer.TypeDefs.First(t => t.FullName == toTitleCase.DeclaringType);
        var ns = !string.IsNullOrEmpty(typeDef.Namespace) ? typeDef.Namespace : "(global)";
        _state.IlTreeExpansionState[$"ns:{ns}"] = true;
        _state.IlTreeExpansionState[$"type:{toTitleCase.DeclaringType}"] = true;
        _state.IlSelectedMethod = toTitleCase;
        _state.IlFocusedTreeKey = $"method:{toTitleCase.Token}";
        _state.App.Invalidate();

        // Click in the editor to focus it, then PageDown scrolls natively
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_0000"), TimeSpan.FromSeconds(10))
            .ClickAt(50, 15) // Click in editor pane (right of splitter)
            .PageDown()
            .PageDown()
            .PageDown()
            .WaitUntil(s => !s.ContainsText("IL_0000"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab4 arrow keys cycle sub tabs.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab4_ArrowKeysCycleSubTabs()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D4) // Tab 4 — Strings
            .WaitUntil(s => s.ContainsText("User Strings"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Verify starting state
        Assert.AreEqual(0, _state!.StringsSourceTab);

        // Right arrow → sub-tab 1
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.StringsSourceTab == 1, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(1, _state.StringsSourceTab);

        // Right arrow → sub-tab 2
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.StringsSourceTab == 2, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(2, _state.StringsSourceTab);

        // Left arrow → back to sub-tab 1
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.LeftArrow)
            .WaitUntil(_ => _state.StringsSourceTab == 1, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(1, _state.StringsSourceTab);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab4 arrow keys during search editing do not switch sub tab.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab4_ArrowKeysDuringSearchEditing_DoNotSwitchSubTab()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D4) // Tab 4 — Strings
            .WaitUntil(s => s.ContainsText("User Strings"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.OemQuestion) // '/' — activate search
            .WaitUntil(_ => _state!.Search[TabId.Strings].IsActive, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(0, _state!.StringsSourceTab);

        // Arrow keys during search editing should NOT switch sub-tabs
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .Key(Hex1bKey.RightArrow)
            .Key(Hex1bKey.LeftArrow)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(0, _state.StringsSourceTab);
        Assert.IsTrue(_state.Search[TabId.Strings].IsActive);

        // Dismiss search, then arrows should work again
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => !_state.Search[TabId.Strings].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.StringsSourceTab == 1, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(1, _state.StringsSourceTab);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab8 events s key filters socket not toggle size.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab8_Events_SKey_FiltersSocket_NotToggleSize()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        // Navigate to Dynamic tab, launch the process, wait for exit
        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Assembly Name");
        await auto.KeyAsync(Hex1bKey.D8, cts.Token);
        await auto.WaitUntilAsync(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"));
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state!.Tracer?.ProcessState
            is TraceProcessState.Exited or TraceProcessState.Error,
            timeout: TimeSpan.FromSeconds(30));

        // Record initial size toggle state
        var sizesBefore = _state!.HumanReadableSizes;

        // Press S on the Events sub-tab — should set Socket filter, not toggle sizes
        await auto.KeyAsync(Hex1bKey.S, cts.Token);
        await auto.WaitUntilAsync(_ => _state.DynamicCategoryFilter == TraceEventCategory.Socket);

        Assert.AreEqual(TraceEventCategory.Socket, _state.DynamicCategoryFilter);
        Assert.AreEqual(sizesBefore, _state.HumanReadableSizes);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab3 scroll position preserved across tab switch.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab3_ScrollPositionPreservedAcrossTabSwitch()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        // Navigate to IL tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("▶") || s.ContainsText("▼"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Select ToTitleCase programmatically (139 bytes of IL, overflows viewport)
        var toTitleCase = _state!.Analyzer.MethodDefs.First(m => m.Name == "ToTitleCase");
        var typeDef = _state.Analyzer.TypeDefs.First(t => t.FullName == toTitleCase.DeclaringType);
        var ns = !string.IsNullOrEmpty(typeDef.Namespace) ? typeDef.Namespace : "(global)";
        _state.IlTreeExpansionState[$"ns:{ns}"] = true;
        _state.IlTreeExpansionState[$"type:{toTitleCase.DeclaringType}"] = true;
        _state.IlSelectedMethod = toTitleCase;
        _state.IlFocusedTreeKey = $"method:{toTitleCase.Token}";
        _state.App.Invalidate();

        // Click in editor to focus it, then scroll down natively via PageDown
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_0000"), TimeSpan.FromSeconds(10))
            .ClickAt(50, 15) // Click in editor pane
            .PageDown()
            .PageDown()
            .WaitUntil(s => !s.ContainsText("IL_0000"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var savedMethod = _state!.IlSelectedMethod;

        // Switch to tab 1 (General)
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D1)
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(savedMethod, _state.IlSelectedMethod);

        // Switch back to tab 3 — EditorNode preserved by Responsive, scroll intact
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("IL_"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => !s.ContainsText("IL_0000"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.AreEqual(savedMethod, _state.IlSelectedMethod);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies quit key exits app.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task QuitKey_ExitsApp()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Q) // q = quit
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // App should exit after q key
        var completed = await Task.WhenAny(runTask, Task.Delay(5000, cts.Token));
        Assert.AreEqual(runTask, completed);
    }

    /// <summary>
    /// Verifies cross view back suppressed during search editing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task CrossViewBack_SuppressedDuringSearchEditing()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        // Navigate to PE/Metadata MethodDef, then use the production Go-to-IL
        // binding so the cross-view back target is created on the UI path.
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D2) // Tab 2 — PE/Metadata
            .WaitUntil(s => s.ContainsText("PE Headers"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.TypeDef, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.MethodDef, TimeSpan.FromSeconds(10))
            .WaitUntil(_ => _state!.PeFocusedKey is int, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.G)
            .WaitUntil(_ => _state!.CurrentTab == TabId.IlInspector, TimeSpan.FromSeconds(10))
            .WaitUntil(_ => _state!.CrossViewBackTarget is { Tab: TabId.PeMetadata }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var state = _state!;

        // Wait for "Esc: Back" hint to appear
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Esc: Back"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Open search — type "test" then press Escape
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion) // '/' — activate search
            .WaitUntil(_ => state.Search[TabId.IlInspector].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.T).Key(Hex1bKey.E).Key(Hex1bKey.S).Key(Hex1bKey.T) // type "test"
            .Key(Hex1bKey.Escape) // should dismiss search, NOT navigate back
            .WaitUntil(_ => !state.Search[TabId.IlInspector].IsActive, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Verify we stayed on IL Inspector — Escape dismissed search, didn't navigate back
        Assert.AreEqual(TabId.IlInspector, state.CurrentTab);
        Assert.IsNotNull(state.CrossViewBackTarget); // Back target still present

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab8 enter on jit event navigates to il inspector.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab8_Enter_OnJitEvent_NavigatesToIlInspector()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        // Navigate to Dynamic tab, launch trace, and wait for exit
        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Assembly Name");
        await auto.KeyAsync(Hex1bKey.D8, cts.Token);
        await auto.WaitUntilAsync(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"));
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state!.Tracer?.ProcessState
            is TraceProcessState.Exited or TraceProcessState.Error,
            timeout: TimeSpan.FromSeconds(30));

        var tracer = _state!.Tracer!;

        // HelloWorld defines Formatter.Format(int) and Formatter.Format(string).
        // Both produce JIT events with identical Detail ("Formatter.Format")
        // but distinct MetadataTokens. Deliberately target the SECOND overload
        // so that a name-only regression (FirstOrDefault by DeclaringType+Name)
        // would select the wrong method.
        var formatEvents = tracer.GetEvents()
            .Where(e => e.Category == TraceEventCategory.JIT
                     && e.Detail == "Formatter.Format")
            .ToList();

        Assert.IsGreaterThanOrEqualTo(2, formatEvents.Count, $"Expected >=2 Formatter.Format JIT events, got {formatEvents.Count}");

        var firstToken = formatEvents[0].MetadataToken;
        var targetEvent = formatEvents.First(e => e.MetadataToken != firstToken);
        Assert.IsGreaterThan(0, targetEvent.MetadataToken);

        var expectedMethod = _state.Analyzer.MethodDefs
            .FirstOrDefault(m => m.Token == targetEvent.MetadataToken);
        Assert.IsNotNull(expectedMethod);

        // Verify this IS an overload: name-based FirstOrDefault would return
        // a different method (the first match), proving token is required.
        Assert.IsTrue(DynamicAnalysisView.TryParseJitDetail(targetEvent.Detail,
            out var declType, out var methName));
        var byName = _state.Analyzer.MethodDefs
            .FirstOrDefault(m => m.DeclaringType == declType && m.Name == methName);
        Assert.IsNotNull(byName);

        Assert.AreNotEqual(expectedMethod.Token, byName.Token);

        // Use J key to set JIT filter (runs on the render thread, not a direct state mutation)
        var eventKey = $"{targetEvent.Timestamp.Ticks}:{targetEvent.EventName}:{targetEvent.Detail}:{targetEvent.MetadataToken}";
        await auto.KeyAsync(Hex1bKey.J, cts.Token);
        await auto.WaitUntilTextAsync("Filter: JIT");

        // Set focused key to the second overload's row, then press Enter
        _state.DynamicEventsFocusedKey = eventKey;
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state.CurrentTab == TabId.IlInspector);

        Assert.AreEqual(TabId.IlInspector, _state.CurrentTab);
        Assert.AreEqual(expectedMethod.Token, _state.IlSelectedMethod!.Token);
        Assert.IsNotNull(_state.CrossViewBackTarget);
        Assert.AreEqual(TabId.Dynamic, _state.CrossViewBackTarget.Value.Tab);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab8 jit navigation hint updates and enter navigates without rerun.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab8_JitNavigation_HintUpdatesAndEnterNavigatesWithoutRerun()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        // Navigate to Dynamic tab, launch trace, wait for exit to render on screen.
        // Navigate to Dynamic tab, launch trace. Wait for the target JIT events,
        // then stop the tracer if the process hasn't exited on its own.
        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Assembly Name");
        await auto.KeyAsync(Hex1bKey.D8, cts.Token);
        await auto.WaitUntilAsync(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"));
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilAsync(_ =>
            _state!.Tracer?.GetEvents().Any(e => e.Category == TraceEventCategory.JIT
                && e.Detail == "Formatter.Format" && e.MetadataToken > 0) == true,
            timeout: TimeSpan.FromSeconds(30));
        if (_state!.Tracer!.ProcessState == TraceProcessState.Running)
            await auto.Ctrl().KeyAsync(Hex1bKey.K, cts.Token);
        // Send Escape to trigger a render frame — App.Invalidate() alone doesn't
        // reliably wake the headless render loop. Escape is harmless on the Events
        // subtab after exit (no search or filter active at this point).
        await auto.EscapeAsync(cts.Token);
        await auto.WaitUntilTextAsync("Re-run", timeout: TimeSpan.FromSeconds(10));

        var tracer = _state!.Tracer!;

        // Filter to JIT events
        await auto.KeyAsync(Hex1bKey.J, cts.Token);
        await auto.WaitUntilTextAsync("Filter: JIT");

        // Find a navigable JIT event from the analyzed assembly
        var targetEvent = tracer.GetEvents()
            .First(e => e.Category == TraceEventCategory.JIT
                     && e.Detail == "Formatter.Format"
                     && e.MetadataToken > 0);
        var eventKey = $"{targetEvent.Timestamp.Ticks}:{targetEvent.EventName}:" +
                       $"{targetEvent.Detail}:{targetEvent.MetadataToken}";
        var expectedMethod = _state.Analyzer.MethodDefs
            .First(m => m.Token == targetEvent.MetadataToken);

        // Set the focused key and verify the method resolves (same check that
        // BuildEventsSubTab uses). Then set CanNavigateJitEvent directly — the
        // programmatic Invalidate() path races with terminal snapshots, and
        // sending arrow keys changes the focused row away from the target event.
        _state.DynamicEventsFocusedKey = eventKey;
        Assert.IsNotNull(DynamicAnalysisView.ResolveJitEventMethod(
            _state, tracer.GetEvents()));
        _state.CanNavigateJitEvent = true;
        _state.App.Invalidate();

        await auto.WaitUntilAsync(_ =>
            DynamicAnalysisView.ResolveJitEventMethod(_state!, tracer.GetEvents()) is not null,
            description: "focused JIT event to resolve to IL");

        // Press Enter — should navigate to IL Inspector, NOT re-run the trace
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state.CurrentTab == TabId.IlInspector);

        Assert.AreEqual(TabId.IlInspector, _state.CurrentTab);
        Assert.AreEqual(expectedMethod.Token, _state.IlSelectedMethod!.Token);
        Assert.IsNotNull(_state.CrossViewBackTarget);
        Assert.AreEqual(TabId.Dynamic, _state.CrossViewBackTarget.Value.Tab);

        // The tracer must NOT have been replaced — Enter navigated, not re-ran
        Assert.AreSame(tracer, _state.Tracer);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab8 search editing hint shows rerun not go to il.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab8_SearchEditing_HintShowsRerunNotGoToIl()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        // Navigate to Dynamic tab, launch trace. Wait for the target JIT events,
        // then stop the tracer if the process hasn't exited on its own (the traced
        // process can hang under EventPipe on Windows CI).
        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Assembly Name");
        await auto.KeyAsync(Hex1bKey.D8, cts.Token);
        await auto.WaitUntilAsync(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"));
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilAsync(_ =>
            _state!.Tracer?.GetEvents().Any(e => e.Category == TraceEventCategory.JIT
                && e.Detail == "Formatter.Format" && e.MetadataToken > 0) == true,
            timeout: TimeSpan.FromSeconds(30));
        if (_state!.Tracer!.ProcessState == TraceProcessState.Running)
            await auto.Ctrl().KeyAsync(Hex1bKey.K, cts.Token);
        // Send Escape to trigger a render frame — App.Invalidate() alone doesn't
        // reliably wake the headless render loop. Escape is harmless on the Events
        // subtab after exit (no search or filter active at this point).
        await auto.EscapeAsync(cts.Token);
        await auto.WaitUntilTextAsync("Re-run", timeout: TimeSpan.FromSeconds(10));

        var tracer = _state!.Tracer!;

        // Filter to JIT and focus a navigable event
        await auto.KeyAsync(Hex1bKey.J, cts.Token);
        await auto.WaitUntilTextAsync("Filter: JIT");

        var targetEvent = tracer.GetEvents()
            .First(e => e.Category == TraceEventCategory.JIT
                     && e.Detail == "Formatter.Format"
                     && e.MetadataToken > 0);
        var eventKey = $"{targetEvent.Timestamp.Ticks}:{targetEvent.EventName}:" +
                       $"{targetEvent.Detail}:{targetEvent.MetadataToken}";

        _state.DynamicEventsFocusedKey = eventKey;
        Assert.IsNotNull(DynamicAnalysisView.ResolveJitEventMethod(
            _state, tracer.GetEvents()));
        _state.CanNavigateJitEvent = true;
        _state.App.Invalidate();

        await auto.WaitUntilAsync(_ =>
            DynamicAnalysisView.ResolveJitEventMethod(_state!, tracer.GetEvents()) is not null,
            description: "focused JIT event to resolve to IL");

        // Open search — Enter now confirms search, not navigates
        await auto.KeyAsync(Hex1bKey.OemQuestion, cts.Token);
        await auto.WaitUntilAsync(_ => _state.Search[TabId.Dynamic].IsActive);

        // Hint must revert to "Re-run" while search is editing
        await auto.WaitUntilTextAsync("Re-run");

        // Tab must still be Dynamic — Enter did not navigate
        Assert.AreEqual(TabId.Dynamic, _state.CurrentTab);
        Assert.AreSame(tracer, _state.Tracer);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab8 enter during search editing confirms search not navigates.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab8_EnterDuringSearchEditing_ConfirmsSearchNotNavigates()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        // Navigate to Dynamic tab, launch trace. Wait for the target JIT events,
        // then stop the tracer if the process hasn't exited on its own.
        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Assembly Name");
        await auto.KeyAsync(Hex1bKey.D8, cts.Token);
        await auto.WaitUntilAsync(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"));
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilAsync(_ =>
            _state!.Tracer?.GetEvents().Any(e => e.Category == TraceEventCategory.JIT
                && e.Detail == "Formatter.Format" && e.MetadataToken > 0) == true,
            timeout: TimeSpan.FromSeconds(30));
        if (_state!.Tracer!.ProcessState == TraceProcessState.Running)
            await auto.Ctrl().KeyAsync(Hex1bKey.K, cts.Token);
        // Send Escape to trigger a render frame — App.Invalidate() alone doesn't
        // reliably wake the headless render loop. Escape is harmless on the Events
        // subtab after exit (no search or filter active at this point).
        await auto.EscapeAsync(cts.Token);
        await auto.WaitUntilTextAsync("Re-run", timeout: TimeSpan.FromSeconds(10));

        var tracer = _state!.Tracer!;

        // Filter to JIT and focus a navigable event
        await auto.KeyAsync(Hex1bKey.J, cts.Token);
        await auto.WaitUntilTextAsync("Filter: JIT");

        var targetEvent = tracer.GetEvents()
            .First(e => e.Category == TraceEventCategory.JIT
                     && e.Detail == "Formatter.Format"
                     && e.MetadataToken > 0);
        var eventKey = $"{targetEvent.Timestamp.Ticks}:{targetEvent.EventName}:" +
                       $"{targetEvent.Detail}:{targetEvent.MetadataToken}";
        _state.DynamicEventsFocusedKey = eventKey;
        Assert.IsNotNull(DynamicAnalysisView.ResolveJitEventMethod(
            _state, tracer.GetEvents()));
        _state.CanNavigateJitEvent = true;
        _state.App.Invalidate();

        await auto.WaitUntilAsync(_ =>
            DynamicAnalysisView.ResolveJitEventMethod(_state!, tracer.GetEvents()) is not null,
            description: "focused JIT event to resolve to IL");

        // Open search and type a query
        var search = _state.Search[TabId.Dynamic];
        await auto.KeyAsync(Hex1bKey.OemQuestion, cts.Token);
        await auto.WaitUntilAsync(_ => search.IsActive);
        await auto.TypeAsync("Format", cts.Token);
        await auto.WaitUntilAsync(_ => search.Query == "Format");

        Assert.IsTrue(search.IsActive);
        Assert.IsFalse(search.IsConfirmed);

        // Press Enter — should confirm search, NOT navigate to IL
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilAsync(_ => search.IsConfirmed);

        Assert.IsTrue(search.IsConfirmed);
        Assert.AreEqual("Format", search.Query);
        Assert.AreEqual(TabId.Dynamic, _state.CurrentTab);
        Assert.AreSame(tracer, _state.Tracer);

        cts.Cancel();
        await runTask;
    }

    // --- Apphost Detection ---

    /// <summary>
    /// Verifies apphost exe shows dialog.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ApphostExe_ShowsDialog()
    {

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldExe);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Apphost Detected"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsTrue(_state!.ApphostDialogOpen);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies apphost exe enter navigates to managed dll.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ApphostExe_Enter_NavigatesToManagedDll()
    {

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldExe);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Apphost Detected"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => !s.ContainsText("Apphost Detected"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("depth 2"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsFalse(_state!.ApphostDialogOpen);
        Assert.IsTrue(_state.Analyzer.HasMetadata);
        Assert.AreEqual("HelloWorld.dll", _state.Analyzer.FileName);
        Assert.ContainsSingle(_state.NavigationStack);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies apphost exe escape dismisses dialog.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ApphostExe_Escape_DismissesDialog()
    {

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldExe);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Apphost Detected"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => !s.ContainsText("Apphost Detected"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsFalse(_state!.ApphostDialogOpen);
        Assert.IsFalse(_state.Analyzer.HasMetadata);
        Assert.IsEmpty(_state.NavigationStack);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies apphost exe enter then back reshows dialog.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ApphostExe_Enter_ThenBack_ReshowsDialog()
    {

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.HelloWorldExe);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        // Accept the dialog → navigate into the managed .dll
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Apphost Detected"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => !s.ContainsText("Apphost Detected"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("depth 2"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsTrue(_state!.Analyzer.HasMetadata);

        // Back out → should re-show the dialog on the apphost .exe
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => s.ContainsText("Apphost Detected"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsTrue(_state.ApphostDialogOpen);
        Assert.IsFalse(_state.Analyzer.HasMetadata);
        Assert.IsNotNull(_state.ApphostCompanionDllPath);
        Assert.IsEmpty(_state.NavigationStack);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies the General tab shows the Native AOT info block (binary kind,
    /// ReadyToRun format version, runtime version, native import summary) for a
    /// Native AOT executable.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task General_NativeAot_ShowsAotInfo()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        var (terminal, app) = CreateDotsiderApp(Samples.NativeAotConsoleExe!);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Native AOT Sidecars Detected"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => !s.ContainsText("Native AOT Sidecars Detected"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Native AOT (.NET)"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("ILC / RTR Format"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Native Imports"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("R2R Sections"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Recovered Types"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.IsTrue(_state!.IsNativeAot);
        Assert.IsNotNull(_state.Analyzer.NativeAotInfo);
        Assert.IsNotEmpty(_state.Analyzer.ReadyToRunSections);
        Assert.IsNotEmpty(_state.Analyzer.RecoveredTypes);
        Assert.IsNotNull(_state.Analyzer.NativeSymbols);
        Assert.IsNotEmpty(_state.Analyzer.NativeSymbols.Symbols);

        cts.Cancel();
        await runTask;
    }

    private static string GetWasmNativePath()
    {
        TestSkip.When(Samples.WasmConsoleNativeWasm is null && Samples.ReadyToRunConsoleWasmNativeWasm is null,
            "browser-wasm publish did not run on this leg.");

        return Samples.WasmConsoleNativeWasm ?? Samples.ReadyToRunConsoleWasmNativeWasm!;
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
