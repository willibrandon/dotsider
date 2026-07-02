using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the PE/Metadata tab (Tab 1), showing PE headers, CLR header,
/// and sub-tabbed metadata tables for sections, types, methods, and more.
/// </summary>
public static class PeMetadataView
{
    private static readonly Hex1bColor AddressColor = Hex1bColor.FromRgb(100, 100, 130);

    /// <summary>Set per-frame to enable yank flash on the focused table row.</summary>
    [ThreadStatic] private static bool s_yankFlash;

    /// <summary>
    /// Builds the PE/Metadata view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context for building widgets.</param>
    /// <param name="state">The shared application state.</param>
    /// <returns>The root widget for the PE/Metadata tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        s_yankFlash = state.YankFlashRow;
        var analyzer = state.Analyzer;
        var search = state.Search[TabId.PeMetadata];

        // Set up match navigation — cycle through active sub-tab's filtered rows
        if (state.CurrentTab == TabId.PeMetadata)
        {
            var rowKeys = GetActiveRowKeys(state);
            if (rowKeys.Count > 0)
            {
                state.NavigateNextMatch = () =>
                {
                    var idx = FindKeyIndex(rowKeys, state.PeFocusedKey);
                    idx = (idx + 1) % rowKeys.Count;
                    state.PeFocusedKey = rowKeys[idx];
                };
                state.NavigatePrevMatch = () =>
                {
                    var idx = FindKeyIndex(rowKeys, state.PeFocusedKey);
                    idx = idx <= 0 ? rowKeys.Count - 1 : idx - 1;
                    state.PeFocusedKey = rowKeys[idx];
                };
            }
            else
            {
                state.NavigateNextMatch = null;
                state.NavigatePrevMatch = null;
            }

            // Ensure the first row is focused when arriving at a sub-tab
            state.PeFocusedKey ??= state.PeSubTab switch
                {
                    PeSubTabId.Sections when analyzer.Sections.Count > 0 => analyzer.Sections[0].Name,
                    PeSubTabId.TypeDef when analyzer.TypeDefs.Count > 0 => analyzer.TypeDefs[0].Token,
                    PeSubTabId.MethodDef when analyzer.MethodDefs.Count > 0 => analyzer.MethodDefs[0].Token,
                    PeSubTabId.TypeRef when analyzer.TypeRefs.Count > 0 => analyzer.TypeRefs[0].Token,
                    PeSubTabId.MemberRef when analyzer.MemberRefs.Count > 0 => analyzer.MemberRefs[0].Token,
                    PeSubTabId.Attributes when analyzer.CustomAttributes.Count > 0 =>
                        $"{analyzer.CustomAttributes[0].Parent}|{analyzer.CustomAttributes[0].Constructor}",
                    PeSubTabId.Resources when analyzer.Resources.Count > 0 => analyzer.Resources[0].Name,
                    PeSubTabId.DebugDirectory when analyzer.DebugDirectory.Count > 0 =>
                        GetDebugDirectoryKey(analyzer.DebugDirectory[0]),
                    PeSubTabId.Imports when GetImportRows(analyzer).Count > 0 =>
                        GetImportRows(analyzer)[0].Key,
                    PeSubTabId.Exports when analyzer.Exports.Count > 0 =>
                        analyzer.Exports[0].Ordinal,
                    PeSubTabId.LoadConfig when analyzer.LoadConfig is not null =>
                        GetLoadConfigRows(analyzer.LoadConfig)[0].Field,
                    PeSubTabId.RtrSections when analyzer.ReadyToRunSections.Count > 0 =>
                        analyzer.ReadyToRunSections[0].SectionId,
                    PeSubTabId.AotTypes when analyzer.RecoveredTypes.Count > 0 =>
                        analyzer.RecoveredTypes[0].FullName,
                    _ => null
                };
        }

        // Build PE Headers text for read-only editor
        var peText = analyzer.PeHeaders is { } pe
            ? string.Join("\n",
                $"  Machine:            {pe.Machine}",
                $"  Magic:              {pe.Magic}",
                $"  Characteristics:    {pe.Characteristics}",
                $"  Timestamp:          0x{pe.TimeDateStamp:X8}",
                $"  Linker Version:     {pe.MajorLinkerVersion}.{pe.MinorLinkerVersion}",
                $"  Size of Code:       {FormatSize(pe.SizeOfCode, state)}",
                $"  Entry Point RVA:    0x{pe.EntryPointRva:X8}",
                $"  Image Base:         0x{pe.ImageBase:X16}",
                $"  Section Alignment:  {FormatSize(pe.SectionAlignment, state)}",
                $"  File Alignment:     {FormatSize(pe.FileAlignment, state)}",
                $"  Size of Image:      {FormatSize(pe.SizeOfImage, state)}",
                $"  Size of Headers:    {FormatSize(pe.SizeOfHeaders, state)}",
                $"  Subsystem:          {pe.Subsystem}",
                $"  DLL Characteristics:{pe.DllCharacteristics}",
                $"  Number of Sections: {pe.NumberOfSections}")
            : "  No PE headers available";

        if (state.PeHeadersEditorText != peText)
        {
            state.PeHeadersEditorText = peText;
            state.PeHeadersEditorState = new EditorState(new Hex1bDocument(peText)) { IsReadOnly = true };
        }

        // Build CLR Header text for read-only editor
        var clrText = analyzer.ClrHeader is { } clr
            ? string.Join("\n",
                $"  Runtime Version:    {clr.MajorRuntimeVersion}.{clr.MinorRuntimeVersion}",
                $"  Metadata RVA:       0x{clr.MetadataRva:X8}",
                $"  Metadata Size:      {FormatSize(clr.MetadataSize, state)}",
                $"  Flags:              {clr.Flags}",
                $"  Entry Point Token:  0x{clr.EntryPointToken:X8}",
                $"  Resources RVA:      0x{clr.ResourcesRva:X8}",
                $"  Resources Size:     {FormatSize(clr.ResourcesSize, state)}",
                $"  Strong Name RVA:    0x{clr.StrongNameSignatureRva:X8}",
                $"  Strong Name Size:   {FormatSize(clr.StrongNameSignatureSize, state)}")
            : "  No CLR header (not a .NET assembly)";

        if (state.ClrHeaderEditorText != clrText)
        {
            state.ClrHeaderEditorText = clrText;
            state.ClrHeaderEditorState = new EditorState(new Hex1bDocument(clrText)) { IsReadOnly = true };
        }

        // Build detail popup editor state when content changes
        if (state.PeDetailContent is not null && state.PeDetailEditorText != state.PeDetailContent)
        {
            state.PeDetailEditorText = state.PeDetailContent;
            state.PeDetailEditorState = new EditorState(new Hex1bDocument(state.PeDetailContent)) { IsReadOnly = true };
        }

        // Adjust word boundaries after double-click (consistent with IL Inspector)
        if (state.CurrentTab == TabId.PeMetadata)
        {
            if (state.PeHeadersEditorState is not null)
                IlInspectorView.AdjustWordSelectionCursorOneShot(
                    state.PeHeadersEditorState,
                    ref state.PeHeadersPrevSelectionAnchor,
                    ref state.PeHeadersPrevCursorPosition);
            if (state.ClrHeaderEditorState is not null)
                IlInspectorView.AdjustWordSelectionCursorOneShot(
                    state.ClrHeaderEditorState,
                    ref state.ClrHeaderPrevSelectionAnchor,
                    ref state.ClrHeaderPrevCursorPosition);
        }

        return ctx.ZStack(z =>
        [
            // Layer 0: Main content
            z.VStack(outer =>
            {
                var widgets = new List<Hex1bWidget>
                {
                    // Top section: PE Headers | CLR Header (side by side, read-only editors)
                    outer.HSplitter(
                    left =>
                    [
                        left.Border(
                            left.ThemePanel(t => t
                                .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                                .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
                            left.Editor(state.PeHeadersEditorState!)
                                .ViewRenderer(InfoEditorViewRenderer.Instance)
                                .Decorations(new InfoLabelDecorationProvider())
                                .Decorations(state.PeHeadersYankProvider)
                                .InputBindings(bindings =>
                                {
                                    TextObjectHelper.ConfigureReadOnlyEditorBindings(
                                        bindings,
                                        state.PeHeadersEditorState!,
                                        () => state.VimPending,
                                        () => state.VimPendingEditor,
                                        () => state.VimPendingCursorOffset,
                                        () => state.VimPendingTimestamp,
                                        (s, e, o) => { state.VimPending = s; state.VimPendingEditor = e; state.VimPendingCursorOffset = o; state.VimPendingTimestamp = DateTime.UtcNow; },
                                        state.PerformEditorYank,
                                        () => state.App.Invalidate());
                                })
                                .FillWidth().FillHeight())
                        ).Title(" PE Headers ").Fill()
                    ],
                    right =>
                    [
                        right.Border(
                            right.ThemePanel(t => t
                                .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                                .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
                            right.Editor(state.ClrHeaderEditorState!)
                                .ViewRenderer(InfoEditorViewRenderer.Instance)
                                .Decorations(new InfoLabelDecorationProvider())
                                .Decorations(state.ClrHeaderYankProvider)
                                .InputBindings(bindings =>
                                {
                                    TextObjectHelper.ConfigureReadOnlyEditorBindings(
                                        bindings,
                                        state.ClrHeaderEditorState!,
                                        () => state.VimPending,
                                        () => state.VimPendingEditor,
                                        () => state.VimPendingCursorOffset,
                                        () => state.VimPendingTimestamp,
                                        (s, e, o) => { state.VimPending = s; state.VimPendingEditor = e; state.VimPendingCursorOffset = o; state.VimPendingTimestamp = DateTime.UtcNow; },
                                        state.PerformEditorYank,
                                        () => state.App.Invalidate());
                                })
                                .FillWidth().FillHeight())
                        ).Title(" CLR Header ").Fill()
                    ],
                    leftWidth: 50).FixedHeight(12)
                };

                // Search bar (shared helper)
                SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

                // Bottom section: Metadata tables in sub-tabs
                Hex1bWidget metadataTabs = outer.TabPanel(tp =>
                [
                    tp.Tab("Sections", t => [BuildSectionsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.Sections),
                    tp.Tab("TypeDef", t => [BuildTypeDefsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.TypeDef),
                    tp.Tab("MethodDef", t => [BuildMethodDefsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.MethodDef),
                    tp.Tab("TypeRef", t => [BuildTypeRefsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.TypeRef),
                    tp.Tab("MemberRef", t => [BuildMemberRefsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.MemberRef),
                    tp.Tab("Attributes", t => [BuildAttributesTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.Attributes),
                    tp.Tab("Resources", t => [BuildResourcesTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.Resources),
                    tp.Tab("Debug Directory", t => [BuildDebugDirectoryTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.DebugDirectory),
                    tp.Tab("Imports", t => [BuildImportsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.Imports),
                    tp.Tab("Exports", t => [BuildExportsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.Exports),
                    tp.Tab("Load Config", t => [BuildLoadConfigTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.LoadConfig),
                    tp.Tab("R2R Sections", t => [BuildRtrSectionsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.RtrSections),
                    tp.Tab("AOT Types", t => [BuildAotTypesTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.AotTypes)
                ])
                .OnSelectionChanged(e =>
                {
                    state.PeSubTab = e.SelectedIndex;
                    search.Reset();
                    state.PeFocusedKey = null;
                    state.RequestContentFocus();
                    state.App.Invalidate();
                })
                .Compact()
                .Fill();

                // Always wrap in a ThemePanel so the widget tree stays stable when
                // the detail popup toggles — avoids re-measure that resets scroll.
                // When the popup is open, suppress the teal tab highlight so it
                // doesn't bleed through the transparent backdrop.
                var popupOpen = state.PeDetailContent is not null;
                metadataTabs = outer.ThemePanel(t => popupOpen
                    ? t.Set(TabBarTheme.SelectedForegroundColor, Hex1bColor.FromRgb(140, 140, 160))
                         .Set(TabBarTheme.SelectedBackgroundColor, Hex1bColor.Default)
                    : t, metadataTabs)
                .Fill();

                widgets.Add(metadataTabs);

                return [.. widgets];
            })
            .InputBindings(bindings =>
            {
                var isSearchEditing = search.IsActive && !search.IsConfirmed;

                if (!isSearchEditing)
                {
                    // Only register Left/Right for sub-tab switching when no editor is focused
                    // (otherwise they consume the key and the editor never sees it)
                    if (state.App.FocusedNode is not EditorNode)
                    {
                        bindings.Key(Hex1bKey.LeftArrow).Global().Action(_ =>
                        {
                            state.VimPending = VimMotionState.Idle;
                            if (state.PeSubTab > 0)
                            {
                                state.PeSubTab--;
                                search.Reset();
                                state.PeFocusedKey = null;
                                state.RequestContentFocus();
                                state.App.Invalidate();
                            }
                        }, "Previous sub-tab");

                        bindings.Key(Hex1bKey.RightArrow).Global().Action(_ =>
                        {
                            state.VimPending = VimMotionState.Idle;
                            if (state.PeSubTab < PeSubTabId.Count - 1)
                            {
                                state.PeSubTab++;
                                search.Reset();
                                state.PeFocusedKey = null;
                                state.RequestContentFocus();
                                state.App.Invalidate();
                            }
                        }, "Next sub-tab");
                    }

                    // Tab cycles focus: PE Headers → CLR Header → Table → PE Headers
                    bindings.Key(Hex1bKey.Tab).Global().Action(_ =>
                    {
                        state.VimPending = VimMotionState.Idle;
                        if (state.App.FocusedNode is EditorNode { State: var es })
                        {
                            if (es == state.PeHeadersEditorState)
                            {
                                // PE Headers → CLR Header
                                state.App.RequestFocus(node =>
                                    node is EditorNode e && e.State == state.ClrHeaderEditorState);
                            }
                            else
                            {
                                // CLR Header (or detail popup) → Table
                                state.RequestContentFocus();
                            }
                        }
                        else
                        {
                            // Table → PE Headers
                            state.App.RequestFocus(node =>
                                node is EditorNode e && e.State == state.PeHeadersEditorState);
                        }
                        state.App.Invalidate();
                    }, "Cycle focus");

                    // g: Go to IL Inspector for focused TypeDef or MethodDef
                    if (state.PeSubTab is PeSubTabId.TypeDef or PeSubTabId.MethodDef)
                    {
                        bindings.Key(Hex1bKey.G).Global().Action(_ =>
                        {
                            state.VimPending = VimMotionState.Idle;
                            if (state.PeFocusedKey is int token)
                            {
                                if (state.PeSubTab == PeSubTabId.TypeDef)
                                {
                                    var typeDef = analyzer.TypeDefs.FirstOrDefault(t => t.Token == token);
                                    if (typeDef is not null)
                                    {
                                        var method = analyzer.MethodDefs.FirstOrDefault(
                                            m => m.DeclaringType == typeDef.FullName);
                                        if (method is not null)
                                            state.NavigateToIlMethod(method);
                                    }
                                }
                                else // MethodDef
                                {
                                    var method = analyzer.MethodDefs.FirstOrDefault(m => m.Token == token);
                                    if (method is not null)
                                        state.NavigateToIlMethod(method);
                                }
                            }
                        }, "Go to IL");
                    }
                }

                // Detail popup dismiss — only register when search is not active
                // to avoid conflicting with DotsiderApp's global "Clear search" binding
                if (!search.IsActive && state.PeDetailContent is not null)
                {
                    bindings.Key(Hex1bKey.Escape).Global().OverridesCapture().Action(_ =>
                    {
                        state.VimPending = VimMotionState.Idle;
                        state.PeDetailContent = null;
                        state.RequestContentFocus();
                        state.App.Invalidate();
                    }, "Dismiss detail");
                }
            })
            .Fill(),

            // Layer 1: Detail popup overlay (read-only editor for selection + yank)
            state.PeDetailContent is not null && state.PeDetailEditorState is not null
                ? z.Backdrop(
                    z.Border(
                        z.ThemePanel(t => t
                            .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                            .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
                        z.Editor(state.PeDetailEditorState)
                            .ViewRenderer(InfoEditorViewRenderer.Instance)
                            .Decorations(new InfoLabelDecorationProvider())
                            .Decorations(state.PeDetailYankProvider)
                            .InputBindings(bindings =>
                            {
                                TextObjectHelper.ConfigureReadOnlyEditorBindings(
                                    bindings,
                                    state.PeDetailEditorState!,
                                    () => state.VimPending,
                                    () => state.VimPendingEditor,
                                    () => state.VimPendingCursorOffset,
                                    () => state.VimPendingTimestamp,
                                    (s, e, o) => { state.VimPending = s; state.VimPendingEditor = e; state.VimPendingCursorOffset = o; state.VimPendingTimestamp = DateTime.UtcNow; },
                                    state.PerformEditorYank,
                                    () => state.App.Invalidate());
                            })
                            .FillWidth().FillHeight())
                    ).Title(" Detail ").FixedWidth(60).FixedHeight(12)
                ).OnClickAway(() =>
                {
                    state.PeDetailContent = null;
                    state.PeDetailEditorText = null;
                    state.PeDetailEditorState = null;
                    state.RequestContentFocus();
                    state.App.Invalidate();
                })
                : null
        ]).Fill();
    }

    private static TableWidget<SectionInfo> BuildSectionsTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.Analyzer.Sections, query,
            s => $"{s.Name} {s.Characteristics}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(s => s.Name)
            .Header(h =>
            [
                h.Cell("Name").Width(SizeHint.Fixed(12)),
                h.Cell("Virtual Addr").Width(SizeHint.Fixed(14)),
                h.Cell("Virtual Size").Width(SizeHint.Fixed(14)),
                h.Cell("Raw Offset").Width(SizeHint.Fixed(14)),
                h.Cell("Raw Size").Width(SizeHint.Fixed(14)),
                h.Cell("Characteristics").Width(SizeHint.Fill)
            ])
            .Row((r, s, rs) =>
            [
                r.Cell(c => FocusHighlightCell(c,s.Name, query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c,HexCell(c, $"0x{s.VirtualAddress:X8}"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c,c.Text(FormatSize(s.VirtualSize, state)), rs.IsFocused)),
                r.Cell(c => FocusStyle(c,HexCell(c, $"0x{s.RawDataOffset:X8}"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c,c.Text(FormatSize(s.RawDataSize, state)), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,s.Characteristics.ToString(), query, true, rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, s) =>
            {
                state.PeDetailContent = string.Join("\n",
                    $"Section: {s.Name}",
                    $"Virtual Address: 0x{s.VirtualAddress:X8}",
                    $"Virtual Size: {s.VirtualSize} (0x{s.VirtualSize:X})",
                    $"Raw Offset: 0x{s.RawDataOffset:X8}",
                    $"Raw Size: {s.RawDataSize} (0x{s.RawDataSize:X})",
                    $"Characteristics: {s.Characteristics}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<DebugDirectoryInfo> BuildDebugDirectoryTable(
        WidgetContext<VStackWidget> ctx,
        DotsiderState state)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.Analyzer.DebugDirectory, query,
            d => $"{d.Type} {d.Stamp:X8} {d.Payload}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(GetDebugDirectoryKey)
            .Header(h =>
            [
                h.Cell("Type").Width(SizeHint.Fixed(20)),
                h.Cell("Stamp").Width(SizeHint.Fixed(12)),
                h.Cell("Major").Width(SizeHint.Fixed(7)),
                h.Cell("Minor").Width(SizeHint.Fixed(7)),
                h.Cell("Size").Width(SizeHint.Fixed(10)),
                h.Cell("RVA").Width(SizeHint.Fixed(12)),
                h.Cell("Pointer").Width(SizeHint.Fixed(12)),
                h.Cell("Payload").Width(SizeHint.Fill)
            ])
            .Row((r, d, rs) =>
            [
                r.Cell(c => FocusHighlightCell(c,d.Type.ToString(), query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c,HexCell(c, $"0x{d.Stamp:X8}"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c,c.Text(d.MajorVersion.ToString()), rs.IsFocused)),
                r.Cell(c => FocusStyle(c,c.Text(d.MinorVersion.ToString()), rs.IsFocused)),
                r.Cell(c => FocusStyle(c,c.Text(FormatSize(d.DataSize, state)), rs.IsFocused)),
                r.Cell(c => FocusStyle(c,HexCell(c, $"0x{d.AddressOfRawData:X8}"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c,HexCell(c, $"0x{d.PointerToRawData:X8}"), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,d.Payload, query, true, rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, d) =>
            {
                state.PeDetailContent = string.Join("\n",
                    "Debug Directory",
                    $"Type: {d.Type}",
                    $"Stamp: 0x{d.Stamp:X8}",
                    $"Major Version: {d.MajorVersion}",
                    $"Minor Version: {d.MinorVersion}",
                    $"Data Size: {d.DataSize} (0x{d.DataSize:X})",
                    $"Address Of Raw Data: 0x{d.AddressOfRawData:X8}",
                    $"Pointer To Raw Data: 0x{d.PointerToRawData:X8}",
                    $"Payload: {d.Payload}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<TypeDefInfo> BuildTypeDefsTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.Analyzer.TypeDefs, query,
            t => $"{t.FullName} {t.BaseType} {t.Attributes}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(t => t.Token)
            .Header(h =>
            [
                h.Cell("Token").Width(SizeHint.Fixed(12)),
                h.Cell("Full Name").Width(SizeHint.Fill),
                h.Cell("Base Type").Width(SizeHint.Fixed(30)),
                h.Cell("Attributes").Width(SizeHint.Fixed(20)),
                h.Cell("Methods").Width(SizeHint.Fixed(8)),
                h.Cell("Fields").Width(SizeHint.Fixed(8))
            ])
            .Row((r, t, rs) =>
            [
                r.Cell(c => FocusStyle(c,HexCell(c, $"0x{t.Token:X8}"), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,t.FullName, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,t.BaseType ?? "", query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,t.Attributes.ToString(), query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c,c.Text(t.MethodCount.ToString()), rs.IsFocused)),
                r.Cell(c => FocusStyle(c,c.Text(t.FieldCount.ToString()), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, t) =>
            {
                state.PeDetailContent = string.Join("\n",
                    $"TypeDef: {t.FullName}",
                    $"Token: 0x{t.Token:X8}",
                    $"Base Type: {t.BaseType ?? "none"}",
                    $"Attributes: {t.Attributes}",
                    $"Methods: {t.MethodCount}",
                    $"Fields: {t.FieldCount}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<MethodDefInfo> BuildMethodDefsTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.Analyzer.MethodDefs, query,
            m => $"{m.DeclaringType} {m.Name} {m.Signature}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(m => m.Token)
            .Header(h =>
            [
                h.Cell("Token").Width(SizeHint.Fixed(12)),
                h.Cell("Type").Width(SizeHint.Fixed(30)),
                h.Cell("Name").Width(SizeHint.Fixed(25)),
                h.Cell("Signature").Width(SizeHint.Fill),
                h.Cell("Attributes").Width(SizeHint.Fixed(20)),
                h.Cell("RVA").Width(SizeHint.Fixed(12))
            ])
            .Row((r, m, rs) =>
            [
                r.Cell(c => FocusStyle(c,HexCell(c, $"0x{m.Token:X8}"), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,m.DeclaringType, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,m.Name, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,m.Signature, query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c,c.Text(m.Attributes.ToString()), rs.IsFocused)),
                r.Cell(c => FocusStyle(c,m.Rva == 0 ? c.Text("") : HexCell(c, $"0x{m.Rva:X8}"), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, m) =>
            {
                state.PeDetailContent = string.Join("\n",
                    $"MethodDef: {m.DeclaringType}::{m.Name}",
                    $"Token: 0x{m.Token:X8}",
                    $"Signature: {m.Signature}",
                    $"Attributes: {m.Attributes}",
                    $"Impl: {m.ImplAttributes}",
                    $"RVA: 0x{m.Rva:X8}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<TypeRefInfo> BuildTypeRefsTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.Analyzer.TypeRefs, query,
            t => $"{t.FullName} {t.ResolutionScope}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(t => t.Token)
            .Header(h =>
            [
                h.Cell("Token").Width(SizeHint.Fixed(12)),
                h.Cell("Full Name").Width(SizeHint.Fill),
                h.Cell("Resolution Scope").Width(SizeHint.Fixed(30))
            ])
            .Row((r, t, rs) =>
            [
                r.Cell(c => FocusStyle(c,HexCell(c, $"0x{t.Token:X8}"), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,t.FullName, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,t.ResolutionScope, query, true, rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, t) =>
            {
                state.PeDetailContent = string.Join("\n",
                    $"TypeRef: {t.FullName}",
                    $"Token: 0x{t.Token:X8}",
                    $"Namespace: {t.Namespace}",
                    $"Name: {t.Name}",
                    $"Resolution Scope: {t.ResolutionScope}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<MemberRefInfo> BuildMemberRefsTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.Analyzer.MemberRefs, query,
            m => $"{m.DeclaringType} {m.Name}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(m => m.Token)
            .Header(h =>
            [
                h.Cell("Token").Width(SizeHint.Fixed(12)),
                h.Cell("Declaring Type").Width(SizeHint.Fill),
                h.Cell("Name").Width(SizeHint.Fixed(25))
            ])
            .Row((r, m, rs) =>
            [
                r.Cell(c => FocusStyle(c,HexCell(c, $"0x{m.Token:X8}"), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,m.DeclaringType, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,m.Name, query, true, rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, m) =>
            {
                state.PeDetailContent = string.Join("\n",
                    $"MemberRef: {m.DeclaringType}::{m.Name}",
                    $"Token: 0x{m.Token:X8}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<CustomAttributeInfo> BuildAttributesTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.Analyzer.CustomAttributes, query,
            a => $"{a.Parent} {a.Constructor} {a.Value}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(a => $"{a.Parent}|{a.Constructor}")
            .Header(h =>
            [
                h.Cell("Parent").Width(SizeHint.Fixed(30)),
                h.Cell("Constructor").Width(SizeHint.Fill),
                h.Cell("Value").Width(SizeHint.Fixed(40))
            ])
            .Row((r, a, rs) =>
            [
                r.Cell(c => FocusHighlightCell(c,a.Parent, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,a.Constructor, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,a.Value ?? "", query, true, rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, a) =>
            {
                state.PeDetailContent = string.Join("\n",
                    "Custom Attribute",
                    $"Parent: {a.Parent}",
                    $"Constructor: {a.Constructor}",
                    $"Value: {a.Value ?? "null"}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<ResourceInfo> BuildResourcesTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.Analyzer.Resources, query,
            r => $"{r.Name} {r.Visibility}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(r => r.Name)
            .Header(h =>
            [
                h.Cell("Name").Width(SizeHint.Fill),
                h.Cell("Visibility").Width(SizeHint.Fixed(10)),
                h.Cell("Offset").Width(SizeHint.Fixed(12)),
                h.Cell("Size").Width(SizeHint.Fixed(12)),
                h.Cell("Linked").Width(SizeHint.Fixed(8))
            ])
            .Row((r, res, rs) =>
            [
                r.Cell(c => FocusHighlightCell(c,res.Name, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,res.Visibility, query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c,HexCell(c, $"0x{res.Offset:X8}"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c,c.Text(res.Size >= 0 ? FormatSize((int)res.Size, state) : "?"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c,c.Text(res.IsLinked ? "Yes" : "No"), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, res) =>
            {
                state.PeDetailContent = string.Join("\n",
                    $"Resource: {res.Name}",
                    $"Visibility: {res.Visibility}",
                    $"Offset: 0x{res.Offset:X8}",
                    $"Size: {(res.Size >= 0 ? res.Size.ToString() : "unknown")}",
                    $"Linked: {res.IsLinked}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static string FormatSize(int size, DotsiderState state) =>
        state.FormatSizeToggleable(size);

    private static IReadOnlyList<T> ApplySearch<T>(
        IReadOnlyList<T> items, string? query, Func<T, string> toSearchable)
    {
        if (string.IsNullOrEmpty(query)) return items;
        return [.. items
            .Where(i => toSearchable(i).Contains(query, StringComparison.OrdinalIgnoreCase))];
    }

    private static ThemePanelWidget HexCell<T>(WidgetContext<T> c, string text) where T : Hex1bWidget =>
        c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, AddressColor), c.Text(text));

    private static readonly Hex1bColor FocusFg = Hex1bColor.Black;
    private static readonly Hex1bColor FocusBg = Hex1bColor.FromRgb(0, 200, 180);
    private static readonly Hex1bColor YankFlashFg = Hex1bColor.FromRgb(24, 24, 37);
    private static readonly Hex1bColor YankFlashBg = Hex1bColor.FromRgb(126, 201, 216);

    private static Hex1bWidget FocusStyle<T>(WidgetContext<T> c, Hex1bWidget child, bool isFocused)
        where T : Hex1bWidget
    {
        if (!isFocused) return child;
        var flash = s_yankFlash;
        var fg = flash ? YankFlashFg : FocusFg;
        var bg = flash ? YankFlashBg : FocusBg;
        return c.ThemePanel(t => t
            .Set(GlobalTheme.ForegroundColor, fg)
            .Set(GlobalTheme.BackgroundColor, bg), child);
    }

    private static Hex1bWidget FocusHighlightCell<T>(
        WidgetContext<T> c, string text, string? query, bool isMatch, bool isFocused)
        where T : Hex1bWidget
    {
        var flash = isFocused && s_yankFlash;
        var fg = isFocused ? (flash ? YankFlashFg : FocusFg) : (Hex1bColor?)null;
        var bg = isFocused ? (flash ? YankFlashBg : FocusBg) : (Hex1bColor?)null;
        return FocusStyle(c, HighlightHelper.HighlightCell(c, text, query, isMatch, fg, bg), isFocused);
    }

    private static IReadOnlyList<object> GetActiveRowKeys(DotsiderState state)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        if (string.IsNullOrEmpty(query)) return [];
        var analyzer = state.Analyzer;
        return state.PeSubTab switch
        {
            PeSubTabId.Sections => [.. ApplySearch(analyzer.Sections, query,
                s => $"{s.Name} {s.Characteristics}").Select(s => (object)s.Name)],
            PeSubTabId.TypeDef => [.. ApplySearch(analyzer.TypeDefs, query,
                t => $"{t.FullName} {t.BaseType} {t.Attributes}").Select(t => (object)t.Token)],
            PeSubTabId.MethodDef => [.. ApplySearch(analyzer.MethodDefs, query,
                m => $"{m.DeclaringType} {m.Name} {m.Signature}").Select(m => (object)m.Token)],
            PeSubTabId.TypeRef => [.. ApplySearch(analyzer.TypeRefs, query,
                t => $"{t.FullName} {t.ResolutionScope}").Select(t => (object)t.Token)],
            PeSubTabId.MemberRef => [.. ApplySearch(analyzer.MemberRefs, query,
                m => $"{m.DeclaringType} {m.Name}").Select(m => (object)m.Token)],
            PeSubTabId.Attributes => [.. ApplySearch(analyzer.CustomAttributes, query,
                a => $"{a.Parent} {a.Constructor} {a.Value}").Select(a => (object)$"{a.Parent}|{a.Constructor}")],
            PeSubTabId.Resources => [.. ApplySearch(analyzer.Resources, query,
                r => $"{r.Name} {r.Visibility}").Select(r => (object)r.Name)],
            PeSubTabId.DebugDirectory => [.. ApplySearch(analyzer.DebugDirectory, query,
                d => $"{d.Type} {d.Stamp:X8} {d.Payload}").Select(d => (object)GetDebugDirectoryKey(d))],
            PeSubTabId.Imports => [.. ApplySearch(GetImportRows(analyzer), query,
                r => $"{r.Module} {r.Function.Name}").Select(r => (object)r.Key)],
            PeSubTabId.Exports => [.. ApplySearch(analyzer.Exports, query,
                e => $"{e.Name} {e.Ordinal} {e.ForwardedTo}").Select(e => (object)e.Ordinal)],
            PeSubTabId.LoadConfig when analyzer.LoadConfig is not null =>
                [.. ApplySearch(GetLoadConfigRows(analyzer.LoadConfig), query,
                    r => $"{r.Field} {r.Value}").Select(r => (object)r.Field)],
            PeSubTabId.RtrSections => [.. ApplySearch(analyzer.ReadyToRunSections, query,
                s => $"{s.SectionId} {s.Name}").Select(s => (object)s.SectionId)],
            PeSubTabId.AotTypes => [.. ApplySearch(analyzer.RecoveredTypes, query,
                t => t.FullName).Select(t => (object)t.FullName)],
            _ => []
        };
    }

    private static TableWidget<ImportRow> BuildImportsTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(GetImportRows(state.Analyzer), query,
            r => $"{r.Module} {r.Function.Name}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(r => r.Key)
            .Header(h =>
            [
                h.Cell("Module").Width(SizeHint.Fixed(24)),
                h.Cell("Function").Width(SizeHint.Fill),
                h.Cell("Hint").Width(SizeHint.Fixed(8)),
                h.Cell("Ordinal").Width(SizeHint.Fixed(9))
            ])
            .Row((r, row, rs) =>
            [
                r.Cell(c => FocusHighlightCell(c, row.Module, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, ImportFunctionDisplay(row.Function), query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(row.Function.Hint?.ToString() ?? ""), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(row.Function.Ordinal?.ToString() ?? ""), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, row) =>
            {
                state.PeDetailContent = string.Join("\n",
                    "Imported Function",
                    $"Module: {row.Module}",
                    $"Function: {ImportFunctionDisplay(row.Function)}",
                    $"Hint: {row.Function.Hint?.ToString() ?? "(none)"}",
                    $"Ordinal: {row.Function.Ordinal?.ToString() ?? "(imported by name)"}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<ExportedFunctionInfo> BuildExportsTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.Analyzer.Exports, query,
            e => $"{e.Name} {e.Ordinal} {e.ForwardedTo}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(e => e.Ordinal)
            .Header(h =>
            [
                h.Cell("Ordinal").Width(SizeHint.Fixed(9)),
                h.Cell("Name").Width(SizeHint.Fill),
                h.Cell("RVA").Width(SizeHint.Fixed(12)),
                h.Cell("Forwarded To").Width(SizeHint.Fixed(30))
            ])
            .Row((r, e, rs) =>
            [
                r.Cell(c => FocusStyle(c, c.Text(e.Ordinal.ToString()), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, e.Name ?? "(ordinal only)", query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c, HexCell(c, $"0x{e.Rva:X8}"), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, e.ForwardedTo ?? "", query, true, rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, e) =>
            {
                state.PeDetailContent = string.Join("\n",
                    "Exported Function",
                    $"Ordinal: {e.Ordinal}",
                    $"Name: {e.Name ?? "(ordinal only)"}",
                    $"RVA: 0x{e.Rva:X8}",
                    $"Forwarded To: {e.ForwardedTo ?? "(not forwarded)"}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<LoadConfigRow> BuildLoadConfigTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        IReadOnlyList<LoadConfigRow> rows = state.Analyzer.LoadConfig is { } loadConfig
            ? GetLoadConfigRows(loadConfig)
            : [];
        var data = ApplySearch(rows, query, r => $"{r.Field} {r.Value}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(r => r.Field)
            .Header(h =>
            [
                h.Cell("Field").Width(SizeHint.Fixed(28)),
                h.Cell("Value").Width(SizeHint.Fill)
            ])
            .Row((r, row, rs) =>
            [
                r.Cell(c => FocusHighlightCell(c, row.Field, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, row.Value, query, true, rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, row) =>
            {
                state.PeDetailContent = string.Join("\n",
                    "Load Configuration",
                    $"{row.Field}: {row.Value}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<RtrSection> BuildRtrSectionsTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.Analyzer.ReadyToRunSections, query, s => $"{s.SectionId} {s.Name}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(s => s.SectionId)
            .Header(h =>
            [
                h.Cell("Id").Width(SizeHint.Fixed(6)),
                h.Cell("Name").Width(SizeHint.Fill),
                h.Cell("Virtual Addr").Width(SizeHint.Fixed(18)),
                h.Cell("Size").Width(SizeHint.Fixed(12)),
                h.Cell("File Offset").Width(SizeHint.Fixed(14))
            ])
            .Row((r, s, rs) =>
            [
                r.Cell(c => FocusStyle(c, c.Text(s.SectionId.ToString()), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, s.Name, query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c, HexCell(c, $"0x{s.VirtualAddress:X}"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(FormatSize((int)Math.Min(s.Size, int.MaxValue), state)), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, HexCell(c, s.FileOffset is { } o ? $"0x{o:X}" : "(in memory)"), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, s) =>
            {
                state.PeDetailContent = string.Join("\n",
                    "ReadyToRun Section",
                    $"Section Id: {s.SectionId}",
                    $"Name: {s.Name}",
                    $"Virtual Address: 0x{s.VirtualAddress:X}",
                    $"Size: {s.Size} (0x{s.Size:X})",
                    $"File Offset: {(s.FileOffset is { } o ? $"0x{o:X}" : "(filled at startup)")}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<RecoveredType> BuildAotTypesTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.Analyzer.RecoveredTypes, query, t => t.FullName);
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(t => t.FullName)
            .Header(h =>
            [
                h.Cell("Type").Width(SizeHint.Fill),
                h.Cell("Methods").Width(SizeHint.Fixed(9))
            ])
            .Row((r, t, rs) =>
            [
                r.Cell(c => FocusHighlightCell(c, t.FullName, query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(t.MethodNames.Count.ToString()), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, t) =>
            {
                var methods = t.MethodNames.Count > 0
                    ? string.Join("\n", t.MethodNames.Select(m => $"  {m}"))
                    : "  (no methods)";
                state.PeDetailContent = string.Join("\n",
                    $"Type: {t.FullName}",
                    $"Methods ({t.MethodNames.Count}):",
                    methods);
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    /// <summary>Flattens the import table into one row per imported function.</summary>
    private static IReadOnlyList<ImportRow> GetImportRows(Core.Analysis.AssemblyAnalyzer analyzer) =>
        [.. analyzer.Imports.SelectMany(m => m.Functions.Select(f =>
            new ImportRow(m.ModuleName, f, $"{m.ModuleName}!{f.Name ?? $"#{f.Ordinal}"}")))];

    /// <summary>Projects the load configuration into displayable field/value rows.</summary>
    private static IReadOnlyList<LoadConfigRow> GetLoadConfigRows(LoadConfigInfo loadConfig) =>
    [
        new("Size", $"{loadConfig.Size} (0x{loadConfig.Size:X})"),
        new("Timestamp", $"0x{loadConfig.TimeDateStamp:X8}"),
        new("Version", $"{loadConfig.MajorVersion}.{loadConfig.MinorVersion}"),
        new("Dependent Load Flags", $"0x{loadConfig.DependentLoadFlags:X4}"),
        new("Security Cookie", loadConfig.SecurityCookie == 0
            ? "(none)" : $"0x{loadConfig.SecurityCookie:X}"),
        new("SEH Handler Count", loadConfig.SehHandlerCount.ToString()),
        new("Guard CF Check Function", loadConfig.GuardCfCheckFunctionPointer == 0
            ? "(none)" : $"0x{loadConfig.GuardCfCheckFunctionPointer:X}"),
        new("Guard CF Function Count", loadConfig.GuardCfFunctionCount.ToString()),
        new("Guard Flags", $"0x{loadConfig.GuardFlags:X8}"),
        new("Guard Flags Decoded", loadConfig.GuardFlagsDescription),
    ];

    private static string ImportFunctionDisplay(ImportedFunctionInfo function) =>
        function.Name ?? $"#{function.Ordinal}";

    /// <summary>A single imported function flattened with its owning module.</summary>
    private sealed record ImportRow(string Module, ImportedFunctionInfo Function, string Key);

    /// <summary>A load-configuration field projected for table display.</summary>
    private sealed record LoadConfigRow(string Field, string Value);

    private static string GetDebugDirectoryKey(DebugDirectoryInfo info) =>
        $"{info.Type}:{info.AddressOfRawData:X8}:{info.PointerToRawData:X8}";

    private static int FindKeyIndex(IReadOnlyList<object> keys, object? focusedKey)
    {
        if (focusedKey is null) return -1;
        for (var i = 0; i < keys.Count; i++)
        {
            if (keys[i].Equals(focusedKey))
                return i;
        }
        return -1;
    }
}
