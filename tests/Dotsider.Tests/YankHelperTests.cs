using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Infrastructure;
using Dotsider.Views;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Widgets;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class YankHelperTests(SampleAssemblyFixture samples) : IDisposable
{
    private readonly List<IDisposable> _disposables = [];

    private DotsiderState CreateState(string dllPath)
    {
        var workload = new Hex1bAppWorkloadAdapter();
        _disposables.Add(workload);
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();
        _disposables.Add(terminal);
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("")),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        _disposables.Add(app);
        return new DotsiderState(app, dllPath);
    }

    public void Dispose()
    {
        foreach (var d in _disposables)
            d.Dispose();
        GC.SuppressFinalize(this);
    }

    // --- GetYankText(DotsiderState) ---

    [Fact]
    public void General_FocusedRef_ReturnsTabSeparatedRow()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.General;
        var firstRef = state.Analyzer.AssemblyRefs[0];
        state.GeneralFocusedDep = firstRef.Name;

        var text = YankHelper.GetYankText(state);

        Assert.NotNull(text);
        Assert.Contains(firstRef.Name, text);
        Assert.Contains(firstRef.Version, text);
        Assert.Contains("\t", text);
    }

    [Fact]
    public void General_NoFocusedRef_ReturnsNull()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.General;
        state.GeneralFocusedDep = null;

        Assert.Null(YankHelper.GetYankText(state));
    }

    [Fact]
    public void PeMetadata_Sections_ReturnsFormattedRow()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.Sections;
        var section = state.Analyzer.Sections[0];
        state.PeFocusedKey = section.Name;

        var text = YankHelper.GetYankText(state);

        Assert.NotNull(text);
        Assert.Contains(section.Name, text);
        Assert.Contains("0x", text);
    }

    [Fact]
    public void PeMetadata_TypeDef_ReturnsFormattedRow()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.TypeDef;
        var typeDef = state.Analyzer.TypeDefs.First(t => !t.FullName.StartsWith("<"));
        state.PeFocusedKey = typeDef.Token;

        var text = YankHelper.GetYankText(state);

        Assert.NotNull(text);
        Assert.Contains(typeDef.FullName, text);
    }

    [Fact]
    public void PeMetadata_MethodDef_ReturnsFormattedRow()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.MethodDef;
        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.PeFocusedKey = method.Token;

        var text = YankHelper.GetYankText(state);

        Assert.NotNull(text);
        Assert.Contains(method.Name, text);
        Assert.Contains(method.DeclaringType, text);
    }

    [Fact]
    public void PeMetadata_TypeRef_ReturnsFormattedRow()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.TypeRef;
        var typeRef = state.Analyzer.TypeRefs[0];
        state.PeFocusedKey = typeRef.Token;

        var text = YankHelper.GetYankText(state);

        Assert.NotNull(text);
        Assert.Contains(typeRef.FullName, text);
    }

    [Fact]
    public void PeMetadata_MemberRef_ReturnsFormattedRow()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.MemberRef;
        var memberRef = state.Analyzer.MemberRefs[0];
        state.PeFocusedKey = memberRef.Token;

        var text = YankHelper.GetYankText(state);

        Assert.NotNull(text);
        Assert.Contains(memberRef.Name, text);
    }

    [Fact]
    public void PeMetadata_Attributes_ReturnsFormattedRow()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.Attributes;
        var attr = state.Analyzer.CustomAttributes[0];
        state.PeFocusedKey = $"{attr.Parent}|{attr.Constructor}";

        var text = YankHelper.GetYankText(state);

        Assert.NotNull(text);
        Assert.Contains(attr.Parent, text);
    }

    [Fact]
    public void PeMetadata_Resources_ReturnsFormattedRow()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.Resources;
        if (state.Analyzer.Resources.Count == 0) return; // some assemblies have none
        var resource = state.Analyzer.Resources[0];
        state.PeFocusedKey = resource.Name;

        var text = YankHelper.GetYankText(state);

        Assert.NotNull(text);
        Assert.Contains(resource.Name, text);
    }

    [Fact]
    public void PeMetadata_NoFocusedKey_ReturnsNull()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeFocusedKey = null;

        Assert.Null(YankHelper.GetYankText(state));
    }

    [Fact]
    public void Strings_FocusedEntry_ReturnsStringValue()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.Strings;
        var strings = state.GetActiveStrings();
        Assert.True(strings.Count > 0);
        var entry = strings[0];
        state.StringsFocusedKey = $"{entry.Offset}:{entry.Source}";

        var text = YankHelper.GetYankText(state);

        Assert.Equal(entry.Value, text);
    }

    [Fact]
    public void DepGraph_SelectedNode_ReturnsNodeName()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.DepGraph;
        state.GraphSelectedNode = "System.Runtime v10.0.0.0";

        Assert.Equal("System.Runtime v10.0.0.0", YankHelper.GetYankText(state));
    }

    [Fact]
    public void DepGraph_SelectedIndex_ReturnsNodeNameWithVersion()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.DepGraph;
        var (nodes, _) = state.CachedGraph ??= DependencyGraphBuilder.Build(state.Analyzer);
        Assert.True(nodes.Count > 0);
        state.GraphSelectedIndex = 0;

        var text = YankHelper.GetYankText(state);

        Assert.NotNull(text);
        Assert.Contains(nodes[0].Name, text);
    }

    [Fact]
    public void SizeMap_SelectedIndex_ReturnsItemWithSize()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.SizeMap;
        state.CachedSizeTree ??= SizeAnalyzer.BuildSizeTree(state.Analyzer);
        var level = state.CachedSizeTree;
        Assert.True(level.Children.Count > 0);
        state.TreemapSelectedIndex = 0;

        var text = YankHelper.GetYankText(state);

        Assert.NotNull(text);
        Assert.Contains(level.Children[0].FullPath, text);
    }

    [Fact]
    public void SizeMap_HoveredItem_ReturnsHoveredText()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.SizeMap;
        state.TreemapHoveredItem = "  RichLibrary.Models: 1.2 KB  ";

        var text = YankHelper.GetYankText(state);

        Assert.Equal("RichLibrary.Models: 1.2 KB", text); // Trimmed
    }

    [Fact]
    public void SizeMap_NoSelection_ReturnsNull()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.SizeMap;
        state.TreemapSelectedIndex = -1;
        state.TreemapHoveredItem = null;

        Assert.Null(YankHelper.GetYankText(state));
    }

    [Fact]
    public void Dynamic_NoTracer_ReturnsNull()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.Dynamic;
        state.Tracer = null;

        Assert.Null(YankHelper.GetYankText(state));
    }

    [Fact]
    public void Dynamic_Events_NoFocusedKey_ReturnsNull()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.Dynamic;
        state.DynamicSubTab = DynamicSubTabId.Events;
        state.DynamicEventsFocusedKey = null;

        Assert.Null(YankHelper.GetYankText(state));
    }

    [Fact]
    public void Dynamic_Output_NoFocusedKey_ReturnsNull()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.Dynamic;
        state.DynamicSubTab = DynamicSubTabId.Output;
        state.DynamicOutputFocusedKey = null;

        Assert.Null(YankHelper.GetYankText(state));
    }

    [Fact]
    public void Dynamic_Counters_ReturnsNull()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.Dynamic;
        state.DynamicSubTab = DynamicSubTabId.Counters;

        Assert.Null(YankHelper.GetYankText(state));
    }

    [Fact]
    public void Dynamic_Summary_ReturnsNull()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.Dynamic;
        state.DynamicSubTab = DynamicSubTabId.Summary;

        Assert.Null(YankHelper.GetYankText(state));
    }

    [Fact]
    public void IlInspector_ReturnsNull()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;

        Assert.Null(YankHelper.GetYankText(state));
    }

    [Fact]
    public void HexDump_ReturnsNull()
    {
        using var state = CreateState(samples.RichLibraryDll);
        state.CurrentTab = TabId.HexDump;

        Assert.Null(YankHelper.GetYankText(state));
    }

    // --- GetYankText(DiffState) ---

    [Fact]
    public void Diff_Types_FocusedRow_ReturnsFormattedText()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var app = new Hex1bApp(_ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("")),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        using var state = new DiffState(app, samples.RichLibraryDll, samples.RichLibraryV2Dll);
        state.CurrentTab = 1; // Types
        var entry = state.DiffResult.TypeDiffs.First(e => e.Kind != DiffKind.Unchanged);
        var type = entry.Right ?? entry.Left!;
        state.DiffFocusedKey = $"{entry.Kind}:{type.FullName}";

        var text = YankHelper.GetYankText(state);

        Assert.NotNull(text);
        Assert.Contains(type.FullName, text);
        Assert.Contains(entry.Kind.ToString(), text);
    }

    [Fact]
    public void Diff_Methods_FocusedRow_ReturnsFormattedText()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var app = new Hex1bApp(_ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("")),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        using var state = new DiffState(app, samples.RichLibraryDll, samples.RichLibraryV2Dll);
        state.CurrentTab = 2; // Methods
        var entry = state.DiffResult.MethodDiffs.First(e => e.Kind != DiffKind.Unchanged);
        var method = entry.Right ?? entry.Left!;
        state.DiffFocusedKey = $"{entry.Kind}:{method.DeclaringType}::{method.Name}{method.Signature}";

        var text = YankHelper.GetYankText(state);

        Assert.NotNull(text);
        Assert.Contains(method.Name, text);
    }

    [Fact]
    public void Diff_Refs_FocusedRow_ReturnsFormattedText()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var app = new Hex1bApp(_ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("")),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        using var state = new DiffState(app, samples.RichLibraryDll, samples.RichLibraryV2Dll);
        state.CurrentTab = 3; // Refs
        var entry = state.DiffResult.AssemblyRefDiffs.First(e => e.Kind != DiffKind.Unchanged);
        var name = entry.Right?.Name ?? entry.Left?.Name ?? "";
        state.DiffFocusedKey = $"{entry.Kind}:{name}";

        var text = YankHelper.GetYankText(state);

        Assert.NotNull(text);
        Assert.Contains(name, text);
    }

    [Fact]
    public void Diff_Summary_ReturnsNull()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var app = new Hex1bApp(_ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("")),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        using var state = new DiffState(app, samples.RichLibraryDll, samples.RichLibraryV2Dll);
        state.CurrentTab = 0; // Summary

        Assert.Null(YankHelper.GetYankText(state));
    }

    [Fact]
    public void Diff_NoFocusedKey_ReturnsNull()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var app = new Hex1bApp(_ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("")),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        using var state = new DiffState(app, samples.RichLibraryDll, samples.RichLibraryV2Dll);
        state.CurrentTab = 1;
        state.DiffFocusedKey = null;

        Assert.Null(YankHelper.GetYankText(state));
    }

    // --- GetHexSelectionText ---

    [Fact]
    public void GetHexSelectionText_ReturnsUppercaseSpaceSeparatedHex()
    {
        using var state = CreateState(samples.RichLibraryDll);
        var hexState = state.HexEditorState;
        var byteMap = hexState.Document.GetByteMap();

        // Select a small range
        var (startChar, _) = byteMap.ByteToChar(0);
        var (endChar, _) = byteMap.ByteToChar(3);
        hexState.Cursor.SelectionAnchor = new DocumentOffset(startChar);
        hexState.Cursor.Position = new DocumentOffset(endChar);

        var result = YankHelper.GetHexSelectionText(hexState);

        Assert.NotNull(result);
        // PE files start with MZ (4D 5A)
        Assert.StartsWith("4D 5A", result);
        // Verify format: uppercase, space-separated
        var parts = result.Split(' ');
        Assert.True(parts.Length >= 4);
        foreach (var part in parts)
        {
            Assert.Equal(2, part.Length);
            Assert.True(part.All(c => char.IsDigit(c) || (c >= 'A' && c <= 'F')));
        }
    }

    [Fact]
    public void GetHexSelectionText_NoSelection_ReturnsNull()
    {
        using var state = CreateState(samples.RichLibraryDll);
        Assert.Null(YankHelper.GetHexSelectionText(state.HexEditorState));
    }

    // --- FindYankProvider ---

    [Fact]
    public void FindYankProvider_DotsiderState_MatchesAllEditors()
    {
        using var state = CreateState(samples.RichLibraryDll);

        // IL editor state is null until a method is selected — create one for testing
        state.IlEditorState = new EditorState(new Hex1bDocument("il test")) { IsReadOnly = true };
        Assert.Same(state.IlYankProvider, YankHelper.FindYankProvider(state, state.IlEditorState));

        state.GeneralInfoEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.Same(state.GeneralInfoYankProvider, YankHelper.FindYankProvider(state, state.GeneralInfoEditorState));

        state.PeHeadersEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.Same(state.PeHeadersYankProvider, YankHelper.FindYankProvider(state, state.PeHeadersEditorState));

        state.ClrHeaderEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.Same(state.ClrHeaderYankProvider, YankHelper.FindYankProvider(state, state.ClrHeaderEditorState));

        state.PeDetailEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.Same(state.PeDetailYankProvider, YankHelper.FindYankProvider(state, state.PeDetailEditorState));

        state.StringsDetailEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.Same(state.StringsDetailYankProvider, YankHelper.FindYankProvider(state, state.StringsDetailEditorState));

        // Unknown editor returns null
        var unknownState = new EditorState(new Hex1bDocument("unknown")) { IsReadOnly = true };
        Assert.Null(YankHelper.FindYankProvider(state, unknownState));
    }

    [Fact]
    public void FindYankProvider_NuGetState_MatchesPackageInfoAndDelegates()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var app = new Hex1bApp(_ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("")),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        using var nugetState = new NuGetState(app, samples.RichLibraryNupkg);

        nugetState.PackageInfoEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.Same(nugetState.PackageInfoYankProvider,
            YankHelper.FindYankProvider(nugetState, nugetState.PackageInfoEditorState));

        // With a selected DLL state, delegates to DotsiderState lookup
        nugetState.SelectedDllState = new DotsiderState(app, samples.RichLibraryDll);
        nugetState.SelectedDllState.GeneralInfoEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.Same(nugetState.SelectedDllState.GeneralInfoYankProvider,
            YankHelper.FindYankProvider(nugetState, nugetState.SelectedDllState.GeneralInfoEditorState));

        // Unknown returns null
        var unknown = new EditorState(new Hex1bDocument("x")) { IsReadOnly = true };
        Assert.Null(YankHelper.FindYankProvider(nugetState, unknown));
    }

    // --- CursorColorHelper ---

    [Fact]
    public void CursorColorHelper_SetSequence_IsOsc12()
    {
        Assert.StartsWith("\x1b]12;", CursorColorHelper.SetTealSequence);
        Assert.Contains("rgb:00/c8/b4", CursorColorHelper.SetTealSequence);
        Assert.EndsWith("\x1b\\", CursorColorHelper.SetTealSequence);
    }

    [Fact]
    public void CursorColorHelper_ResetSequence_IsOsc112()
    {
        Assert.Equal("\x1b]112\x1b\\", CursorColorHelper.ResetSequence);
    }

    [Fact]
    public void CursorColorHelper_ResetCursorColor_WritesToConsole()
    {
        // Capture console output
        var original = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            CursorColorHelper.ResetCursorColor();
            Assert.Equal(CursorColorHelper.ResetSequence, sw.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Fact]
    public void CursorColorHelper_SetThemeCursorColor_WritesToConsole()
    {
        var original = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            CursorColorHelper.SetThemeCursorColor();
            Assert.Equal(CursorColorHelper.SetTealSequence, sw.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
