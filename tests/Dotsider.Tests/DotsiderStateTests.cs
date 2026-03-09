using Dotsider.Core.Analysis;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

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

    [Fact(Timeout = 5_000)]
    public void ConstructFromHelloWorld_HasCorrectFileName()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.Equal("HelloWorld.dll", state.Analyzer.FileName);
    }

    [Fact(Timeout = 5_000)]
    public void HasEntryPoint_TrueForExe()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.True(state.HasEntryPoint);
    }

    [Fact(Timeout = 5_000)]
    public void HasEntryPoint_FalseForLibrary()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        Assert.False(state.HasEntryPoint);
    }

    [Fact(Timeout = 5_000)]
    public void HasEntryPoint_TrueForComplexApp()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.ComplexAppDll);
        Assert.True(state.HasEntryPoint);
    }

    [Fact(Timeout = 5_000)]
    public void HasEntryPoint_FalseForEmptyLib()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.EmptyLibDll);
        Assert.False(state.HasEntryPoint);
    }

    [Fact(Timeout = 5_000)]
    public void HasEntryPoint_FalseForNativeLib()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.NativeLibDll);
        Assert.False(state.HasEntryPoint);
    }

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
    public void PushAssembly_ChangesAnalyzer()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.Equal("HelloWorld.dll", state.Analyzer.FileName);
        Assert.True(state.PushAssembly(samples.RichLibraryDll));
        Assert.Equal("RichLibrary.dll", state.Analyzer.FileName);
        Assert.Single(state.NavigationStack);
    }

    [Fact(Timeout = 5_000)]
    public void PopAssembly_RestoresPrevious()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.True(state.PushAssembly(samples.RichLibraryDll));
        Assert.True(state.PopAssembly());
        Assert.Equal("HelloWorld.dll", state.Analyzer.FileName);
        Assert.Empty(state.NavigationStack);
    }

    [Fact(Timeout = 5_000)]
    public void PushAssembly_InvalidPath_ReturnsFalseAndSetsError()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.False(state.PushAssembly("/nonexistent/fake.dll"));
        Assert.Equal("HelloWorld.dll", state.Analyzer.FileName);
        Assert.Empty(state.NavigationStack);
        Assert.NotNull(state.NavigationError);
    }

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
    public void PopAssembly_ClearsNavigationError()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.True(state.PushAssembly(samples.RichLibraryDll));
        // Set an error, then pop
        state.PushAssembly("/nonexistent/fake.dll");
        Assert.NotNull(state.NavigationError);
        Assert.True(state.PopAssembly());
        Assert.Null(state.NavigationError);
    }

    [Fact(Timeout = 5_000)]
    public void PushAssembly_BadImage_ReturnsFalseAndPreservesState()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.False(state.PushAssembly(samples.NonDotNetBinaryPath));
        Assert.Equal("HelloWorld.dll", state.Analyzer.FileName);
        Assert.Empty(state.NavigationStack);
        Assert.Contains("Cannot open assembly", state.NavigationError);
    }

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
    public void PopAssembly_EmptyStack_ReturnsFalse()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.False(state.PopAssembly());
    }

    [Fact(Timeout = 5_000)]
    public void GetActiveStrings_ReturnsNonEmpty()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.StringsSourceTab = 1; // Metadata strings
        var strings = state.GetActiveStrings();
        Assert.NotEmpty(strings);
    }

    [Fact(Timeout = 5_000)]
    public void FormatSize_Zero()
    {
        Assert.Equal("0 B", DotsiderState.FormatSize(0));
    }

    [Fact(Timeout = 5_000)]
    public void FormatSize_KB()
    {
        Assert.Equal("1.0 KB", DotsiderState.FormatSize(1024));
    }

    [Fact(Timeout = 5_000)]
    public void FormatSize_MB()
    {
        Assert.Equal("1.0 MB", DotsiderState.FormatSize(1048576));
    }

    [Fact(Timeout = 5_000)]
    public void FormatSize_Bytes()
    {
        Assert.Equal("500 B", DotsiderState.FormatSize(500));
    }

    [Fact(Timeout = 5_000)]
    public void ConstructFromAnalyzer_Works()
    {
        var app = CreateApp();
        var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var state = new DotsiderState(app, analyzer);
        Assert.Equal("RichLibrary.dll", state.Analyzer.FileName);
        Assert.NotNull(state.IlDisassembler);
        Assert.NotNull(state.StringExtractor);
    }

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
    public void NavigateToTab_SwitchesCurrentTab()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.General;
        state.NavigateToTab(TabId.IlInspector);
        Assert.Equal(TabId.IlInspector, state.CurrentTab);
    }

    [Fact(Timeout = 5_000)]
    public void NavigateToTab_SameTab_NoOp()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.HexDump;
        state.NavigateToTab(TabId.HexDump);
        Assert.Equal(TabId.HexDump, state.CurrentTab);
    }

    [Fact(Timeout = 5_000)]
    public void NavigateToTab_ToIlInspector_SetsScrollRestore()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.NavigateToTab(TabId.IlInspector);
        Assert.Equal(2, state.IlScrollRestoreFrames);
        Assert.True(state.IlNeedsTreeFocus);
    }

    [Fact(Timeout = 5_000)]
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
        Assert.Equal(0, state.IlDisassemblyScrollOffset);
        Assert.NotNull(state.CrossViewBackTarget);
        Assert.Equal(TabId.PeMetadata, state.CrossViewBackTarget!.Value.Tab);
        Assert.Equal(PeSubTabId.MethodDef, state.CrossViewBackTarget!.Value.SubTab);
    }

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
    public void RvaToFileOffset_InvalidRva_ReturnsNegative()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        Assert.Equal(-1, state.RvaToFileOffset(0x7FFFFFFF));
    }

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
    public void NavigateToHexOffset_InvalidRva_NoTabSwitch()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;
        state.NavigateToHexOffset(0x7FFFFFFF);
        Assert.Equal(TabId.IlInspector, state.CurrentTab);
        Assert.Null(state.CrossViewBackTarget);
    }

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
    public void NavigateBack_NoTarget_NoOp()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;
        state.NavigateBack();
        Assert.Equal(TabId.IlInspector, state.CurrentTab);
    }

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
    public void NavigateToIlMethod_ThenHex_ThenBack_RestoresIl()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;

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
    }

    public void Dispose()
    {
        _app?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        GC.SuppressFinalize(this);
    }
}
