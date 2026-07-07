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
        // Metadata tables answer from the attached pre-ILC root when one exists; the PE
        // headers and native tables stay on the binary itself.
        var metadataAnalyzer = state.MetadataAnalyzer;
        var routed = !ReferenceEquals(metadataAnalyzer, analyzer);
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
                    PeSubTabId.Sections when analyzer.WasmModuleInfo is { Sections.Count: > 0 } wasm =>
                        GetWasmSectionKey(wasm.Sections[0]),
                    PeSubTabId.TypeDef when analyzer.WasmModuleInfo is { Types.Count: > 0 } wasm =>
                        GetWasmTypeKey(wasm.Types[0]),
                    PeSubTabId.MethodDef when analyzer.WasmModuleInfo is { Functions.Count: > 0 } wasm =>
                        GetWasmFunctionKey(wasm.Functions[0]),
                    PeSubTabId.TypeRef when analyzer.WasmModuleInfo is { Tables.Count: > 0 } wasm =>
                        GetWasmTableKey(wasm.Tables[0]),
                    PeSubTabId.MemberRef when analyzer.WasmModuleInfo is { Memories.Count: > 0 } wasm =>
                        GetWasmMemoryKey(wasm.Memories[0]),
                    PeSubTabId.Attributes when analyzer.WasmModuleInfo is { Globals.Count: > 0 } wasm =>
                        GetWasmGlobalKey(wasm.Globals[0]),
                    PeSubTabId.Resources when analyzer.WasmModuleInfo is { DataSegments.Count: > 0 } wasm =>
                        GetWasmDataSegmentKey(wasm.DataSegments[0]),
                    PeSubTabId.DebugDirectory when analyzer.WasmModuleInfo is { Sections.Count: > 0 } wasm
                        && wasm.Sections.FirstOrDefault(static s => s.Id == 0) is { } custom =>
                        GetWasmSectionKey(custom),
                    PeSubTabId.Sections when analyzer.Sections.Count > 0 => analyzer.Sections[0].Name,
                    PeSubTabId.TypeDef when metadataAnalyzer.TypeDefs.Count > 0 => metadataAnalyzer.TypeDefs[0].Token,
                    PeSubTabId.MethodDef when metadataAnalyzer.MethodDefs.Count > 0 => metadataAnalyzer.MethodDefs[0].Token,
                    PeSubTabId.TypeRef when metadataAnalyzer.TypeRefs.Count > 0 => metadataAnalyzer.TypeRefs[0].Token,
                    PeSubTabId.MemberRef when metadataAnalyzer.MemberRefs.Count > 0 => metadataAnalyzer.MemberRefs[0].Token,
                    PeSubTabId.Attributes when metadataAnalyzer.CustomAttributes.Count > 0 =>
                        $"{metadataAnalyzer.CustomAttributes[0].Parent}|{metadataAnalyzer.CustomAttributes[0].Constructor}",
                    PeSubTabId.Resources when metadataAnalyzer.Resources.Count > 0 => metadataAnalyzer.Resources[0].Name,
                    PeSubTabId.DebugDirectory when GetDebugDirectoryRows(state).Count > 0 =>
                        GetDebugDirectoryRowKey(GetDebugDirectoryRows(state)[0]),
                    PeSubTabId.Imports when analyzer.WasmModuleInfo is { Imports.Count: > 0 } wasm =>
                        GetWasmImportKey(wasm.Imports[0]),
                    PeSubTabId.Imports when GetImportRows(analyzer).Count > 0 =>
                        GetImportRows(analyzer)[0].Key,
                    PeSubTabId.Exports when analyzer.WasmModuleInfo is { Exports.Count: > 0 } wasm =>
                        GetWasmExportKey(wasm.Exports[0]),
                    PeSubTabId.Exports when analyzer.Exports.Count > 0 =>
                        analyzer.Exports[0].Ordinal,
                    PeSubTabId.LoadConfig when analyzer.WasmModuleInfo is { Elements.Count: > 0 } wasm =>
                        GetWasmElementKey(wasm.Elements[0]),
                    PeSubTabId.LoadConfig when analyzer.LoadConfig is not null =>
                        GetLoadConfigRows(analyzer.LoadConfig)[0].Field,
                    PeSubTabId.RtrSections when analyzer.WasmModuleInfo is { Tags.Count: > 0 } wasm =>
                        GetWasmTagKey(wasm.Tags[0]),
                    PeSubTabId.RtrSections when analyzer.ReadyToRunSections.Count > 0 =>
                        analyzer.ReadyToRunSections[0].SectionId,
                    PeSubTabId.AotTypes when analyzer.WasmModuleInfo is not null =>
                        GetWasmModuleInfoKey("version"),
                    PeSubTabId.AotTypes when analyzer.RecoveredTypes.Count > 0 =>
                        analyzer.RecoveredTypes[0].FullName,
                    PeSubTabId.Symbols when GetSymbolRows(analyzer).Count > 0 =>
                        GetSymbolRows(analyzer)[0].VirtualAddress,
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

        // Build CLR Header text — the companion's when attached (the native AOT image has none).
        var clrText = metadataAnalyzer.ClrHeader is { } clr
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
                        ).Title(routed ? " CLR Header (pre-ILC) " : " CLR Header ").Fill()
                    ],
                    leftWidth: 50).FixedHeight(12)
                };

                // Search bar (shared helper)
                SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

                // Bottom section: Metadata tables in sub-tabs
                Hex1bWidget metadataTabs = outer.TabPanel(tp =>
                [
                    tp.Tab(GetPeSubTabLabel(state, PeSubTabId.Sections), t => [BuildSectionsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.Sections),
                    tp.Tab(GetPeSubTabLabel(state, PeSubTabId.TypeDef), t => [BuildTypeDefsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.TypeDef),
                    tp.Tab(GetPeSubTabLabel(state, PeSubTabId.MethodDef), t => [BuildMethodDefsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.MethodDef),
                    tp.Tab(GetPeSubTabLabel(state, PeSubTabId.TypeRef), t => [BuildTypeRefsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.TypeRef),
                    tp.Tab(GetPeSubTabLabel(state, PeSubTabId.MemberRef), t => [BuildMemberRefsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.MemberRef),
                    tp.Tab(GetPeSubTabLabel(state, PeSubTabId.Attributes), t => [BuildAttributesTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.Attributes),
                    tp.Tab(GetPeSubTabLabel(state, PeSubTabId.Resources), t => [BuildResourcesTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.Resources),
                    tp.Tab(GetPeSubTabLabel(state, PeSubTabId.DebugDirectory), t => [BuildDebugDirectoryTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.DebugDirectory),
                    tp.Tab(GetPeSubTabLabel(state, PeSubTabId.Imports), t => [BuildImportsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.Imports),
                    tp.Tab(GetPeSubTabLabel(state, PeSubTabId.Exports), t => [BuildExportsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.Exports),
                    tp.Tab(GetPeSubTabLabel(state, PeSubTabId.LoadConfig), t => [BuildLoadConfigTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.LoadConfig),
                    tp.Tab(GetPeSubTabLabel(state, PeSubTabId.RtrSections), t => [BuildRtrSectionsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.RtrSections),
                    tp.Tab(GetPeSubTabLabel(state, PeSubTabId.AotTypes), t => [BuildAotTypesTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.AotTypes),
                    tp.Tab(GetPeSubTabLabel(state, PeSubTabId.Symbols), t => [BuildSymbolsTable(t, state)])
                        .Selected(state.PeSubTab == PeSubTabId.Symbols)
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
                                // Tokens come from the routed analyzer, so they match the
                                // companion-driven IL tree when a pre-ILC set is attached.
                                if (state.PeSubTab == PeSubTabId.TypeDef)
                                {
                                    var typeDef = metadataAnalyzer.TypeDefs.FirstOrDefault(t => t.Token == token);
                                    if (typeDef is not null)
                                    {
                                        var method = metadataAnalyzer.MethodDefs.FirstOrDefault(
                                            m => m.DeclaringType == typeDef.FullName);
                                        if (method is not null)
                                            state.NavigateToIlMethod(method);
                                    }
                                }
                                else // MethodDef
                                {
                                    var method = metadataAnalyzer.MethodDefs.FirstOrDefault(m => m.Token == token);
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

    private static Hex1bWidget BuildSectionsTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        if (state.Analyzer.WasmModuleInfo is { } wasm)
            return BuildWasmSectionsTable(ctx, state, wasm);

        return BuildPeSectionsTable(ctx, state);
    }

    private static string GetPeSubTabLabel(DotsiderState state, int subTab) =>
        state.Analyzer.WasmModuleInfo is not null
            ? subTab switch
            {
                PeSubTabId.Sections => "Sections",
                PeSubTabId.TypeDef => "Types",
                PeSubTabId.MethodDef => "Functions",
                PeSubTabId.TypeRef => "Tables",
                PeSubTabId.MemberRef => "Memories",
                PeSubTabId.Attributes => "Globals",
                PeSubTabId.Resources => "Data",
                PeSubTabId.DebugDirectory => "Custom",
                PeSubTabId.Imports => "Imports",
                PeSubTabId.Exports => "Exports",
                PeSubTabId.LoadConfig => "Elements",
                PeSubTabId.RtrSections => "Tags",
                PeSubTabId.AotTypes => "Module",
                PeSubTabId.Symbols => "Symbols",
                _ => "",
            }
            : subTab switch
            {
                PeSubTabId.Sections => "Sections",
                PeSubTabId.TypeDef => "TypeDef",
                PeSubTabId.MethodDef => "MethodDef",
                PeSubTabId.TypeRef => "TypeRef",
                PeSubTabId.MemberRef => "MemberRef",
                PeSubTabId.Attributes => "Attributes",
                PeSubTabId.Resources => "Resources",
                PeSubTabId.DebugDirectory => "Debug Directory",
                PeSubTabId.Imports => "Imports",
                PeSubTabId.Exports => "Exports",
                PeSubTabId.LoadConfig => "Load Config",
                PeSubTabId.RtrSections => "R2R Sections",
                PeSubTabId.AotTypes => "AOT Types",
                PeSubTabId.Symbols => "Symbols",
                _ => "",
            };

    private static TableWidget<WasmSectionInfo> BuildWasmSectionsTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state, WasmModuleInfo wasm)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(wasm.Sections, query, s => $"{s.Id} {s.Name}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(GetWasmSectionKey)
            .Header(h =>
            [
                h.Cell("Id").Width(SizeHint.Fixed(6)),
                h.Cell("Name").Width(SizeHint.Fill),
                h.Cell("Payload Offset").Width(SizeHint.Fixed(16)),
                h.Cell("Payload Size").Width(SizeHint.Fixed(14))
            ])
            .Row((r, s, rs) =>
            [
                r.Cell(c => FocusStyle(c, c.Text(s.Id.ToString()), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, s.Name, query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c, HexCell(c, $"0x{s.FileOffset:X}"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(FormatSize(s.Size, state)), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, s) =>
            {
                state.PeDetailContent = string.Join("\n",
                    "WebAssembly Section",
                    $"Id: {s.Id}",
                    $"Name: {s.Name}",
                    $"Payload Offset: 0x{s.FileOffset:X}",
                    $"Payload Size: {s.Size} (0x{s.Size:X})");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<SectionInfo> BuildPeSectionsTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
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

    /// <summary>One Debug Directory row with its provenance: the native binary's own entry
    /// or the attached pre-ILC companion's. Both symbol stores are exactly what the sidecar
    /// feature joins, so both are shown, tagged by origin.</summary>
    internal readonly record struct DebugDirectoryRow(string Origin, DebugDirectoryInfo Info);

    /// <summary>
    /// The Debug Directory rows for the current state: the binary's entries, plus the
    /// pre-ILC companion's tagged with their origin when a set is attached.
    /// </summary>
    internal static IReadOnlyList<DebugDirectoryRow> GetDebugDirectoryRows(DotsiderState state)
    {
        var rows = new List<DebugDirectoryRow>(
            state.Analyzer.DebugDirectory.Select(d => new DebugDirectoryRow("native", d)));
        if (state.Analyzer.PreIlcCompanions is { } companions)
            rows.AddRange(companions.Root.DebugDirectory.Select(d => new DebugDirectoryRow("pre-ILC", d)));
        return rows;
    }

    /// <summary>The origin-prefixed row key — unique across the merged analyzers.</summary>
    internal static string GetDebugDirectoryRowKey(DebugDirectoryRow row) =>
        $"{row.Origin}:{GetDebugDirectoryKey(row.Info)}";

    private static Hex1bWidget BuildDebugDirectoryTable(
        WidgetContext<VStackWidget> ctx,
        DotsiderState state)
    {
        if (state.Analyzer.WasmModuleInfo is { } wasm)
            return BuildWasmCustomSectionsTable(ctx, state, wasm);

        var query = state.Search[TabId.PeMetadata].Query;
        var merged = state.Analyzer.PreIlcCompanions is not null;
        var data = ApplySearch(GetDebugDirectoryRows(state), query,
            r => $"{r.Origin} {r.Info.Type} {r.Info.Stamp:X8} {r.Info.Payload}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(r => GetDebugDirectoryRowKey(r))
            .Header(h =>
            [
                .. merged ? new[] { h.Cell("Origin").Width(SizeHint.Fixed(9)) } : [],
                h.Cell("Type").Width(SizeHint.Fixed(20)),
                h.Cell("Stamp").Width(SizeHint.Fixed(12)),
                h.Cell("Major").Width(SizeHint.Fixed(7)),
                h.Cell("Minor").Width(SizeHint.Fixed(7)),
                h.Cell("Size").Width(SizeHint.Fixed(10)),
                h.Cell("RVA").Width(SizeHint.Fixed(12)),
                h.Cell("Pointer").Width(SizeHint.Fixed(12)),
                h.Cell("Payload").Width(SizeHint.Fill)
            ])
            .Row((r, row, rs) =>
            {
                var d = row.Info;
                return
                [
                    .. merged
                        ? [r.Cell(c => FocusHighlightCell(c, row.Origin, query, true, rs.IsFocused))]
                        : (TableCell[])[],
                    r.Cell(c => FocusHighlightCell(c,d.Type.ToString(), query, true, rs.IsFocused)),
                    r.Cell(c => FocusStyle(c,HexCell(c, $"0x{d.Stamp:X8}"), rs.IsFocused)),
                    r.Cell(c => FocusStyle(c,c.Text(d.MajorVersion.ToString()), rs.IsFocused)),
                    r.Cell(c => FocusStyle(c,c.Text(d.MinorVersion.ToString()), rs.IsFocused)),
                    r.Cell(c => FocusStyle(c,c.Text(FormatSize(d.DataSize, state)), rs.IsFocused)),
                    r.Cell(c => FocusStyle(c,HexCell(c, $"0x{d.AddressOfRawData:X8}"), rs.IsFocused)),
                    r.Cell(c => FocusStyle(c,HexCell(c, $"0x{d.PointerToRawData:X8}"), rs.IsFocused)),
                    r.Cell(c => FocusHighlightCell(c,d.Payload, query, true, rs.IsFocused))
                ];
            })
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, row) =>
            {
                var d = row.Info;
                var lines = new List<string> { "Debug Directory" };
                if (merged) lines.Add($"Origin: {row.Origin}");
                lines.AddRange(
                [
                    $"Type: {d.Type}",
                    $"Stamp: 0x{d.Stamp:X8}",
                    $"Major Version: {d.MajorVersion}",
                    $"Minor Version: {d.MinorVersion}",
                    $"Data Size: {d.DataSize} (0x{d.DataSize:X})",
                    $"Address Of Raw Data: 0x{d.AddressOfRawData:X8}",
                    $"Pointer To Raw Data: 0x{d.PointerToRawData:X8}",
                    $"Payload: {d.Payload}",
                ]);
                state.PeDetailContent = string.Join("\n", lines);
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static Hex1bWidget BuildTypeDefsTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        if (state.Analyzer.WasmModuleInfo is { } wasm)
            return BuildWasmTypesTable(ctx, state, wasm);

        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.MetadataAnalyzer.TypeDefs, query,
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

    private static Hex1bWidget BuildMethodDefsTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        if (state.Analyzer.WasmModuleInfo is { } wasm)
            return BuildWasmFunctionsTable(ctx, state, wasm);

        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.MetadataAnalyzer.MethodDefs, query,
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

    private static Hex1bWidget BuildTypeRefsTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        if (state.Analyzer.WasmModuleInfo is { } wasm)
            return BuildWasmTablesTable(ctx, state, wasm);

        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.MetadataAnalyzer.TypeRefs, query,
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

    private static Hex1bWidget BuildMemberRefsTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        if (state.Analyzer.WasmModuleInfo is { } wasm)
            return BuildWasmMemoriesTable(ctx, state, wasm);

        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.MetadataAnalyzer.MemberRefs, query,
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

    private static Hex1bWidget BuildAttributesTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        if (state.Analyzer.WasmModuleInfo is { } wasm)
            return BuildWasmGlobalsTable(ctx, state, wasm);

        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.MetadataAnalyzer.CustomAttributes, query,
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

    private static Hex1bWidget BuildResourcesTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        if (state.Analyzer.WasmModuleInfo is { } wasm)
            return BuildWasmDataSegmentsTable(ctx, state, wasm);

        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(state.MetadataAnalyzer.Resources, query,
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

    private static TableWidget<WasmTypeInfo> BuildWasmTypesTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state, WasmModuleInfo wasm)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(wasm.Types, query, t => $"type {t.Index} {FormatWasmSignature(t.ParamTypes, t.ResultTypes)}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(GetWasmTypeKey)
            .Header(h =>
            [
                h.Cell("Index").Width(SizeHint.Fixed(8)),
                h.Cell("Params").Width(SizeHint.Fill),
                h.Cell("Results").Width(SizeHint.Fill)
            ])
            .Row((r, t, rs) =>
            [
                r.Cell(c => FocusStyle(c, c.Text(t.Index.ToString()), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, FormatWasmTypes(t.ParamTypes), query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, FormatWasmTypes(t.ResultTypes), query, true, rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, t) =>
            {
                state.PeDetailContent = string.Join("\n",
                    "WebAssembly Type",
                    $"Index: {t.Index}",
                    $"Signature: {FormatWasmSignature(t.ParamTypes, t.ResultTypes)}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<WasmFunctionInfo> BuildWasmFunctionsTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state, WasmModuleInfo wasm)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(wasm.Functions, query,
            f => $"{f.Index} {f.Name} {f.NameSource} {f.ImportModule} {f.ImportName} {f.TypeIndex}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(GetWasmFunctionKey)
            .Header(h =>
            [
                h.Cell("Index").Width(SizeHint.Fixed(8)),
                h.Cell("Name").Width(SizeHint.Fill),
                h.Cell("Kind").Width(SizeHint.Fixed(9)),
                h.Cell("Type").Width(SizeHint.Fixed(8)),
                h.Cell("Offset").Width(SizeHint.Fixed(12)),
                h.Cell("Size").Width(SizeHint.Fixed(10))
            ])
            .Row((r, f, rs) =>
            [
                r.Cell(c => FocusStyle(c, c.Text(f.Index.ToString()), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, f.Name, query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(f.IsImported ? "import" : "defined"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(f.TypeIndex?.ToString() ?? ""), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, HexCell(c, f.CodeOffset is { } o ? $"0x{o:X}" : ""), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(f.CodeSize > 0 ? FormatSize(f.CodeSize, state) : ""), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, f) =>
            {
                state.PeDetailContent = string.Join("\n",
                    "WebAssembly Function",
                    $"Index: {f.Index}",
                    $"Name: {f.Name}",
                    $"Name Source: {f.NameSource}",
                    $"Kind: {(f.IsImported ? "import" : "defined")}",
                    $"Import: {(f.IsImported ? $"{f.ImportModule}!{f.ImportName}" : "(none)")}",
                    $"Type Index: {f.TypeIndex?.ToString() ?? "(none)"}",
                    $"Signature: {FormatWasmSignature(f.ParamTypes, f.ResultTypes)}",
                    $"Body Offset: {(f.BodyOffset is { } body ? $"0x{body:X}" : "(imported)")}",
                    $"Body Size: {f.BodySize}",
                    $"Code Offset: {(f.CodeOffset is { } code ? $"0x{code:X}" : "(imported)")}",
                    $"Code Size: {f.CodeSize}",
                    $"Exports: {(f.ExportNames.Count == 0 ? "(none)" : string.Join(", ", f.ExportNames))}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<WasmTableInfo> BuildWasmTablesTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state, WasmModuleInfo wasm)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(wasm.Tables, query, t => $"{t.Index} {t.RefType} {t.Minimum} {t.Maximum}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(GetWasmTableKey)
            .Header(h =>
            [
                h.Cell("Index").Width(SizeHint.Fixed(8)),
                h.Cell("Ref Type").Width(SizeHint.Fill),
                h.Cell("Minimum").Width(SizeHint.Fixed(12)),
                h.Cell("Maximum").Width(SizeHint.Fixed(12))
            ])
            .Row((r, t, rs) =>
            [
                r.Cell(c => FocusStyle(c, c.Text(t.Index.ToString()), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, t.RefType, query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(t.Minimum.ToString()), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(t.Maximum?.ToString() ?? ""), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .Compact().Fill();
    }

    private static TableWidget<WasmMemoryInfo> BuildWasmMemoriesTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state, WasmModuleInfo wasm)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(wasm.Memories, query, m => $"{m.Index} {m.MinimumPages} {m.MaximumPages} {m.IsShared} {m.IsMemory64}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(GetWasmMemoryKey)
            .Header(h =>
            [
                h.Cell("Index").Width(SizeHint.Fixed(8)),
                h.Cell("Min Pages").Width(SizeHint.Fixed(12)),
                h.Cell("Max Pages").Width(SizeHint.Fixed(12)),
                h.Cell("Shared").Width(SizeHint.Fixed(9)),
                h.Cell("Memory64").Width(SizeHint.Fixed(10))
            ])
            .Row((r, m, rs) =>
            [
                r.Cell(c => FocusStyle(c, c.Text(m.Index.ToString()), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(m.MinimumPages.ToString()), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(m.MaximumPages?.ToString() ?? ""), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(m.IsShared ? "yes" : "no"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(m.IsMemory64 ? "yes" : "no"), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .Compact().Fill();
    }

    private static TableWidget<WasmGlobalInfo> BuildWasmGlobalsTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state, WasmModuleInfo wasm)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(wasm.Globals, query, g => $"{g.Index} {g.ValueTypeName} {g.IsMutable}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(GetWasmGlobalKey)
            .Header(h =>
            [
                h.Cell("Index").Width(SizeHint.Fixed(8)),
                h.Cell("Type").Width(SizeHint.Fill),
                h.Cell("Mutable").Width(SizeHint.Fixed(10))
            ])
            .Row((r, g, rs) =>
            [
                r.Cell(c => FocusStyle(c, c.Text(g.Index.ToString()), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, g.ValueTypeName, query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(g.IsMutable ? "yes" : "no"), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .Compact().Fill();
    }

    private static TableWidget<WasmDataSegmentInfo> BuildWasmDataSegmentsTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state, WasmModuleInfo wasm)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(wasm.DataSegments, query, d => $"{d.Index} {d.Mode} {d.FileOffset:X} {d.Size}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(GetWasmDataSegmentKey)
            .Header(h =>
            [
                h.Cell("Index").Width(SizeHint.Fixed(8)),
                h.Cell("Mode").Width(SizeHint.Fill),
                h.Cell("Offset").Width(SizeHint.Fixed(14)),
                h.Cell("Size").Width(SizeHint.Fixed(12))
            ])
            .Row((r, d, rs) =>
            [
                r.Cell(c => FocusStyle(c, c.Text(d.Index.ToString()), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, d.Mode, query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c, HexCell(c, $"0x{d.FileOffset:X}"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(FormatSize(d.Size, state)), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .Compact().Fill();
    }

    private static TableWidget<WasmSectionInfo> BuildWasmCustomSectionsTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state, WasmModuleInfo wasm)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(GetWasmCustomSections(wasm), query, s => $"{s.Id} {s.Name} {s.FileOffset:X} {s.Size}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(GetWasmSectionKey)
            .Header(h =>
            [
                h.Cell("Name").Width(SizeHint.Fill),
                h.Cell("Offset").Width(SizeHint.Fixed(14)),
                h.Cell("Size").Width(SizeHint.Fixed(12))
            ])
            .Row((r, s, rs) =>
            [
                r.Cell(c => FocusHighlightCell(c, s.Name, query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c, HexCell(c, $"0x{s.FileOffset:X}"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(FormatSize(s.Size, state)), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, s) =>
            {
                state.PeDetailContent = string.Join("\n",
                    "WebAssembly Custom Section",
                    $"Name: {s.Name}",
                    $"File Offset: 0x{s.FileOffset:X}",
                    $"Payload Size: {s.Size}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<WasmElementSegmentInfo> BuildWasmElementsTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state, WasmModuleInfo wasm)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(wasm.Elements, query,
            e => $"{e.Index} {e.Mode} {e.TableIndex} {e.ElementType} {e.ElementCount}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(GetWasmElementKey)
            .Header(h =>
            [
                h.Cell("Index").Width(SizeHint.Fixed(8)),
                h.Cell("Mode").Width(SizeHint.Fill),
                h.Cell("Table").Width(SizeHint.Fixed(8)),
                h.Cell("Type").Width(SizeHint.Fixed(14)),
                h.Cell("Count").Width(SizeHint.Fixed(9))
            ])
            .Row((r, e, rs) =>
            [
                r.Cell(c => FocusStyle(c, c.Text(e.Index.ToString()), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, e.Mode, query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(e.TableIndex?.ToString() ?? ""), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, e.ElementType, query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(e.ElementCount.ToString()), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, e) =>
            {
                state.PeDetailContent = string.Join("\n",
                    "WebAssembly Element Segment",
                    $"Index: {e.Index}",
                    $"Mode: {e.Mode}",
                    $"Table: {e.TableIndex?.ToString() ?? "(implicit)"}",
                    $"Element Type: {e.ElementType}",
                    $"Element Count: {e.ElementCount}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<WasmTagInfo> BuildWasmTagsTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state, WasmModuleInfo wasm)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(wasm.Tags, query, t => $"{t.Index} {t.Attribute} {t.TypeIndex}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(GetWasmTagKey)
            .Header(h =>
            [
                h.Cell("Index").Width(SizeHint.Fixed(8)),
                h.Cell("Attribute").Width(SizeHint.Fill),
                h.Cell("Type").Width(SizeHint.Fixed(8))
            ])
            .Row((r, t, rs) =>
            [
                r.Cell(c => FocusStyle(c, c.Text(t.Index.ToString()), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, t.Attribute.ToString(), query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(t.TypeIndex.ToString()), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .Compact().Fill();
    }

    private static TableWidget<WasmModuleRow> BuildWasmModuleTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state, WasmModuleInfo wasm)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var rows = GetWasmModuleRows(wasm);
        var data = ApplySearch(rows, query, r => $"{r.Field} {r.Value}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(r => GetWasmModuleInfoKey(r.Field))
            .Header(h =>
            [
                h.Cell("Field").Width(SizeHint.Fixed(24)),
                h.Cell("Value").Width(SizeHint.Fill)
            ])
            .Row((r, row, rs) =>
            [
                r.Cell(c => FocusHighlightCell(c, row.Field, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, row.Value, query, true, rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
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
        var metadataAnalyzer = state.MetadataAnalyzer;
        return state.PeSubTab switch
        {
            PeSubTabId.Sections when analyzer.WasmModuleInfo is { } wasm => [.. ApplySearch(wasm.Sections, query,
                s => $"{s.Id} {s.Name}").Select(s => (object)GetWasmSectionKey(s))],
            PeSubTabId.Sections => [.. ApplySearch(analyzer.Sections, query,
                s => $"{s.Name} {s.Characteristics}").Select(s => (object)s.Name)],
            PeSubTabId.TypeDef when analyzer.WasmModuleInfo is { } wasm => [.. ApplySearch(wasm.Types, query,
                t => $"type {t.Index} {FormatWasmSignature(t.ParamTypes, t.ResultTypes)}").Select(t => (object)GetWasmTypeKey(t))],
            PeSubTabId.TypeDef => [.. ApplySearch(metadataAnalyzer.TypeDefs, query,
                t => $"{t.FullName} {t.BaseType} {t.Attributes}").Select(t => (object)t.Token)],
            PeSubTabId.MethodDef when analyzer.WasmModuleInfo is { } wasm => [.. ApplySearch(wasm.Functions, query,
                f => $"{f.Index} {f.Name} {f.NameSource} {f.ImportModule} {f.ImportName} {f.TypeIndex}").Select(f => (object)GetWasmFunctionKey(f))],
            PeSubTabId.MethodDef => [.. ApplySearch(metadataAnalyzer.MethodDefs, query,
                m => $"{m.DeclaringType} {m.Name} {m.Signature}").Select(m => (object)m.Token)],
            PeSubTabId.TypeRef when analyzer.WasmModuleInfo is { } wasm => [.. ApplySearch(wasm.Tables, query,
                t => $"{t.Index} {t.RefType} {t.Minimum} {t.Maximum}").Select(t => (object)GetWasmTableKey(t))],
            PeSubTabId.TypeRef => [.. ApplySearch(metadataAnalyzer.TypeRefs, query,
                t => $"{t.FullName} {t.ResolutionScope}").Select(t => (object)t.Token)],
            PeSubTabId.MemberRef when analyzer.WasmModuleInfo is { } wasm => [.. ApplySearch(wasm.Memories, query,
                m => $"{m.Index} {m.MinimumPages} {m.MaximumPages} {m.IsShared} {m.IsMemory64}").Select(m => (object)GetWasmMemoryKey(m))],
            PeSubTabId.MemberRef => [.. ApplySearch(metadataAnalyzer.MemberRefs, query,
                m => $"{m.DeclaringType} {m.Name}").Select(m => (object)m.Token)],
            PeSubTabId.Attributes when analyzer.WasmModuleInfo is { } wasm => [.. ApplySearch(wasm.Globals, query,
                g => $"{g.Index} {g.ValueTypeName} {g.IsMutable}").Select(g => (object)GetWasmGlobalKey(g))],
            PeSubTabId.Attributes => [.. ApplySearch(metadataAnalyzer.CustomAttributes, query,
                a => $"{a.Parent} {a.Constructor} {a.Value}").Select(a => (object)$"{a.Parent}|{a.Constructor}")],
            PeSubTabId.Resources when analyzer.WasmModuleInfo is { } wasm => [.. ApplySearch(wasm.DataSegments, query,
                d => $"{d.Index} {d.Mode} {d.FileOffset:X} {d.Size}").Select(d => (object)GetWasmDataSegmentKey(d))],
            PeSubTabId.Resources => [.. ApplySearch(metadataAnalyzer.Resources, query,
                r => $"{r.Name} {r.Visibility}").Select(r => (object)r.Name)],
            PeSubTabId.DebugDirectory when analyzer.WasmModuleInfo is { } wasm => [.. ApplySearch(GetWasmCustomSections(wasm), query,
                s => $"{s.Id} {s.Name} {s.FileOffset:X} {s.Size}").Select(s => (object)GetWasmSectionKey(s))],
            PeSubTabId.DebugDirectory => [.. ApplySearch(GetDebugDirectoryRows(state), query,
                r => $"{r.Origin} {r.Info.Type} {r.Info.Stamp:X8} {r.Info.Payload}").Select(r => (object)GetDebugDirectoryRowKey(r))],
            PeSubTabId.Imports when analyzer.WasmModuleInfo is { } wasm => [.. ApplySearch(wasm.Imports, query,
                i => $"{i.ModuleName} {i.Name} {i.Kind}").Select(i => (object)GetWasmImportKey(i))],
            PeSubTabId.Imports => [.. ApplySearch(GetImportRows(analyzer), query,
                r => $"{r.Module} {r.Function.Name}").Select(r => (object)r.Key)],
            PeSubTabId.Exports when analyzer.WasmModuleInfo is { } wasm => [.. ApplySearch(wasm.Exports, query,
                e => $"{e.Name} {e.Kind} {e.Index}").Select(e => (object)GetWasmExportKey(e))],
            PeSubTabId.Exports => [.. ApplySearch(analyzer.Exports, query,
                e => $"{e.Name} {e.Ordinal} {e.ForwardedTo}").Select(e => (object)e.Ordinal)],
            PeSubTabId.LoadConfig when analyzer.WasmModuleInfo is { } wasm => [.. ApplySearch(wasm.Elements, query,
                e => $"{e.Index} {e.Mode} {e.TableIndex} {e.ElementType} {e.ElementCount}").Select(e => (object)GetWasmElementKey(e))],
            PeSubTabId.LoadConfig when analyzer.LoadConfig is not null =>
                [.. ApplySearch(GetLoadConfigRows(analyzer.LoadConfig), query,
                    r => $"{r.Field} {r.Value}").Select(r => (object)r.Field)],
            PeSubTabId.RtrSections when analyzer.WasmModuleInfo is { } wasm => [.. ApplySearch(wasm.Tags, query,
                t => $"{t.Index} {t.Attribute} {t.TypeIndex}").Select(t => (object)GetWasmTagKey(t))],
            PeSubTabId.RtrSections => [.. ApplySearch(analyzer.ReadyToRunSections, query,
                s => $"{s.SectionId} {s.Name}").Select(s => (object)s.SectionId)],
            PeSubTabId.AotTypes when analyzer.WasmModuleInfo is { } wasm => [.. ApplySearch(GetWasmModuleRows(wasm), query,
                r => $"{r.Field} {r.Value}").Select(r => (object)GetWasmModuleInfoKey(r.Field))],
            PeSubTabId.AotTypes => [.. ApplySearch(analyzer.RecoveredTypes, query,
                t => t.FullName).Select(t => (object)t.FullName)],
            PeSubTabId.Symbols => [.. ApplySearch(GetSymbolRows(analyzer), query,
                s => $"{s.Name} {s.ManagedName} {s.Kind} {s.SourceFile}").Select(s => (object)s.VirtualAddress)],
            _ => []
        };
    }

    private static Hex1bWidget BuildImportsTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        if (state.Analyzer.WasmModuleInfo is { } wasm)
            return BuildWasmImportsTable(ctx, state, wasm);

        return BuildPeImportsTable(ctx, state);
    }

    private static TableWidget<WasmImportInfo> BuildWasmImportsTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state, WasmModuleInfo wasm)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(wasm.Imports, query, i => $"{i.ModuleName} {i.Name} {i.Kind}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(GetWasmImportKey)
            .Header(h =>
            [
                h.Cell("Kind").Width(SizeHint.Fixed(10)),
                h.Cell("Index").Width(SizeHint.Fixed(8)),
                h.Cell("Module").Width(SizeHint.Fixed(26)),
                h.Cell("Name").Width(SizeHint.Fill),
                h.Cell("Type").Width(SizeHint.Fixed(8))
            ])
            .Row((r, i, rs) =>
            [
                r.Cell(c => FocusStyle(c, c.Text(i.Kind.ToString()), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(i.Index.ToString()), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, i.ModuleName, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, i.Name, query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(i.TypeIndex?.ToString() ?? ""), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, i) =>
            {
                state.PeDetailContent = string.Join("\n",
                    "WebAssembly Import",
                    $"Kind: {i.Kind}",
                    $"Index: {i.Index}",
                    $"Module: {i.ModuleName}",
                    $"Name: {i.Name}",
                    $"Type Index: {i.TypeIndex?.ToString() ?? "(none)"}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<ImportRow> BuildPeImportsTable(
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

    private static Hex1bWidget BuildExportsTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        if (state.Analyzer.WasmModuleInfo is { } wasm)
            return BuildWasmExportsTable(ctx, state, wasm);

        return BuildPeExportsTable(ctx, state);
    }

    private static TableWidget<WasmExportInfo> BuildWasmExportsTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state, WasmModuleInfo wasm)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(wasm.Exports, query, e => $"{e.Name} {e.Kind} {e.Index}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(GetWasmExportKey)
            .Header(h =>
            [
                h.Cell("Kind").Width(SizeHint.Fixed(10)),
                h.Cell("Index").Width(SizeHint.Fixed(8)),
                h.Cell("Name").Width(SizeHint.Fill)
            ])
            .Row((r, e, rs) =>
            [
                r.Cell(c => FocusStyle(c, c.Text(e.Kind.ToString()), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(e.Index.ToString()), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, e.Name, query, true, rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, e) =>
            {
                state.PeDetailContent = string.Join("\n",
                    "WebAssembly Export",
                    $"Kind: {e.Kind}",
                    $"Index: {e.Index}",
                    $"Name: {e.Name}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static TableWidget<ExportedFunctionInfo> BuildPeExportsTable(
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

    private static Hex1bWidget BuildLoadConfigTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        if (state.Analyzer.WasmModuleInfo is { } wasm)
            return BuildWasmElementsTable(ctx, state, wasm);

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

    private static Hex1bWidget BuildRtrSectionsTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        if (state.Analyzer.WasmModuleInfo is { } wasm)
            return BuildWasmTagsTable(ctx, state, wasm);

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

    private static Hex1bWidget BuildAotTypesTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        if (state.Analyzer.WasmModuleInfo is { } wasm)
            return BuildWasmModuleTable(ctx, state, wasm);

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

    private static TableWidget<NativeSymbol> BuildSymbolsTable(
        WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var query = state.Search[TabId.PeMetadata].Query;
        var data = ApplySearch(GetSymbolRows(state.Analyzer), query,
            s => $"{s.Name} {s.ManagedName} {s.Kind} {s.SourceFile}");
        state.Search[TabId.PeMetadata].SetMatchCount(data.Count);

        return ctx.Table(data)
            .RowKey(s => s.VirtualAddress)
            .Header(h =>
            [
                h.Cell("Address").Width(SizeHint.Fixed(14)),
                h.Cell("RVA").Width(SizeHint.Fixed(12)),
                h.Cell("Size").Width(SizeHint.Fixed(9)),
                h.Cell("Kind").Width(SizeHint.Fixed(13)),
                h.Cell("Name").Width(SizeHint.Fill)
            ])
            .Row((r, s, rs) =>
            [
                r.Cell(c => FocusStyle(c, HexCell(c, $"0x{s.VirtualAddress:X}"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, HexCell(c, s.Rva is { } rva ? $"0x{rva:X8}" : ""), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(s.Size.ToString()), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(s.Kind.ToString()), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c, s.ManagedName ?? s.Name, query, true, rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null || state.App.FocusedNode is EditorNode ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, s) =>
            {
                var info = state.Analyzer.NativeSymbols!;
                state.PeDetailContent = string.Join("\n",
                    "Native Symbol",
                    $"Name: {s.Name}",
                    $"Managed: {s.ManagedName ?? "(no managed join)"}{(s.IsExactMatch ? " (exact)" : "")}",
                    $"Kind: {s.Kind}",
                    $"Address: 0x{s.VirtualAddress:X}"
                        + (s.Rva is { } rva ? $"  RVA: 0x{rva:X8}" : "")
                        + (s.FileOffset is { } fo ? $"  File Offset: 0x{fo:X}" : ""),
                    $"Section: {s.Section ?? "(unknown)"}",
                    $"Size: {s.Size} (0x{s.Size:X})",
                    $"Source: {(s.SourceFile is not null ? $"{s.SourceFile}:{s.Line}" : "(none)")}",
                    $"Aliases: {(s.Aliases.Count > 0 ? string.Join(", ", s.Aliases) : "(none)")}",
                    "",
                    $"Symbols From: {info.Source} ({info.Status})",
                    $"Path: {info.Path ?? "(none)"}");
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    /// <summary>The native symbols to display, or empty when the binary is managed.</summary>
    private static IReadOnlyList<NativeSymbol> GetSymbolRows(Core.Analysis.AssemblyAnalyzer analyzer) =>
        analyzer.NativeSymbols?.Symbols ?? [];

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

    /// <summary>Builds a stable key for a WebAssembly section row.</summary>
    private static string GetWasmSectionKey(WasmSectionInfo section) =>
        $"{section.FileOffset}:{section.Id}:{section.Name}";

    /// <summary>Builds a stable key for a WebAssembly type row.</summary>
    private static string GetWasmTypeKey(WasmTypeInfo type) =>
        $"wasm:type:{type.Index}";

    /// <summary>Builds a stable key for a WebAssembly function row.</summary>
    private static string GetWasmFunctionKey(WasmFunctionInfo function) =>
        $"wasm:function:{function.Index}";

    /// <summary>Builds a stable key for a WebAssembly table row.</summary>
    private static string GetWasmTableKey(WasmTableInfo table) =>
        $"wasm:table:{table.Index}";

    /// <summary>Builds a stable key for a WebAssembly memory row.</summary>
    private static string GetWasmMemoryKey(WasmMemoryInfo memory) =>
        $"wasm:memory:{memory.Index}";

    /// <summary>Builds a stable key for a WebAssembly global row.</summary>
    private static string GetWasmGlobalKey(WasmGlobalInfo global) =>
        $"wasm:global:{global.Index}";

    /// <summary>Builds a stable key for a WebAssembly data-segment row.</summary>
    private static string GetWasmDataSegmentKey(WasmDataSegmentInfo dataSegment) =>
        $"wasm:data:{dataSegment.Index}";

    /// <summary>Builds a stable key for a WebAssembly import row.</summary>
    private static string GetWasmImportKey(WasmImportInfo import) =>
        $"{import.Kind}:{import.Index}:{import.ModuleName}:{import.Name}";

    /// <summary>Builds a stable key for a WebAssembly export row.</summary>
    private static string GetWasmExportKey(WasmExportInfo export) =>
        $"{export.Kind}:{export.Index}:{export.Name}";

    /// <summary>Builds a stable key for a WebAssembly element-segment row.</summary>
    private static string GetWasmElementKey(WasmElementSegmentInfo element) =>
        $"wasm:element:{element.Index}";

    /// <summary>Builds a stable key for a WebAssembly tag row.</summary>
    private static string GetWasmTagKey(WasmTagInfo tag) =>
        $"wasm:tag:{tag.Index}";

    /// <summary>Builds a stable key for a WebAssembly module-summary row.</summary>
    private static string GetWasmModuleInfoKey(string field) =>
        $"wasm:module:{field}";

    /// <summary>Returns custom sections for the WebAssembly custom-section table.</summary>
    private static IReadOnlyList<WasmSectionInfo> GetWasmCustomSections(WasmModuleInfo wasm) =>
        [.. wasm.Sections.Where(static s => s.Id == 0)];

    /// <summary>Projects module-level WebAssembly facts into field/value rows.</summary>
    private static IReadOnlyList<WasmModuleRow> GetWasmModuleRows(WasmModuleInfo wasm) =>
    [
        new("Version", wasm.Version.ToString()),
        new("Sections", wasm.Sections.Count.ToString()),
        new("Types", wasm.Types.Count.ToString()),
        new("Functions", $"{wasm.Functions.Count} ({wasm.ImportedFunctionCount} imported, {wasm.DefinedFunctionCount} defined)"),
        new("Tables", wasm.Tables.Count.ToString()),
        new("Memories", wasm.Memories.Count.ToString()),
        new("Globals", wasm.Globals.Count.ToString()),
        new("Elements", wasm.Elements.Count.ToString()),
        new("Data", $"{wasm.DataSegments.Count} segments, {wasm.DataSize} bytes"),
        new("Tags", wasm.Tags.Count.ToString()),
        new("Start", wasm.StartFunctionIndex?.ToString() ?? "(none)"),
        new("Data Count", wasm.DataCount?.ToString() ?? "(none)"),
        new("Symbol Map", wasm.SymbolMapStatus.ToString()),
        new("Symbol Map Entries", wasm.SymbolMapEntryCount.ToString()),
        new("Symbol Map Path", wasm.SymbolMapPath ?? "(none)"),
        new("Target Features", wasm.TargetFeatures.Count == 0 ? "(none)" : string.Join(", ", wasm.TargetFeatures)),
        new("Producers", wasm.ProducerFields.Count == 0 ? "(none)" : string.Join(", ", wasm.ProducerFields)),
        new("Diagnostic", wasm.Diagnostic ?? "(none)")
    ];

    /// <summary>Formats a WebAssembly function signature for display.</summary>
    private static string FormatWasmSignature(IReadOnlyList<byte> paramTypes, IReadOnlyList<byte> resultTypes) =>
        $"({FormatWasmTypes(paramTypes)}) -> {FormatWasmTypes(resultTypes)}";

    /// <summary>Formats WebAssembly value-type bytes for display.</summary>
    private static string FormatWasmTypes(IReadOnlyList<byte> valueTypes) =>
        valueTypes.Count == 0
            ? "void"
            : string.Join(", ", valueTypes.Select(FormatWasmType));

    /// <summary>Formats a WebAssembly value-type byte for display.</summary>
    private static string FormatWasmType(byte valueType) =>
        valueType switch
        {
            0x7F => "i32",
            0x7E => "i64",
            0x7D => "f32",
            0x7C => "f64",
            0x7B => "v128",
            0x70 => "funcref",
            0x6F => "externref",
            0x68 => "i31ref",
            0x67 => "structref",
            0x66 => "arrayref",
            0x64 => "exnref",
            0x63 => "anyref",
            0x62 => "eqref",
            0x6C => "nullexternref",
            0x6D => "nullref",
            0x6E => "nullfuncref",
            _ => $"0x{valueType:X2}"
        };

    /// <summary>A single imported function flattened with its owning module.</summary>
    private sealed record ImportRow(string Module, ImportedFunctionInfo Function, string Key);

    /// <summary>A load-configuration field projected for table display.</summary>
    private sealed record LoadConfigRow(string Field, string Value);

    /// <summary>A WebAssembly module fact projected for table display.</summary>
    private sealed record WasmModuleRow(string Field, string Value);

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
