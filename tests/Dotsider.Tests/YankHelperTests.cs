using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Infrastructure;
using Dotsider.Views;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Yank Helper.
/// </summary>
[TestClass]
public class YankHelperTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

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

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var d in _disposables)
            d.Dispose();
    }

    // --- GetYankText(DotsiderState) ---

    /// <summary>
    /// Verifies general focused ref returns tab separated row.
    /// </summary>
    [TestMethod]
    public void General_FocusedRef_ReturnsTabSeparatedRow()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.General;
        var firstRef = state.Analyzer.AssemblyRefs[0];
        state.GeneralFocusedDep = firstRef.Name;

        var text = YankHelper.GetYankText(state);

        Assert.IsNotNull(text);
        Assert.Contains(firstRef.Name, text);
        Assert.Contains(firstRef.Version, text);
        Assert.Contains("\t", text);
    }

    /// <summary>
    /// Verifies general no focused ref returns null.
    /// </summary>
    [TestMethod]
    public void General_NoFocusedRef_ReturnsNull()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.General;
        state.GeneralFocusedDep = null;

        Assert.IsNull(YankHelper.GetYankText(state));
    }

    /// <summary>
    /// Verifies pe metadata sections returns formatted row.
    /// </summary>
    [TestMethod]
    public void PeMetadata_Sections_ReturnsFormattedRow()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.Sections;
        var section = state.Analyzer.Sections[0];
        state.PeFocusedKey = section.Name;

        var text = YankHelper.GetYankText(state);

        Assert.IsNotNull(text);
        Assert.Contains(section.Name, text);
        Assert.Contains("0x", text);
    }

    /// <summary>
    /// Verifies pe metadata type def returns formatted row.
    /// </summary>
    [TestMethod]
    public void PeMetadata_TypeDef_ReturnsFormattedRow()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.TypeDef;
        var typeDef = state.Analyzer.TypeDefs.First(t => !t.FullName.StartsWith('<'));
        state.PeFocusedKey = typeDef.Token;

        var text = YankHelper.GetYankText(state);

        Assert.IsNotNull(text);
        Assert.Contains(typeDef.FullName, text);
    }

    /// <summary>
    /// Verifies pe metadata method def returns formatted row.
    /// </summary>
    [TestMethod]
    public void PeMetadata_MethodDef_ReturnsFormattedRow()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.MethodDef;
        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.PeFocusedKey = method.Token;

        var text = YankHelper.GetYankText(state);

        Assert.IsNotNull(text);
        Assert.Contains(method.Name, text);
        Assert.Contains(method.DeclaringType, text);
    }

    /// <summary>
    /// Verifies pe metadata type ref returns formatted row.
    /// </summary>
    [TestMethod]
    public void PeMetadata_TypeRef_ReturnsFormattedRow()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.TypeRef;
        var typeRef = state.Analyzer.TypeRefs[0];
        state.PeFocusedKey = typeRef.Token;

        var text = YankHelper.GetYankText(state);

        Assert.IsNotNull(text);
        Assert.Contains(typeRef.FullName, text);
    }

    /// <summary>
    /// Verifies pe metadata member ref returns formatted row.
    /// </summary>
    [TestMethod]
    public void PeMetadata_MemberRef_ReturnsFormattedRow()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.MemberRef;
        var memberRef = state.Analyzer.MemberRefs[0];
        state.PeFocusedKey = memberRef.Token;

        var text = YankHelper.GetYankText(state);

        Assert.IsNotNull(text);
        Assert.Contains(memberRef.Name, text);
    }

    /// <summary>
    /// Verifies pe metadata attributes returns formatted row.
    /// </summary>
    [TestMethod]
    public void PeMetadata_Attributes_ReturnsFormattedRow()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.Attributes;
        var attr = state.Analyzer.CustomAttributes[0];
        state.PeFocusedKey = $"{attr.Parent}|{attr.Constructor}";

        var text = YankHelper.GetYankText(state);

        Assert.IsNotNull(text);
        Assert.Contains(attr.Parent, text);
    }

    /// <summary>
    /// Verifies pe metadata resources returns formatted row.
    /// </summary>
    [TestMethod]
    public void PeMetadata_Resources_ReturnsFormattedRow()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.Resources;
        if (state.Analyzer.Resources.Count == 0) return; // some assemblies have none
        var resource = state.Analyzer.Resources[0];
        state.PeFocusedKey = resource.Name;

        var text = YankHelper.GetYankText(state);

        Assert.IsNotNull(text);
        Assert.Contains(resource.Name, text);
    }

    /// <summary>
    /// Verifies pe metadata no focused key returns null.
    /// </summary>
    [TestMethod]
    public void PeMetadata_NoFocusedKey_ReturnsNull()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.PeMetadata;
        state.PeFocusedKey = null;

        Assert.IsNull(YankHelper.GetYankText(state));
    }

    /// <summary>
    /// Verifies strings focused entry returns string value.
    /// </summary>
    [TestMethod]
    public void Strings_FocusedEntry_ReturnsStringValue()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.Strings;
        var strings = state.GetActiveStrings();
        Assert.IsGreaterThan(0, strings.Count);
        var entry = strings[0];
        state.StringsFocusedKey = $"{entry.Offset}:{entry.Source}";

        var text = YankHelper.GetYankText(state);

        Assert.AreEqual(entry.Value, text);
    }

    /// <summary>
    /// Verifies dep graph selected node returns node name.
    /// </summary>
    [TestMethod]
    public void DepGraph_SelectedNode_ReturnsNodeName()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.DepGraph;
        state.GraphSelectedNode = "System.Runtime v10.0.0.0";

        Assert.AreEqual("System.Runtime v10.0.0.0", YankHelper.GetYankText(state));
    }

    /// <summary>
    /// Verifies dep graph selected index returns node name with version.
    /// </summary>
    [TestMethod]
    public void DepGraph_SelectedIndex_ReturnsNodeNameWithVersion()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.DepGraph;
        if (state.CachedGraph is null)
        {
            var result = DependencyGraphBuilder.Build(state.Analyzer);
            state.CachedGraph = (result.Nodes, result.Edges);
            state.GraphNavigation = result.NavigationById;
        }
        var nodes = state.CachedGraph!.Value.Nodes;
        Assert.IsGreaterThan(0, nodes.Count);
        state.GraphSelectedIndex = 0;

        var text = YankHelper.GetYankText(state);

        Assert.IsNotNull(text);
        Assert.Contains(nodes[0].Name, text);
    }

    /// <summary>
    /// Under <see cref="Views.DependencyGraphScope.DirectOnly"/>, yank at selection index 0
    /// returns the first visible node (the root), not whatever node sits at index 0 in the
    /// underlying cached graph. The selected-index-to-node mapping must follow the same
    /// visible projection the view uses.
    /// </summary>
    [TestMethod]
    public void DepGraph_Yank_UsesVisibleModel_UnderDirectOnlyScope()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.DepGraph;
        var result = DependencyGraphBuilder.Build(state.Analyzer);
        state.CachedGraph = (result.Nodes, result.Edges);
        state.GraphNavigation = result.NavigationById;

        state.DepGraphScope = Views.DependencyGraphScope.DirectOnly;

        var visible = Views.DependencyGraphView.BuildVisibleModel(
            result.Nodes, result.Edges, result.NavigationById,
            state.DepGraphScope, state.DepGraphHideFramework);
        Assert.IsGreaterThanOrEqualTo(2, visible.Nodes.Count, "RichLibrary should have root plus at least one direct ref");

        state.GraphSelectedIndex = 1;
        var text = YankHelper.GetYankText(state);

        Assert.IsNotNull(text);
        Assert.Contains(visible.Nodes[1].Name, text);
    }

    /// <summary>
    /// Verifies size map selected index returns item with size.
    /// </summary>
    [TestMethod]
    public void SizeMap_SelectedIndex_ReturnsItemWithSize()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.SizeMap;
        state.CachedSizeTree ??= SizeAnalyzer.BuildSizeTree(state.Analyzer);
        var level = state.CachedSizeTree;
        Assert.IsGreaterThan(0, level.Children.Count);
        state.TreemapSelectedIndex = 0;

        var text = YankHelper.GetYankText(state);

        Assert.IsNotNull(text);
        Assert.Contains(level.Children[0].FullPath, text);
    }

    /// <summary>
    /// Verifies size map hovered item returns hovered text.
    /// </summary>
    [TestMethod]
    public void SizeMap_HoveredItem_ReturnsHoveredText()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.SizeMap;
        state.TreemapHoveredItem = "  RichLibrary.Models: 1.2 KB  ";

        var text = YankHelper.GetYankText(state);

        Assert.AreEqual("RichLibrary.Models: 1.2 KB", text); // Trimmed
    }

    /// <summary>
    /// Verifies size map no selection returns null.
    /// </summary>
    [TestMethod]
    public void SizeMap_NoSelection_ReturnsNull()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.SizeMap;
        state.TreemapSelectedIndex = -1;
        state.TreemapHoveredItem = null;

        Assert.IsNull(YankHelper.GetYankText(state));
    }

    /// <summary>
    /// Verifies dynamic no tracer returns null.
    /// </summary>
    [TestMethod]
    public void Dynamic_NoTracer_ReturnsNull()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.Dynamic;
        state.Tracer = null;

        Assert.IsNull(YankHelper.GetYankText(state));
    }

    /// <summary>
    /// Verifies dynamic events no focused key returns null.
    /// </summary>
    [TestMethod]
    public void Dynamic_Events_NoFocusedKey_ReturnsNull()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.Dynamic;
        state.DynamicSubTab = DynamicSubTabId.Events;
        state.DynamicEventsFocusedKey = null;

        Assert.IsNull(YankHelper.GetYankText(state));
    }

    /// <summary>
    /// Verifies dynamic output no focused key returns null.
    /// </summary>
    [TestMethod]
    public void Dynamic_Output_NoFocusedKey_ReturnsNull()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.Dynamic;
        state.DynamicSubTab = DynamicSubTabId.Output;
        state.DynamicOutputFocusedKey = null;

        Assert.IsNull(YankHelper.GetYankText(state));
    }

    /// <summary>
    /// Verifies dynamic counters returns null.
    /// </summary>
    [TestMethod]
    public void Dynamic_Counters_ReturnsNull()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.Dynamic;
        state.DynamicSubTab = DynamicSubTabId.Counters;

        Assert.IsNull(YankHelper.GetYankText(state));
    }

    /// <summary>
    /// Verifies dynamic summary returns null.
    /// </summary>
    [TestMethod]
    public void Dynamic_Summary_ReturnsNull()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.Dynamic;
        state.DynamicSubTab = DynamicSubTabId.Summary;

        Assert.IsNull(YankHelper.GetYankText(state));
    }

    /// <summary>
    /// Verifies il inspector returns null.
    /// </summary>
    [TestMethod]
    public void IlInspector_ReturnsNull()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;

        Assert.IsNull(YankHelper.GetYankText(state));
    }

    /// <summary>
    /// Verifies hex dump returns null.
    /// </summary>
    [TestMethod]
    public void HexDump_ReturnsNull()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        state.CurrentTab = TabId.HexDump;

        Assert.IsNull(YankHelper.GetYankText(state));
    }

    // --- GetYankText(DiffState) ---

    /// <summary>
    /// Verifies diff types focused row returns formatted text.
    /// </summary>
    [TestMethod]
    public void Diff_Types_FocusedRow_ReturnsFormattedText()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var app = new Hex1bApp(_ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("")),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        using var state = new DiffState(app, Samples.RichLibraryDll, Samples.RichLibraryV2Dll);
        state.CurrentTab = 1; // Types
        var entry = state.DiffResult.TypeDiffs.First(e => e.Kind != DiffKind.Unchanged);
        var type = entry.Right ?? entry.Left!;
        state.DiffFocusedKey = $"{entry.Kind}:{type.FullName}";

        var text = YankHelper.GetYankText(state);

        Assert.IsNotNull(text);
        Assert.Contains(type.FullName, text);
        Assert.Contains(entry.Kind.ToString(), text);
    }

    /// <summary>
    /// Verifies diff methods focused row returns formatted text.
    /// </summary>
    [TestMethod]
    public void Diff_Methods_FocusedRow_ReturnsFormattedText()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var app = new Hex1bApp(_ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("")),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        using var state = new DiffState(app, Samples.RichLibraryDll, Samples.RichLibraryV2Dll);
        state.CurrentTab = 2; // Methods
        var entry = state.DiffResult.MethodDiffs.First(e => e.Kind != DiffKind.Unchanged);
        var method = entry.Right ?? entry.Left!;
        state.DiffFocusedKey = $"{entry.Kind}:{method.DeclaringType}::{method.Name}{method.Signature}";

        var text = YankHelper.GetYankText(state);

        Assert.IsNotNull(text);
        Assert.Contains(method.Name, text);
    }

    /// <summary>
    /// Verifies diff refs focused row returns formatted text.
    /// </summary>
    [TestMethod]
    public void Diff_Refs_FocusedRow_ReturnsFormattedText()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var app = new Hex1bApp(_ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("")),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        using var state = new DiffState(app, Samples.RichLibraryDll, Samples.RichLibraryV2Dll);
        state.CurrentTab = 3; // Refs
        var entry = state.DiffResult.AssemblyRefDiffs.First(e => e.Kind != DiffKind.Unchanged);
        var name = entry.Right?.Name ?? entry.Left?.Name ?? "";
        state.DiffFocusedKey = $"{entry.Kind}:{name}";

        var text = YankHelper.GetYankText(state);

        Assert.IsNotNull(text);
        Assert.Contains(name, text);
    }

    /// <summary>
    /// Verifies diff summary returns null.
    /// </summary>
    [TestMethod]
    public void Diff_Summary_ReturnsNull()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var app = new Hex1bApp(_ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("")),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        using var state = new DiffState(app, Samples.RichLibraryDll, Samples.RichLibraryV2Dll);
        state.CurrentTab = 0; // Summary

        Assert.IsNull(YankHelper.GetYankText(state));
    }

    /// <summary>
    /// Verifies diff no focused key returns null.
    /// </summary>
    [TestMethod]
    public void Diff_NoFocusedKey_ReturnsNull()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var app = new Hex1bApp(_ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("")),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        using var state = new DiffState(app, Samples.RichLibraryDll, Samples.RichLibraryV2Dll);
        state.CurrentTab = 1;
        state.DiffFocusedKey = null;

        Assert.IsNull(YankHelper.GetYankText(state));
    }

    // --- GetHexSelectionText ---

    /// <summary>
    /// Verifies get hex selection text returns uppercase space separated hex.
    /// </summary>
    [TestMethod]
    public void GetHexSelectionText_ReturnsUppercaseSpaceSeparatedHex()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        var hexState = state.HexEditorState;
        var byteMap = hexState.Document.GetByteMap();

        // Select a small range
        var (startChar, _) = byteMap.ByteToChar(0);
        var (endChar, _) = byteMap.ByteToChar(3);
        hexState.Cursor.SelectionAnchor = new DocumentOffset(startChar);
        hexState.Cursor.Position = new DocumentOffset(endChar);

        var result = YankHelper.GetHexSelectionText(hexState);

        Assert.IsNotNull(result);
        // PE files start with MZ (4D 5A)
        Assert.StartsWith("4D 5A", result);
        // Verify format: uppercase, space-separated
        var parts = result.Split(' ');
        Assert.IsGreaterThanOrEqualTo(4, parts.Length);
        foreach (var part in parts)
        {
            Assert.HasCount(2, part);
            Assert.IsTrue(part.All(c => char.IsDigit(c) || (c >= 'A' && c <= 'F')));
        }
    }

    /// <summary>
    /// Verifies get hex selection text no selection returns null.
    /// </summary>
    [TestMethod]
    public void GetHexSelectionText_NoSelection_ReturnsNull()
    {
        using var state = CreateState(Samples.RichLibraryDll);
        Assert.IsNull(YankHelper.GetHexSelectionText(state.HexEditorState));
    }

    // --- FindYankProvider ---

    /// <summary>
    /// Verifies find yank provider dotsider state matches all editors.
    /// </summary>
    [TestMethod]
    public void FindYankProvider_DotsiderState_MatchesAllEditors()
    {
        using var state = CreateState(Samples.RichLibraryDll);

        // IL editor state is null until a method is selected — create one for testing
        state.IlEditorState = new EditorState(new Hex1bDocument("il test")) { IsReadOnly = true };
        Assert.AreSame(state.IlYankProvider, YankHelper.FindYankProvider(state, state.IlEditorState));

        state.GeneralInfoEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.AreSame(state.GeneralInfoYankProvider, YankHelper.FindYankProvider(state, state.GeneralInfoEditorState));

        state.PeHeadersEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.AreSame(state.PeHeadersYankProvider, YankHelper.FindYankProvider(state, state.PeHeadersEditorState));

        state.ClrHeaderEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.AreSame(state.ClrHeaderYankProvider, YankHelper.FindYankProvider(state, state.ClrHeaderEditorState));

        state.PeDetailEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.AreSame(state.PeDetailYankProvider, YankHelper.FindYankProvider(state, state.PeDetailEditorState));

        state.StringsDetailEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.AreSame(state.StringsDetailYankProvider, YankHelper.FindYankProvider(state, state.StringsDetailEditorState));

        state.DynamicCpuEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.AreSame(state.DynamicCpuYankProvider, YankHelper.FindYankProvider(state, state.DynamicCpuEditorState));

        state.DynamicMemoryEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.AreSame(state.DynamicMemoryYankProvider, YankHelper.FindYankProvider(state, state.DynamicMemoryEditorState));

        state.DynamicGcEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.AreSame(state.DynamicGcYankProvider, YankHelper.FindYankProvider(state, state.DynamicGcEditorState));

        state.DynamicThreadingEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.AreSame(state.DynamicThreadingYankProvider, YankHelper.FindYankProvider(state, state.DynamicThreadingEditorState));

        state.DynamicSummaryEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.AreSame(state.DynamicSummaryYankProvider, YankHelper.FindYankProvider(state, state.DynamicSummaryEditorState));

        state.DataInterpEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.AreSame(state.DataInterpYankProvider, YankHelper.FindYankProvider(state, state.DataInterpEditorState));

        // Unknown editor returns null
        var unknownState = new EditorState(new Hex1bDocument("unknown")) { IsReadOnly = true };
        Assert.IsNull(YankHelper.FindYankProvider(state, unknownState));
    }

    /// <summary>
    /// Verifies find yank provider nu get state matches package info and delegates.
    /// </summary>
    [TestMethod]
    public void FindYankProvider_NuGetState_MatchesPackageInfoAndDelegates()
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var app = new Hex1bApp(_ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("")),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        using var nugetState = new NuGetState(app, Samples.RichLibraryNupkg);

        nugetState.PackageInfoEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        Assert.AreSame(nugetState.PackageInfoYankProvider,
            YankHelper.FindYankProvider(nugetState, nugetState.PackageInfoEditorState));

        // With a selected DLL state, delegates to DotsiderState lookup
        nugetState.SelectedDllState = new DotsiderState(app, Samples.RichLibraryDll)
        {
            GeneralInfoEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true }
        };
        Assert.AreSame(nugetState.SelectedDllState.GeneralInfoYankProvider,
            YankHelper.FindYankProvider(nugetState, nugetState.SelectedDllState.GeneralInfoEditorState));

        // Unknown returns null
        var unknown = new EditorState(new Hex1bDocument("x")) { IsReadOnly = true };
        Assert.IsNull(YankHelper.FindYankProvider(nugetState, unknown));
    }

    // --- CursorColorHelper ---

    /// <summary>
    /// Verifies cursor color helper set sequence is osc12.
    /// </summary>
    [TestMethod]
    public void CursorColorHelper_SetSequence_IsOsc12()
    {
        Assert.StartsWith("\x1b]12;", CursorColorHelper.SetTealSequence);
        Assert.Contains("rgb:00/c8/b4", CursorColorHelper.SetTealSequence);
        Assert.EndsWith("\x1b\\", CursorColorHelper.SetTealSequence);
    }

    /// <summary>
    /// Verifies cursor color helper reset cursor color writes to console.
    /// </summary>
    [TestMethod]
    public void CursorColorHelper_ResetCursorColor_WritesToConsole()
    {
        using var sw = new StringWriter();
        CursorColorHelper.ResetCursorColor(sw);
        Assert.AreEqual(CursorColorHelper.ResetSequence, sw.ToString());
    }

    /// <summary>
    /// Verifies cursor color helper set theme cursor color writes to console.
    /// </summary>
    [TestMethod]
    public void CursorColorHelper_SetThemeCursorColor_WritesToConsole()
    {
        using var sw = new StringWriter();
        CursorColorHelper.SetThemeCursorColor(sw);
        Assert.AreEqual(CursorColorHelper.SetTealSequence, sw.ToString());
    }
}
