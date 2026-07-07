using Dotsider.Core.Analysis;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Dotsider State.
/// </summary>
[Collection("SampleAssemblies")]
public class DotsiderStateTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _app;

    private Hex1bApp CreateApp()
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();
        _app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = _workload });
        return _app;
    }

    /// <summary>
    /// Verifies construct from hello world has correct file name.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ConstructFromHelloWorld_HasCorrectFileName()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.Equal("HelloWorld.dll", state.Analyzer.FileName);
    }

    /// <summary>
    /// Verifies has entry point true for exe.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HasEntryPoint_TrueForExe()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.True(state.HasEntryPoint);
    }

    /// <summary>
    /// Verifies has entry point false for library.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HasEntryPoint_FalseForLibrary()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        Assert.False(state.HasEntryPoint);
    }

    /// <summary>
    /// Verifies has entry point true for complex app.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HasEntryPoint_TrueForComplexApp()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.ComplexAppDll);
        Assert.True(state.HasEntryPoint);
    }

    /// <summary>
    /// Verifies has entry point false for empty lib.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HasEntryPoint_FalseForEmptyLib()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.EmptyLibDll);
        Assert.False(state.HasEntryPoint);
    }

    /// <summary>
    /// Verifies has entry point false for native lib.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HasEntryPoint_FalseForNativeLib()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.NativeLibDll);
        Assert.False(state.HasEntryPoint);
    }

    /// <summary>
    /// Verifies is native aot false for all managed samples.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void IsNativeAot_FalseForAllManagedSamples()
    {
        var app = CreateApp();
        string[] paths = [samples.HelloWorldDll, samples.RichLibraryDll, samples.ComplexAppDll,
            samples.MinimalApiDll, samples.NativeLibDll, samples.EmptyLibDll];
        foreach (var path in paths)
        {
            using var state = new DotsiderState(app, path);
            Assert.False(state.IsNativeAot, $"IsNativeAot should be false for {Path.GetFileName(path)}");
        }
    }

    /// <summary>
    /// Verifies push assembly changes analyzer.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void PushAssembly_ChangesAnalyzer()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.Equal("HelloWorld.dll", state.Analyzer.FileName);
        Assert.True(state.PushAssembly(samples.RichLibraryDll));
        Assert.Equal("RichLibrary.dll", state.Analyzer.FileName);
        Assert.Single(state.NavigationStack);
    }

    /// <summary>
    /// Verifies pop assembly restores previous.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void PopAssembly_RestoresPrevious()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.True(state.PushAssembly(samples.RichLibraryDll));
        state.PopAssembly();
        Assert.Equal("HelloWorld.dll", state.Analyzer.FileName);
        Assert.Empty(state.NavigationStack);
    }

    /// <summary>
    /// Verifies push assembly invalid path returns false and sets error.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void PushAssembly_InvalidPath_ReturnsFalseAndSetsError()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.False(state.PushAssembly("/nonexistent/fake.dll"));
        Assert.Equal("HelloWorld.dll", state.Analyzer.FileName);
        Assert.Empty(state.NavigationStack);
        Assert.NotNull(state.NavigationError);
    }

    /// <summary>
    /// Verifies push assembly depth limit returns false at max.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void PushAssembly_DepthLimit_ReturnsFalseAtMax()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        // Push to the limit (alternating two assemblies)
        for (var i = 0; i < DotsiderState.MaxNavigationDepth; i++)
        {
            var path = i % 2 == 0 ? samples.RichLibraryDll : samples.EmptyLibDll;
            Assert.True(state.PushAssembly(path), $"Push {i + 1} should succeed");
        }
        // Next push should fail
        Assert.False(state.PushAssembly(samples.ComplexAppDll));
        Assert.Contains("depth limit", state.NavigationError);
    }

    /// <summary>
    /// Verifies push assembly success clears error.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void PushAssembly_SuccessClearsError()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        // Trigger an error first
        state.PushAssembly("/nonexistent/fake.dll");
        Assert.NotNull(state.NavigationError);
        // Successful push clears it
        Assert.True(state.PushAssembly(samples.RichLibraryDll));
        Assert.Null(state.NavigationError);
    }

    /// <summary>
    /// Verifies pop assembly clears navigation error.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void PopAssembly_ClearsNavigationError()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.True(state.PushAssembly(samples.RichLibraryDll));
        // Set an error, then pop
        state.PushAssembly("/nonexistent/fake.dll");
        Assert.NotNull(state.NavigationError);
        state.PopAssembly();
        Assert.Null(state.NavigationError);
    }

    /// <summary>
    /// Verifies push assembly bad image returns false and preserves state.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void PushAssembly_BadImage_ReturnsFalseAndPreservesState()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.False(state.PushAssembly(samples.NonDotNetBinaryPath));
        Assert.Equal("HelloWorld.dll", state.Analyzer.FileName);
        Assert.Empty(state.NavigationStack);
        Assert.Contains("Cannot open assembly", state.NavigationError);
    }

    /// <summary>
    /// Verifies push assembly unauthorized access returns false.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void PushAssembly_UnauthorizedAccess_ReturnsFalse()
    {
        // File.ReadAllBytes on a directory throws UnauthorizedAccessException on all platforms
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.False(state.PushAssembly(Path.GetTempPath()));
        Assert.Equal("HelloWorld.dll", state.Analyzer.FileName);
        Assert.Empty(state.NavigationStack);
        Assert.NotNull(state.NavigationError);
    }

    /// <summary>
    /// Verifies pop assembly empty stack is no op.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void PopAssembly_EmptyStack_IsNoOp()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        state.PopAssembly();
        Assert.Equal("HelloWorld.dll", state.Analyzer.FileName);
        Assert.Empty(state.NavigationStack);
    }

    /// <summary>
    /// Verifies get active strings returns non empty.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void GetActiveStrings_ReturnsNonEmpty()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.StringsSourceTab = 1; // Metadata strings
        var strings = state.GetActiveStrings();
        Assert.NotEmpty(strings);
    }

    /// <summary>
    /// Verifies format size zero.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FormatSize_Zero()
    {
        Assert.Equal("0 B", DotsiderState.FormatSize(0));
    }

    /// <summary>
    /// Verifies format size kb.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FormatSize_KB()
    {
        Assert.Equal("1.0 KB", DotsiderState.FormatSize(1024));
    }

    /// <summary>
    /// Verifies format size mb.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FormatSize_MB()
    {
        Assert.Equal("1.0 MB", DotsiderState.FormatSize(1048576));
    }

    /// <summary>
    /// Verifies format size bytes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FormatSize_Bytes()
    {
        Assert.Equal("500 B", DotsiderState.FormatSize(500));
    }

    /// <summary>
    /// Verifies construct from analyzer works.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ConstructFromAnalyzer_Works()
    {
        var app = CreateApp();
        var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var state = new DotsiderState(app, analyzer);
        Assert.Equal("RichLibrary.dll", state.Analyzer.FileName);
        Assert.NotNull(state.IlDisassembler);
        Assert.NotNull(state.StringExtractor);
    }

    /// <summary>
    /// Verifies all project types construct without error.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void AllProjectTypes_ConstructWithoutError()
    {
        var app = CreateApp();
        string[] paths = [samples.HelloWorldDll, samples.RichLibraryDll, samples.ComplexAppDll,
            samples.MinimalApiDll, samples.NativeLibDll, samples.EmptyLibDll, samples.RichLibraryV2Dll];
        foreach (var path in paths)
        {
            using var state = new DotsiderState(app, path);
            Assert.NotNull(state.Analyzer);
            Assert.NotNull(state.IlDisassembler);
            Assert.NotNull(state.StringExtractor);
        }
    }

    // --- Cross-View Navigation Tests ---

    /// <summary>
    /// Verifies navigate to tab switches current tab.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToTab_SwitchesCurrentTab()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.General;
        state.NavigateToTab(TabId.IlInspector);
        Assert.Equal(TabId.IlInspector, state.CurrentTab);
    }

    /// <summary>
    /// Verifies navigate to tab same tab no op.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToTab_SameTab_NoOp()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.HexDump;
        state.NavigateToTab(TabId.HexDump);
        Assert.Equal(TabId.HexDump, state.CurrentTab);
    }

    /// <summary>
    /// Verifies navigate to tab to il inspector switches tab.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToTab_ToIlInspector_SwitchesTab()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.NavigateToTab(TabId.IlInspector);
        Assert.Equal(TabId.IlInspector, state.CurrentTab);
    }

    /// <summary>
    /// Verifies navigate to tab il round trip preserves editor state.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToTab_IlRoundTrip_PreservesEditorState()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        state.CurrentTab = TabId.General;
        state.NavigateToTab(TabId.IlInspector);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument("test")) { IsReadOnly = true };

        // Leave IL, go to Strings, return
        state.NavigateToTab(TabId.Strings);
        state.NavigateToTab(TabId.IlInspector);

        // Editor state survives round-trip (Responsive preserves nodes)
        Assert.NotNull(state.IlEditorState);
        Assert.Equal(TabId.IlInspector, state.CurrentTab);
    }

    /// <summary>
    /// Verifies that NavigateToIlMethod sets the IlFocusedTreeKey to the
    /// jumped-to method's row key. This is the regression target for the
    /// IL tab-entry focus behavior — the table uses this key to deterministically
    /// focus the correct row.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToIlMethod_SetsIlFocusedTreeKey()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        // Initially null
        Assert.Null(state.IlFocusedTreeKey);

        state.CurrentTab = TabId.PeMetadata;
        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);

        // Focused key must point to the jumped-to method row
        Assert.Equal($"method:{method.Token}", state.IlFocusedTreeKey);
        Assert.Equal(TabId.IlInspector, state.CurrentTab);
    }

    /// <summary>
    /// Verifies navigate to il method sets state and switches tab.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToIlMethod_SetsStateAndSwitchesTab()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.MethodDef;

        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);

        Assert.Equal(TabId.IlInspector, state.CurrentTab);
        Assert.Equal(method, state.IlSelectedMethod);
        Assert.NotNull(state.CrossViewBackTarget);
        Assert.Equal(TabId.PeMetadata, state.CrossViewBackTarget!.Value.Tab);
        Assert.Equal(PeSubTabId.MethodDef, state.CrossViewBackTarget!.Value.SubTab);
        // Focused tree key must point to the jumped-to method row
        Assert.Equal($"method:{method.Token}", state.IlFocusedTreeKey);
    }

    /// <summary>
    /// Verifies navigate to il method expands tree nodes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToIlMethod_ExpandsTreeNodes()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;

        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);

        var typeDef = state.Analyzer.TypeDefs.First(t => t.FullName == method.DeclaringType);
        var ns = string.IsNullOrEmpty(typeDef.Namespace) ? "(global)" : typeDef.Namespace;
        Assert.True(state.IlTreeExpansionState[$"ns:{ns}"]);
        Assert.True(state.IlTreeExpansionState[$"type:{method.DeclaringType}"]);
    }

    /// <summary>
    /// Verifies navigate to il method clears stale il search.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToIlMethod_ClearsStaleIlSearch()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;

        var ilSearch = state.Search[TabId.IlInspector];
        ilSearch.ActivateOrCycle();
        ilSearch.UpdateQuery("Foo");
        ilSearch.Confirm();
        ilSearch.SetMatchCount(3);

        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);

        Assert.False(ilSearch.IsActive);
        Assert.False(ilSearch.IsConfirmed);
        Assert.Null(ilSearch.Query);
        Assert.Equal(-1, ilSearch.MatchCount);
    }

    /// <summary>
    /// Verifies rva to file offset returns correct offset.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RvaToFileOffset_ReturnsCorrectOffset()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        // Find a method with an RVA that falls within a section
        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        var offset = state.RvaToFileOffset(method.Rva);
        Assert.True(offset >= 0, "RVA should resolve to a valid file offset");

        // Verify the offset falls within the raw data range of some section
        var foundSection = state.Analyzer.Sections.Any(s =>
            offset >= s.RawDataOffset && offset < s.RawDataOffset + s.RawDataSize);
        Assert.True(foundSection, "File offset should be within a section's raw data");
    }

    /// <summary>
    /// Verifies rva to file offset invalid rva returns negative.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RvaToFileOffset_InvalidRva_ReturnsNegative()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        Assert.Equal(-1, state.RvaToFileOffset(0x7FFFFFFF));
    }

    /// <summary>
    /// Verifies navigate to hex offset sets state and switches tab.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToHexOffset_SetsStateAndSwitchesTab()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;

        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToHexOffset(method.Rva);

        Assert.Equal(TabId.HexDump, state.CurrentTab);
        Assert.NotNull(state.HexScrollTarget);
        Assert.NotNull(state.CrossViewBackTarget);
        Assert.Equal(TabId.IlInspector, state.CrossViewBackTarget!.Value.Tab);
    }

    /// <summary>
    /// Verifies navigate to hex offset sets cursor position.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToHexOffset_SetsCursorPosition()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;

        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        var expectedOffset = state.RvaToFileOffset(method.Rva);
        state.NavigateToHexOffset(method.Rva);

        Assert.Equal((int)expectedOffset, state.HexEditorState.ByteCursorOffset);
        Assert.Equal(expectedOffset, state.HexScrollTarget);
    }

    /// <summary>
    /// Verifies a raw Wasm function can jump to its file-backed bytes in the Hex Dump.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToHexFileOffset_WasmFunction_SetsCursorPosition()
    {
        var wasmPath = GetWasmNativePath();
        var app = CreateApp();
        using var state = new DotsiderState(app, wasmPath);
        state.CurrentTab = TabId.IlInspector;

        var symbol = state.Analyzer.NativeSymbols!.Symbols.First(s => s.FileOffset is not null && s.Size > 0);
        state.IlSelectedNativeSymbol = symbol;
        state.NavigateToHexFileOffset(symbol.FileOffset!.Value);

        Assert.Equal(TabId.HexDump, state.CurrentTab);
        Assert.Equal((int)symbol.FileOffset.Value, state.HexEditorState.ByteCursorOffset);
        Assert.Equal(symbol.FileOffset.Value, state.HexScrollTarget);
        Assert.NotNull(state.CrossViewBackTarget);
        Assert.Equal(TabId.IlInspector, state.CrossViewBackTarget!.Value.Tab);
    }

    /// <summary>
    /// Verifies navigate to hex offset invalid rva no tab switch.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToHexOffset_InvalidRva_NoTabSwitch()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;
        state.NavigateToHexOffset(0x7FFFFFFF);
        Assert.Equal(TabId.IlInspector, state.CurrentTab);
        Assert.Null(state.CrossViewBackTarget);
    }

    /// <summary>
    /// Verifies navigate back restores previous tab.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateBack_RestoresPreviousTab()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.TypeDef;

        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);
        Assert.Equal(TabId.IlInspector, state.CurrentTab);

        state.NavigateBack();
        Assert.Equal(TabId.PeMetadata, state.CurrentTab);
        Assert.Equal(PeSubTabId.TypeDef, state.PeSubTab);
        Assert.Null(state.CrossViewBackTarget);
    }

    /// <summary>
    /// Verifies navigate back no target no op.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateBack_NoTarget_NoOp()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;
        state.NavigateBack();
        Assert.Equal(TabId.IlInspector, state.CurrentTab);
    }

    /// <summary>
    /// Verifies push assembly clears cross view back target.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void PushAssembly_ClearsCrossViewBackTarget()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;

        // Create a cross-view back target
        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);
        Assert.NotNull(state.CrossViewBackTarget);

        // Push a new assembly — should clear the stale back target
        state.PushAssembly(samples.HelloWorldDll);
        Assert.Null(state.CrossViewBackTarget);
    }

    /// <summary>
    /// Verifies pop assembly clears cross view back target.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void PopAssembly_ClearsCrossViewBackTarget()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        // Push first, then create back target
        state.PushAssembly(samples.HelloWorldDll);
        state.CurrentTab = TabId.PeMetadata;
        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);
        Assert.NotNull(state.CrossViewBackTarget);

        // Pop — should clear back target
        state.PopAssembly();
        Assert.Null(state.CrossViewBackTarget);
    }

    /// <summary>
    /// Verifies navigate to il method then hex then back restores il.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToIlMethod_ThenHex_ThenBack_RestoresIl()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.MethodDef;

        // PE → IL Inspector
        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);
        Assert.Equal(TabId.IlInspector, state.CurrentTab);

        // IL Inspector → Hex Dump
        state.NavigateToHexOffset(method.Rva);
        Assert.Equal(TabId.HexDump, state.CurrentTab);
        // Back target should be IL Inspector (most recent navigation)
        Assert.Equal(TabId.IlInspector, state.CrossViewBackTarget!.Value.Tab);

        // Back → IL Inspector
        state.NavigateBack();
        Assert.Equal(TabId.IlInspector, state.CurrentTab);

        // The PE Metadata frame underneath must remain reachable via a second
        // Esc — chained cross-view jumps unwind one frame at a time, with the
        // exact origin sub-tab preserved.
        Assert.Equal((TabId.PeMetadata, PeSubTabId.MethodDef), state.CrossViewBackTarget);
    }

    // --- Apphost Detection ---

    /// <summary>
    /// Verifies construct from apphost exe sets apphost dialog state.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ConstructFromApphostExe_SetsApphostDialogState()
    {

        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldExe);

        Assert.True(state.ApphostDialogOpen);
        Assert.NotNull(state.ApphostCompanionDllPath);
        Assert.EndsWith(".dll", state.ApphostCompanionDllPath!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies construct from managed dll no apphost dialog.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ConstructFromManagedDll_NoApphostDialog()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);

        Assert.False(state.ApphostDialogOpen);
        Assert.Null(state.ApphostCompanionDllPath);
    }

    /// <summary>
    /// Verifies push assembly from apphost to companion dll works.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void PushAssembly_FromApphostToCompanionDll_Works()
    {

        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldExe);
        Assert.False(state.Analyzer.HasMetadata);

        Assert.True(state.PushAssembly(state.ApphostCompanionDllPath!));

        Assert.True(state.Analyzer.HasMetadata);
        Assert.Equal("HelloWorld.dll", state.Analyzer.FileName);
        Assert.Single(state.NavigationStack);
    }

    /// <summary>
    /// Verifies tab 3 keeps its managed label for ordinary IL inspection.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void IlInspectorTabLabel_ManagedAssembly_IsIlInspector()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);

        Assert.Equal(IlInspectorTabLabel.IlInspector, IlInspectorTabLabel.For(state));
    }

    /// <summary>
    /// Verifies tab 3 names the native-only AOT surface as disassembly.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void IlInspectorTabLabel_NativeAotWithoutAttachment_IsDisassembly()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var app = CreateApp();
        using var state = new DotsiderState(app, samples.NativeAotConsoleExe!);

        Assert.Equal(IlInspectorTabLabel.Disassembly, IlInspectorTabLabel.For(state));
    }

    /// <summary>
    /// Verifies tab 3 names ReadyToRun as an IL plus native surface.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void IlInspectorTabLabel_ReadyToRun_IsIlAndNative()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, "ReadyToRun crossgen2 publish did not run on this leg.");

        var app = CreateApp();
        using var state = new DotsiderState(app, samples.ReadyToRunConsoleDll!);

        Assert.Equal(IlInspectorTabLabel.IlAndNative, IlInspectorTabLabel.For(state));
    }

    /// <summary>
    /// Verifies the pre-ILC sidecar toggle switches tab 3 between paired IL/native and native disassembly.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void IlInspectorTabLabel_PreIlcAttachment_FollowsTreeToggle()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");
        Assert.SkipWhen(samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var app = CreateApp();
        using var state = new DotsiderState(app, samples.NativeAotConsoleExe!);

        Assert.True(state.AttachPreIlc());
        Assert.Equal(IlInspectorTabLabel.IlAndNative, IlInspectorTabLabel.For(state));

        state.IlAotTreeNativeView = true;

        Assert.Equal(IlInspectorTabLabel.Disassembly, IlInspectorTabLabel.For(state));
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        _app?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        GC.SuppressFinalize(this);
    }

    private string GetWasmNativePath()
    {
        Assert.SkipWhen(samples.WasmConsoleNativeWasm is null && samples.ReadyToRunConsoleWasmNativeWasm is null,
            "browser-wasm sample publish did not produce dotnet.native.wasm on this leg.");

        return samples.WasmConsoleNativeWasm ?? samples.ReadyToRunConsoleWasmNativeWasm!;
    }
}
