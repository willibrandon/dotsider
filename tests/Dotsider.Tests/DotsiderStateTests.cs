using Dotsider.Core.Analysis;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Dotsider State.
/// </summary>
[TestClass]
public class DotsiderStateTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

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
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ConstructFromHelloWorld_HasCorrectFileName()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.HelloWorldDll);
        Assert.AreEqual("HelloWorld.dll", state.Analyzer.FileName);
    }

    /// <summary>
    /// Verifies has entry point true for exe.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HasEntryPoint_TrueForExe()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.HelloWorldDll);
        Assert.IsTrue(state.HasEntryPoint);
    }

    /// <summary>
    /// Verifies has entry point false for library.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HasEntryPoint_FalseForLibrary()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        Assert.IsFalse(state.HasEntryPoint);
    }

    /// <summary>
    /// Verifies has entry point true for complex app.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HasEntryPoint_TrueForComplexApp()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.ComplexAppDll);
        Assert.IsTrue(state.HasEntryPoint);
    }

    /// <summary>
    /// Verifies has entry point false for empty lib.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HasEntryPoint_FalseForEmptyLib()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.EmptyLibDll);
        Assert.IsFalse(state.HasEntryPoint);
    }

    /// <summary>
    /// Verifies has entry point false for native lib.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HasEntryPoint_FalseForNativeLib()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.NativeLibDll);
        Assert.IsFalse(state.HasEntryPoint);
    }

    /// <summary>
    /// Verifies is native aot false for all managed Samples.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IsNativeAot_FalseForAllManagedSamples()
    {
        var app = CreateApp();
        string[] paths = [Samples.HelloWorldDll, Samples.RichLibraryDll, Samples.ComplexAppDll,
            Samples.MinimalApiDll, Samples.NativeLibDll, Samples.EmptyLibDll];
        foreach (var path in paths)
        {
            using var state = new DotsiderState(app, path);
            Assert.IsFalse(state.IsNativeAot, $"IsNativeAot should be false for {Path.GetFileName(path)}");
        }
    }

    /// <summary>
    /// Verifies push assembly changes analyzer.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PushAssembly_ChangesAnalyzer()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.HelloWorldDll);
        Assert.AreEqual("HelloWorld.dll", state.Analyzer.FileName);
        Assert.IsTrue(state.PushAssembly(Samples.RichLibraryDll));
        Assert.AreEqual("RichLibrary.dll", state.Analyzer.FileName);
        Assert.ContainsSingle(state.NavigationStack);
    }

    /// <summary>
    /// Verifies pop assembly restores previous.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PopAssembly_RestoresPrevious()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.HelloWorldDll);
        Assert.IsTrue(state.PushAssembly(Samples.RichLibraryDll));
        state.PopAssembly();
        Assert.AreEqual("HelloWorld.dll", state.Analyzer.FileName);
        Assert.IsEmpty(state.NavigationStack);
    }

    /// <summary>
    /// Verifies push assembly invalid path returns false and sets error.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PushAssembly_InvalidPath_ReturnsFalseAndSetsError()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.HelloWorldDll);
        Assert.IsFalse(state.PushAssembly("/nonexistent/fake.dll"));
        Assert.AreEqual("HelloWorld.dll", state.Analyzer.FileName);
        Assert.IsEmpty(state.NavigationStack);
        Assert.IsNotNull(state.NavigationError);
    }

    /// <summary>
    /// Verifies push assembly depth limit returns false at max.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PushAssembly_DepthLimit_ReturnsFalseAtMax()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.HelloWorldDll);
        // Push to the limit (alternating two assemblies)
        for (var i = 0; i < DotsiderState.MaxNavigationDepth; i++)
        {
            var path = i % 2 == 0 ? Samples.RichLibraryDll : Samples.EmptyLibDll;
            Assert.IsTrue(state.PushAssembly(path), $"Push {i + 1} should succeed");
        }
        // Next push should fail
        Assert.IsFalse(state.PushAssembly(Samples.ComplexAppDll));
        Assert.Contains("depth limit", state.NavigationError!);
    }

    /// <summary>
    /// Verifies push assembly success clears error.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PushAssembly_SuccessClearsError()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.HelloWorldDll);
        // Trigger an error first
        state.PushAssembly("/nonexistent/fake.dll");
        Assert.IsNotNull(state.NavigationError);
        // Successful push clears it
        Assert.IsTrue(state.PushAssembly(Samples.RichLibraryDll));
        Assert.IsNull(state.NavigationError);
    }

    /// <summary>
    /// Verifies pop assembly clears navigation error.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PopAssembly_ClearsNavigationError()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.HelloWorldDll);
        Assert.IsTrue(state.PushAssembly(Samples.RichLibraryDll));
        // Set an error, then pop
        state.PushAssembly("/nonexistent/fake.dll");
        Assert.IsNotNull(state.NavigationError);
        state.PopAssembly();
        Assert.IsNull(state.NavigationError);
    }

    /// <summary>
    /// Verifies push assembly bad image returns false and preserves state.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PushAssembly_BadImage_ReturnsFalseAndPreservesState()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.HelloWorldDll);
        Assert.IsFalse(state.PushAssembly(Samples.NonDotNetBinaryPath));
        Assert.AreEqual("HelloWorld.dll", state.Analyzer.FileName);
        Assert.IsEmpty(state.NavigationStack);
        Assert.Contains("Cannot open assembly", state.NavigationError!);
    }

    /// <summary>
    /// Verifies push assembly unauthorized access returns false.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PushAssembly_UnauthorizedAccess_ReturnsFalse()
    {
        // File.ReadAllBytes on a directory throws UnauthorizedAccessException on all platforms
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.HelloWorldDll);
        Assert.IsFalse(state.PushAssembly(Path.GetTempPath()));
        Assert.AreEqual("HelloWorld.dll", state.Analyzer.FileName);
        Assert.IsEmpty(state.NavigationStack);
        Assert.IsNotNull(state.NavigationError);
    }

    /// <summary>
    /// Verifies pop assembly empty stack is no op.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PopAssembly_EmptyStack_IsNoOp()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.HelloWorldDll);
        state.PopAssembly();
        Assert.AreEqual("HelloWorld.dll", state.Analyzer.FileName);
        Assert.IsEmpty(state.NavigationStack);
    }

    /// <summary>
    /// Verifies get active strings returns non empty.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetActiveStrings_ReturnsNonEmpty()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.StringsSourceTab = 1; // Metadata strings
        var strings = state.GetActiveStrings();
        Assert.IsNotEmpty(strings);
    }

    /// <summary>
    /// Verifies format size zero.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FormatSize_Zero()
    {
        Assert.AreEqual("0 B", DotsiderState.FormatSize(0));
    }

    /// <summary>
    /// Verifies format size kb.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FormatSize_KB()
    {
        Assert.AreEqual("1.0 KB", DotsiderState.FormatSize(1024));
    }

    /// <summary>
    /// Verifies format size mb.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FormatSize_MB()
    {
        Assert.AreEqual("1.0 MB", DotsiderState.FormatSize(1048576));
    }

    /// <summary>
    /// Verifies format size bytes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FormatSize_Bytes()
    {
        Assert.AreEqual("500 B", DotsiderState.FormatSize(500));
    }

    /// <summary>
    /// Verifies construct from analyzer works.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ConstructFromAnalyzer_Works()
    {
        var app = CreateApp();
        var analyzer = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var state = new DotsiderState(app, analyzer);
        Assert.AreEqual("RichLibrary.dll", state.Analyzer.FileName);
        Assert.IsNotNull(state.IlDisassembler);
        Assert.IsNotNull(state.StringExtractor);
    }

    /// <summary>
    /// Verifies all project types construct without error.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AllProjectTypes_ConstructWithoutError()
    {
        var app = CreateApp();
        string[] paths = [Samples.HelloWorldDll, Samples.RichLibraryDll, Samples.ComplexAppDll,
            Samples.MinimalApiDll, Samples.NativeLibDll, Samples.EmptyLibDll, Samples.RichLibraryV2Dll];
        foreach (var path in paths)
        {
            using var state = new DotsiderState(app, path);
            Assert.IsNotNull(state.Analyzer);
            Assert.IsNotNull(state.IlDisassembler);
            Assert.IsNotNull(state.StringExtractor);
        }
    }

    // --- Cross-View Navigation Tests ---

    /// <summary>
    /// Verifies navigate to tab switches current tab.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateToTab_SwitchesCurrentTab()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.CurrentTab = TabId.General;
        state.NavigateToTab(TabId.IlInspector);
        Assert.AreEqual(TabId.IlInspector, state.CurrentTab);
    }

    /// <summary>
    /// Verifies navigate to tab same tab no op.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateToTab_SameTab_NoOp()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.CurrentTab = TabId.HexDump;
        state.NavigateToTab(TabId.HexDump);
        Assert.AreEqual(TabId.HexDump, state.CurrentTab);
    }

    /// <summary>
    /// Verifies navigate to tab to il inspector switches tab.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateToTab_ToIlInspector_SwitchesTab()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.NavigateToTab(TabId.IlInspector);
        Assert.AreEqual(TabId.IlInspector, state.CurrentTab);
    }

    /// <summary>
    /// Verifies navigate to tab il round trip preserves editor state.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateToTab_IlRoundTrip_PreservesEditorState()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);

        state.CurrentTab = TabId.General;
        state.NavigateToTab(TabId.IlInspector);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument("test"))
        { IsReadOnly = true };

        // Leave IL, go to Strings, return
        state.NavigateToTab(TabId.Strings);
        state.NavigateToTab(TabId.IlInspector);

        // Editor state survives round-trip (Responsive preserves nodes)
        Assert.IsNotNull(state.IlEditorState);
        Assert.AreEqual(TabId.IlInspector, state.CurrentTab);
    }

    /// <summary>
    /// Verifies that NavigateToIlMethod sets the IlFocusedTreeKey to the
    /// jumped-to method's row key. This is the regression target for the
    /// IL tab-entry focus behavior — the table uses this key to deterministically
    /// focus the correct row.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateToIlMethod_SetsIlFocusedTreeKey()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);

        // Initially null
        Assert.IsNull(state.IlFocusedTreeKey);

        state.CurrentTab = TabId.PeMetadata;
        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);

        // Focused key must point to the jumped-to method row
        Assert.AreEqual($"method:{method.Token}", state.IlFocusedTreeKey);
        Assert.AreEqual(TabId.IlInspector, state.CurrentTab);
    }

    /// <summary>
    /// Verifies navigate to il method sets state and switches tab.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateToIlMethod_SetsStateAndSwitchesTab()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.MethodDef;

        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);

        Assert.AreEqual(TabId.IlInspector, state.CurrentTab);
        Assert.AreEqual(method, state.IlSelectedMethod);
        Assert.IsNotNull(state.CrossViewBackTarget);
        Assert.AreEqual(TabId.PeMetadata, state.CrossViewBackTarget!.Value.Tab);
        Assert.AreEqual(PeSubTabId.MethodDef, state.CrossViewBackTarget!.Value.SubTab);
        // Focused tree key must point to the jumped-to method row
        Assert.AreEqual($"method:{method.Token}", state.IlFocusedTreeKey);
    }

    /// <summary>
    /// Verifies navigate to il method expands tree nodes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateToIlMethod_ExpandsTreeNodes()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;

        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);

        var typeDef = state.Analyzer.TypeDefs.First(t => t.FullName == method.DeclaringType);
        var ns = string.IsNullOrEmpty(typeDef.Namespace) ? "(global)" : typeDef.Namespace;
        Assert.IsTrue(state.IlTreeExpansionState[$"ns:{ns}"]);
        Assert.IsTrue(state.IlTreeExpansionState[$"type:{method.DeclaringType}"]);
    }

    /// <summary>
    /// Verifies navigate to il method clears stale il search.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateToIlMethod_ClearsStaleIlSearch()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;

        var ilSearch = state.Search[TabId.IlInspector];
        ilSearch.ActivateOrCycle();
        ilSearch.UpdateQuery("Foo");
        ilSearch.Confirm();
        ilSearch.SetMatchCount(3);

        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);

        Assert.IsFalse(ilSearch.IsActive);
        Assert.IsFalse(ilSearch.IsConfirmed);
        Assert.IsNull(ilSearch.Query);
        Assert.AreEqual(-1, ilSearch.MatchCount);
    }

    /// <summary>
    /// Verifies rva to file offset returns correct offset.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RvaToFileOffset_ReturnsCorrectOffset()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);

        // Find a method with an RVA that falls within a section
        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        var offset = state.RvaToFileOffset(method.Rva);
        Assert.IsGreaterThanOrEqualTo(0, offset, "RVA should resolve to a valid file offset");

        // Verify the offset falls within the raw data range of some section
        var foundSection = state.Analyzer.Sections.Any(s =>
            offset >= s.RawDataOffset && offset < s.RawDataOffset + s.RawDataSize);
        Assert.IsTrue(foundSection, "File offset should be within a section's raw data");
    }

    /// <summary>
    /// Verifies rva to file offset invalid rva returns negative.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RvaToFileOffset_InvalidRva_ReturnsNegative()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        Assert.AreEqual(-1, state.RvaToFileOffset(0x7FFFFFFF));
    }

    /// <summary>
    /// Verifies navigate to hex offset sets state and switches tab.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateToHexOffset_SetsStateAndSwitchesTab()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;

        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToHexOffset(method.Rva);

        Assert.AreEqual(TabId.HexDump, state.CurrentTab);
        Assert.IsNotNull(state.HexScrollTarget);
        Assert.IsNotNull(state.CrossViewBackTarget);
        Assert.AreEqual(TabId.IlInspector, state.CrossViewBackTarget!.Value.Tab);
    }

    /// <summary>
    /// Verifies navigate to hex offset sets cursor position.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateToHexOffset_SetsCursorPosition()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;

        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        var expectedOffset = state.RvaToFileOffset(method.Rva);
        state.NavigateToHexOffset(method.Rva);

        Assert.AreEqual((int)expectedOffset, state.HexEditorState.ByteCursorOffset);
        Assert.AreEqual(expectedOffset, state.HexScrollTarget);
    }

    /// <summary>
    /// Verifies a raw Wasm function can jump to its file-backed bytes in the Hex Dump.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateToHexFileOffset_WasmFunction_SetsCursorPosition()
    {
        var wasmPath = GetWasmNativePath();
        var app = CreateApp();
        using var state = new DotsiderState(app, wasmPath);
        state.CurrentTab = TabId.IlInspector;

        var symbol = state.Analyzer.NativeSymbols!.Symbols.First(s => s.FileOffset is not null && s.Size > 0);
        state.IlSelectedNativeSymbol = symbol;
        state.NavigateToHexFileOffset(symbol.FileOffset!.Value);

        Assert.AreEqual(TabId.HexDump, state.CurrentTab);
        Assert.AreEqual((int)symbol.FileOffset.Value, state.HexEditorState.ByteCursorOffset);
        Assert.AreEqual(symbol.FileOffset.Value, state.HexScrollTarget);
        Assert.IsNotNull(state.CrossViewBackTarget);
        Assert.AreEqual(TabId.IlInspector, state.CrossViewBackTarget!.Value.Tab);
    }

    /// <summary>
    /// Verifies navigate to hex offset invalid rva no tab switch.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateToHexOffset_InvalidRva_NoTabSwitch()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;
        state.NavigateToHexOffset(0x7FFFFFFF);
        Assert.AreEqual(TabId.IlInspector, state.CurrentTab);
        Assert.IsNull(state.CrossViewBackTarget);
    }

    /// <summary>
    /// Verifies navigate back restores previous tab.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateBack_RestoresPreviousTab()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.TypeDef;

        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);
        Assert.AreEqual(TabId.IlInspector, state.CurrentTab);

        state.NavigateBack();
        Assert.AreEqual(TabId.PeMetadata, state.CurrentTab);
        Assert.AreEqual(PeSubTabId.TypeDef, state.PeSubTab);
        Assert.IsNull(state.CrossViewBackTarget);
    }

    /// <summary>
    /// Verifies navigate back no target no op.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateBack_NoTarget_NoOp()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;
        state.NavigateBack();
        Assert.AreEqual(TabId.IlInspector, state.CurrentTab);
    }

    /// <summary>
    /// Verifies push assembly clears cross view back target.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PushAssembly_ClearsCrossViewBackTarget()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;

        // Create a cross-view back target
        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);
        Assert.IsNotNull(state.CrossViewBackTarget);

        // Push a new assembly — should clear the stale back target
        state.PushAssembly(Samples.HelloWorldDll);
        Assert.IsNull(state.CrossViewBackTarget);
    }

    /// <summary>
    /// Verifies pop assembly clears cross view back target.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PopAssembly_ClearsCrossViewBackTarget()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);

        // Push first, then create back target
        state.PushAssembly(Samples.HelloWorldDll);
        state.CurrentTab = TabId.PeMetadata;
        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);
        Assert.IsNotNull(state.CrossViewBackTarget);

        // Pop — should clear back target
        state.PopAssembly();
        Assert.IsNull(state.CrossViewBackTarget);
    }

    /// <summary>
    /// Verifies navigate to il method then hex then back restores il.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateToIlMethod_ThenHex_ThenBack_RestoresIl()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.MethodDef;

        // PE → IL Inspector
        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);
        Assert.AreEqual(TabId.IlInspector, state.CurrentTab);

        // IL Inspector → Hex Dump
        state.NavigateToHexOffset(method.Rva);
        Assert.AreEqual(TabId.HexDump, state.CurrentTab);
        // Back target should be IL Inspector (most recent navigation)
        Assert.AreEqual(TabId.IlInspector, state.CrossViewBackTarget!.Value.Tab);

        // Back → IL Inspector
        state.NavigateBack();
        Assert.AreEqual(TabId.IlInspector, state.CurrentTab);

        // The PE Metadata frame underneath must remain reachable via a second
        // Esc — chained cross-view jumps unwind one frame at a time, with the
        // exact origin sub-tab preserved.
        Assert.AreEqual((TabId.PeMetadata, PeSubTabId.MethodDef), state.CrossViewBackTarget);
    }

    // --- Apphost Detection ---

    /// <summary>
    /// Verifies construct from apphost exe sets apphost dialog state.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ConstructFromApphostExe_SetsApphostDialogState()
    {

        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.HelloWorldExe);

        Assert.IsTrue(state.ApphostDialogOpen);
        Assert.IsNotNull(state.ApphostCompanionDllPath);
        Assert.EndsWith(".dll", state.ApphostCompanionDllPath!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies construct from managed dll no apphost dialog.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ConstructFromManagedDll_NoApphostDialog()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.HelloWorldDll);

        Assert.IsFalse(state.ApphostDialogOpen);
        Assert.IsNull(state.ApphostCompanionDllPath);
    }

    /// <summary>
    /// Verifies push assembly from apphost to companion dll works.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PushAssembly_FromApphostToCompanionDll_Works()
    {

        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.HelloWorldExe);
        Assert.IsFalse(state.Analyzer.HasMetadata);

        Assert.IsTrue(state.PushAssembly(state.ApphostCompanionDllPath!));

        Assert.IsTrue(state.Analyzer.HasMetadata);
        Assert.AreEqual("HelloWorld.dll", state.Analyzer.FileName);
        Assert.ContainsSingle(state.NavigationStack);
    }

    /// <summary>
    /// Verifies tab 3 keeps its managed label for ordinary IL inspection.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IlInspectorTabLabel_ManagedAssembly_IsIlInspector()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.HelloWorldDll);

        Assert.AreEqual(IlInspectorTabLabel.IlInspector, IlInspectorTabLabel.For(state));
    }

    /// <summary>
    /// Verifies tab 3 names the native-only AOT surface as disassembly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IlInspectorTabLabel_NativeAotWithoutAttachment_IsDisassembly()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.NativeAotConsoleExe!);

        Assert.AreEqual(IlInspectorTabLabel.Disassembly, IlInspectorTabLabel.For(state));
    }

    /// <summary>
    /// Verifies tab 3 names ReadyToRun as an IL plus native surface.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IlInspectorTabLabel_ReadyToRun_IsIlAndNative()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, "ReadyToRun crossgen2 publish did not run on this leg.");

        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.ReadyToRunConsoleDll!);

        Assert.AreEqual(IlInspectorTabLabel.IlAndNative, IlInspectorTabLabel.For(state));
    }

    /// <summary>
    /// Verifies the pre-ILC sidecar toggle switches tab 3 between paired IL/native and native disassembly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IlInspectorTabLabel_PreIlcAttachment_FollowsTreeToggle()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");
        TestSkip.When(Samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.NativeAotConsoleExe!);

        Assert.IsTrue(state.AttachPreIlc());
        Assert.AreEqual(IlInspectorTabLabel.IlAndNative, IlInspectorTabLabel.For(state));

        state.IlAotTreeNativeView = true;

        Assert.AreEqual(IlInspectorTabLabel.Disassembly, IlInspectorTabLabel.For(state));
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _app?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
    }

    private static string GetWasmNativePath()
    {
        TestSkip.When(Samples.WasmConsoleNativeWasm is null && Samples.ReadyToRunConsoleWasmNativeWasm is null,
            "browser-wasm sample publish did not produce dotnet.native.wasm on this leg.");

        return Samples.WasmConsoleNativeWasm ?? Samples.ReadyToRunConsoleWasmNativeWasm!;
    }
}
