using Dotsider.Core.Analysis.Models;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Resolves yankable text for table rows and surface nodes.
/// Editor selections are handled directly by the Y key handler — this helper
/// is only called when the focused node is NOT an EditorNode.
/// </summary>
public static class YankHelper
{
    /// <summary>
    /// Returns the yankable text for the current tab's focused table row or surface node,
    /// or null if nothing is yankable.
    /// </summary>
    public static string? GetYankText(DotsiderState state) => state.CurrentTab switch
    {
        TabId.General => GetGeneralYankText(state),
        TabId.PeMetadata => GetPeMetadataYankText(state),
        TabId.Strings => GetStringsYankText(state),
        TabId.DepGraph => GetDepGraphYankText(state),
        TabId.SizeMap => GetSizeTreemapYankText(state),
        TabId.Dynamic => GetDynamicYankText(state),
        _ => null
    };

    /// <summary>
    /// Returns the yankable text for the current diff tab's focused row,
    /// or null if nothing is yankable.
    /// </summary>
    public static string? GetYankText(DiffState state)
    {
        if (state.DiffFocusedKey is not string key) return null;

        return state.CurrentTab switch
        {
            1 => FormatDiffTypesRow(state, key),
            2 => FormatDiffMethodsRow(state, key),
            3 => FormatDiffRefsRow(state, key),
            _ => null
        };
    }

    /// <summary>
    /// Extracts the selected bytes from a hex editor and formats them as
    /// uppercase space-separated hex (e.g., "4D 5A 90 00").
    /// </summary>
    public static string? GetHexSelectionText(EditorState editorState)
    {
        if (!editorState.Cursor.HasSelection) return null;

        var doc = editorState.Document;
        var range = editorState.Cursor.SelectionRange;
        var byteMap = doc.GetByteMap();

        var startByte = byteMap.CharToByteStart(
            Math.Min(range.Start.Value, byteMap.CharCount - 1));
        var endByte = byteMap.CharToByteStart(
            Math.Min(range.End.Value, byteMap.CharCount - 1));

        if (endByte < startByte) (startByte, endByte) = (endByte, startByte);

        var count = endByte - startByte + 1;
        if (count <= 0 || startByte >= doc.ByteCount) return null;
        count = Math.Min(count, doc.ByteCount - startByte);

        var bytes = doc.GetBytes(startByte, count).Span;
        return string.Join(" ", bytes.ToArray().Select(b => b.ToString("X2")));
    }

    /// <summary>
    /// Finds the yank flash decoration provider for the given editor state
    /// within a <see cref="DotsiderState"/>.
    /// </summary>
    public static IlYankDecorationProvider? FindYankProvider(DotsiderState state, EditorState editorState)
    {
        if (editorState == state.IlEditorState) return state.IlYankProvider;
        if (editorState == state.IlPairNativeEditorState) return state.IlPairYankProvider;
        if (editorState == state.GeneralInfoEditorState) return state.GeneralInfoYankProvider;
        if (editorState == state.PeHeadersEditorState) return state.PeHeadersYankProvider;
        if (editorState == state.ClrHeaderEditorState) return state.ClrHeaderYankProvider;
        if (editorState == state.PeDetailEditorState) return state.PeDetailYankProvider;
        if (editorState == state.StringsDetailEditorState) return state.StringsDetailYankProvider;
        if (editorState == state.DynamicCpuEditorState) return state.DynamicCpuYankProvider;
        if (editorState == state.DynamicMemoryEditorState) return state.DynamicMemoryYankProvider;
        if (editorState == state.DynamicGcEditorState) return state.DynamicGcYankProvider;
        if (editorState == state.DynamicThreadingEditorState) return state.DynamicThreadingYankProvider;
        if (editorState == state.DynamicSummaryEditorState) return state.DynamicSummaryYankProvider;
        if (editorState == state.DataInterpEditorState) return state.DataInterpYankProvider;
        return null;
    }

    /// <summary>
    /// Finds the yank flash decoration provider for the given editor state
    /// within a <see cref="NuGetState"/>.
    /// </summary>
    public static IlYankDecorationProvider? FindYankProvider(NuGetState state, EditorState editorState)
    {
        if (editorState == state.PackageInfoEditorState) return state.PackageInfoYankProvider;
        if (state.SelectedDllState is not null)
            return FindYankProvider(state.SelectedDllState, editorState);
        return null;
    }

    private static string? GetGeneralYankText(DotsiderState state)
    {
        if (state.GeneralFocusedDep is not string name) return null;
        var asmRef = state.MetadataAnalyzer.AssemblyRefs.FirstOrDefault(r => r.Name == name);
        if (asmRef is null) return null;
        return $"{asmRef.Name}\t{asmRef.Version}\t{asmRef.Culture}\t{asmRef.PublicKeyToken}";
    }

    private static string? GetPeMetadataYankText(DotsiderState state)
    {
        if (state.PeFocusedKey is null) return null;
        // Yank answers from the same analyzer the table rendered from.
        var analyzer = PeMetadataRouting.AnalyzerForPeSubTab(state, state.PeSubTab);

        return state.PeSubTab switch
        {
            PeSubTabId.Sections => FormatSection(analyzer.Sections, state.PeFocusedKey),
            PeSubTabId.TypeDef => FormatTypeDef(analyzer.TypeDefs, state.PeFocusedKey),
            PeSubTabId.MethodDef => FormatMethodDef(analyzer.MethodDefs, state.PeFocusedKey),
            PeSubTabId.TypeRef => FormatTypeRef(analyzer.TypeRefs, state.PeFocusedKey),
            PeSubTabId.MemberRef => FormatMemberRef(analyzer.MemberRefs, state.PeFocusedKey),
            PeSubTabId.Attributes => FormatAttribute(analyzer.CustomAttributes, state.PeFocusedKey),
            PeSubTabId.Resources => FormatResource(analyzer.Resources, state.PeFocusedKey),
            PeSubTabId.DebugDirectory => FormatDebugDirectoryRow(state),
            _ => null
        };
    }

    private static string? FormatDebugDirectoryRow(DotsiderState state)
    {
        if (state.PeFocusedKey is not string key) return null;
        var rows = PeMetadataView.GetDebugDirectoryRows(state);
        var idx = -1;
        for (var i = 0; i < rows.Count; i++)
        {
            if (PeMetadataView.GetDebugDirectoryRowKey(rows[i]) == key) { idx = i; break; }
        }

        if (idx < 0) return null;
        var row = rows[idx];
        var entry = row.Info;
        return $"{row.Origin}\t{entry.Type}\t0x{entry.Stamp:X8}\t{entry.MajorVersion}\t{entry.MinorVersion}\t{entry.DataSize}\t"
            + $"0x{entry.AddressOfRawData:X8}\t0x{entry.PointerToRawData:X8}\t{entry.Payload}";
    }

    private static string? GetStringsYankText(DotsiderState state)
    {
        if (state.StringsFocusedKey is not string key) return null;
        var strings = state.GetActiveStrings();
        var entry = strings.FirstOrDefault(e => $"{e.Offset}:{e.Source}" == key);
        return entry?.Value;
    }

    private static string? GetDepGraphYankText(DotsiderState state)
    {
        if (state.GraphSelectedNode is not null)
            return state.GraphSelectedNode;

        if (state.GraphSelectedIndex < 0 || state.CachedGraph is not { } graph)
            return null;

        // Selection indices and the rendered label both come from the visible model the view
        // computes each frame, so yank must use the same projection — otherwise a filtered
        // view with selection index 1 would yank the underlying cached node at index 1,
        // which is a hidden framework assembly, not the assembly the user sees.
        var visible = DependencyGraphView.BuildVisibleModel(
            graph.Nodes, graph.Edges, state.GraphNavigation,
            state.DepGraphScope, state.DepGraphHideFramework);

        if (state.GraphSelectedIndex >= visible.Nodes.Count)
            return null;

        var node = visible.Nodes[state.GraphSelectedIndex];
        var disambig = DependencyGraphView.ComputeDisambiguation(visible.Nodes);
        return DependencyGraphView.FormatLabel(node, disambig);
    }

    private static string? GetSizeTreemapYankText(DotsiderState state)
    {
        if (state.TreemapHoveredItem is not null)
            return state.TreemapHoveredItem.Trim();

        var currentLevel = state.TreemapCurrentLevel ?? state.CachedSizeTree;
        if (currentLevel is null) return null;

        if (state.TreemapSelectedIndex >= 0
            && state.TreemapSelectedIndex < currentLevel.Children.Count)
        {
            var child = currentLevel.Children[state.TreemapSelectedIndex];
            return $"{child.FullPath}: {DotsiderState.FormatSize(child.Size)}";
        }

        return null;
    }

    private static string? GetDynamicYankText(DotsiderState state)
    {
        if (state.Tracer is null) return null;

        return state.DynamicSubTab switch
        {
            DynamicSubTabId.Events => GetDynamicEventYankText(state),
            DynamicSubTabId.Output => GetDynamicOutputYankText(state),
            _ => null
        };
    }

    private static string? GetDynamicEventYankText(DotsiderState state)
    {
        if (state.DynamicEventsFocusedKey is not string focusedKey) return null;
        var events = state.Tracer!.GetEvents();

        var evt = events.FirstOrDefault(e =>
            $"{e.Timestamp.Ticks}:{e.EventName}:{e.Detail}:{e.MetadataToken}" == focusedKey);
        if (evt is null) return null;

        return $"{evt.Timestamp:mm\\:ss\\.fff}\t{evt.Category}\t{evt.EventName}\t{evt.Detail}";
    }

    private static string? GetDynamicOutputYankText(DotsiderState state)
    {
        if (state.DynamicOutputFocusedKey is not string focusedKey) return null;
        var output = state.Tracer!.GetOutput();

        var line = output.FirstOrDefault(o =>
            $"{o.Timestamp.Ticks}:{o.Text}" == focusedKey);
        return line?.Text;
    }

    private static string? FormatSection(IReadOnlyList<SectionInfo> sections, object key)
    {
        var s = sections.FirstOrDefault(x => (object)x.Name == key || x.Name.Equals(key));
        return s is null ? null
            : $"{s.Name}\t0x{s.VirtualAddress:X8}\t{s.VirtualSize}\t0x{s.RawDataOffset:X8}\t{s.RawDataSize}\t{s.Characteristics}";
    }

    private static string? FormatTypeDef(IReadOnlyList<TypeDefInfo> types, object key)
    {
        if (key is not int token) return null;
        var t = types.FirstOrDefault(x => x.Token == token);
        return t is null ? null
            : $"0x{t.Token:X8}\t{t.FullName}\t{t.BaseType}\t{t.Attributes}\t{t.MethodCount}\t{t.FieldCount}";
    }

    private static string? FormatMethodDef(IReadOnlyList<MethodDefInfo> methods, object key)
    {
        if (key is not int token) return null;
        var m = methods.FirstOrDefault(x => x.Token == token);
        return m is null ? null
            : $"0x{m.Token:X8}\t{m.DeclaringType}\t{m.Name}\t{m.Signature}\t{m.Attributes}\t0x{m.Rva:X8}";
    }

    private static string? FormatTypeRef(IReadOnlyList<TypeRefInfo> types, object key)
    {
        if (key is not int token) return null;
        var t = types.FirstOrDefault(x => x.Token == token);
        return t is null ? null
            : $"0x{t.Token:X8}\t{t.FullName}\t{t.ResolutionScope}";
    }

    private static string? FormatMemberRef(IReadOnlyList<MemberRefInfo> members, object key)
    {
        if (key is not int token) return null;
        var m = members.FirstOrDefault(x => x.Token == token);
        return m is null ? null
            : $"0x{m.Token:X8}\t{m.DeclaringType}\t{m.Name}";
    }

    private static string? FormatAttribute(IReadOnlyList<CustomAttributeInfo> attrs, object key)
    {
        if (key is not string compositeKey) return null;
        var a = attrs.FirstOrDefault(x => $"{x.Parent}|{x.Constructor}" == compositeKey);
        return a is null ? null
            : $"{a.Parent}\t{a.Constructor}\t{a.Value}";
    }

    private static string? FormatResource(IReadOnlyList<ResourceInfo> resources, object key)
    {
        var r = resources.FirstOrDefault(x => (object)x.Name == key || x.Name.Equals(key));
        return r is null ? null
            : $"{r.Name}\t{r.Visibility}\t0x{r.Offset:X8}\t{r.Size}\t{(r.IsLinked ? "Yes" : "No")}";
    }


    private static string? FormatDiffTypesRow(DiffState state, string key)
    {
        var entry = state.DiffResult.TypeDiffs.FirstOrDefault(e =>
            $"{e.Kind}:{e.Left?.FullName ?? e.Right?.FullName ?? ""}" == key);
        if (entry is null) return null;
        var type = entry.Right ?? entry.Left!;
        return $"{entry.Kind}\t{type.FullName}\t{type.BaseType}\t{type.MethodCount}\t{type.FieldCount}\t{entry.ChangeDescription}";
    }

    private static string? FormatDiffMethodsRow(DiffState state, string key)
    {
        var entry = state.DiffResult.MethodDiffs.FirstOrDefault(e =>
        {
            var m = e.Right ?? e.Left!;
            return $"{e.Kind}:{m.DeclaringType}::{m.Name}{m.Signature}" == key;
        });
        if (entry is null) return null;
        var method = entry.Right ?? entry.Left!;
        return $"{entry.Kind}\t{method.DeclaringType}\t{method.Name}\t{method.Signature}\t{entry.ChangeDescription}";
    }

    private static string? FormatDiffRefsRow(DiffState state, string key)
    {
        var entry = state.DiffResult.AssemblyRefDiffs.FirstOrDefault(e =>
            $"{e.Kind}:{e.Left?.Name ?? e.Right?.Name ?? ""}" == key);
        if (entry is null) return null;
        var leftVersion = entry.Left?.Version ?? "";
        var rightVersion = entry.Right?.Version ?? "";
        var name = entry.Right?.Name ?? entry.Left?.Name ?? "";
        return $"{entry.Kind}\t{name}\t{leftVersion}\t{rightVersion}\t{entry.ChangeDescription}";
    }
}
