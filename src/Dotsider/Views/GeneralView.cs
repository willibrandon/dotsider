using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the General tab (Tab 1), showing assembly metadata and
/// a dependency table of referenced assemblies with drill-down navigation.
/// </summary>
public static class GeneralView
{
    private static readonly Hex1bColor LabelColor = Hex1bColor.FromRgb(100, 130, 160);

    /// <summary>
    /// Builds the General view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context for building widgets.</param>
    /// <param name="state">The shared application state.</param>
    /// <returns>The root widget for the General tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var analyzer = state.Analyzer;
        // The refs table and drill-down answer from the pre-ILC root when attached; the
        // info block keeps the native facts and appends the sidecar summary below.
        var metadataAnalyzer = state.MetadataAnalyzer;
        var routed = !ReferenceEquals(metadataAnalyzer, analyzer);
        var search = state.Search[TabId.General];
        var query = search.Query;

        // Filter assembly refs by search query
        var refs = (IReadOnlyList<AssemblyRefInfo>)metadataAnalyzer.AssemblyRefs;
        if (!string.IsNullOrEmpty(query))
        {
            refs = [.. refs
                .Where(r => $"{r.Name} {r.Version} {r.Culture} {r.PublicKeyToken}"
                    .Contains(query, StringComparison.OrdinalIgnoreCase))];
        }

        if (state.CurrentTab == TabId.General)
        {
            if (!string.IsNullOrEmpty(query))
                search.SetMatchCount(refs.Count);

            // Set up match navigation — cycle through filtered assembly refs
            if (refs.Count > 0 && !string.IsNullOrEmpty(query))
            {
                state.NavigateNextMatch = () =>
                {
                    var idx = FindRefIndex(refs, state.GeneralFocusedDep);
                    idx = (idx + 1) % refs.Count;
                    state.GeneralFocusedDep = refs[idx].Name;
                };
                state.NavigatePrevMatch = () =>
                {
                    var idx = FindRefIndex(refs, state.GeneralFocusedDep);
                    idx = idx <= 0 ? refs.Count - 1 : idx - 1;
                    state.GeneralFocusedDep = refs[idx].Name;
                };
            }
            else
            {
                state.NavigateNextMatch = null;
                state.NavigatePrevMatch = null;
            }
        }

        // Build Assembly Info text for read-only editor
        var infoLines = new List<string>
        {
            $"  Assembly Name:    {analyzer.AssemblyName ?? "(none)"}",
            $"  Version:          {analyzer.AssemblyVersion ?? "(none)"}",
            $"  Target Framework: {state.EffectiveTargetFrameworkDisplay}",
            $"  Culture:          {analyzer.Culture ?? "neutral"}",
            $"  Public Key Token: {analyzer.PublicKeyToken ?? "(none)"}",
            "",
            $"  File Size:        {state.FormatSizeToggleable(analyzer.FileSize)}",
            $"  Architecture:     {analyzer.Architecture}",
            $"  Last Modified:    {analyzer.LastModified:yyyy-MM-dd HH:mm:ss UTC}",
            $"  Created:          {analyzer.CreatedTime:yyyy-MM-dd HH:mm:ss UTC}",
            $"  Read-Only:        {(analyzer.IsReadOnly ? "Yes" : "No")}",
            $"  Has Metadata:     {(analyzer.HasMetadata ? "Yes" : "No")}",
            $"  PDB:              {analyzer.PdbProvenance}",
            $"  Source Link:      {(analyzer.SourceLink.IsPresent ? $"present, {analyzer.SourceLink.Mappings.Count} mappings" : "not present")}",
        };

        if (analyzer.NativeAotInfo is { } aot)
        {
            var imports = analyzer.Imports;
            infoLines.Add("");
            infoLines.Add("  Binary Kind:      Native AOT (.NET)");
            infoLines.Add($"  ILC / RTR Format: v{aot.MajorVersion}.{aot.MinorVersion} "
                + $"({aot.SectionCount} sections @ 0x{aot.HeaderOffset:X})");
            infoLines.Add($"  Runtime Version:  {aot.RuntimeVersion ?? "(not detected)"}");
            infoLines.Add($"  Native Imports:   {imports.Count} modules, "
                + $"{imports.Sum(m => m.Functions.Count)} functions");
            infoLines.Add($"  R2R Sections:     {analyzer.ReadyToRunSections.Count}");
            var recoveredTypes = analyzer.RecoveredTypes;
            infoLines.Add($"  Recovered Types:  {recoveredTypes.Count} types, "
                + $"{recoveredTypes.Sum(t => t.MethodNames.Count)} methods");
            infoLines.Add($"  Frozen Strings:   {analyzer.FrozenStrings.Count}");
            if (analyzer.NativeSymbols is { } symbols)
            {
                infoLines.Add(symbols.Symbols.Count > 0
                    ? $"  Native Symbols:   {symbols.Symbols.Count} from {symbols.Source}"
                    : $"  Native Symbols:   {symbols.Diagnostic ?? symbols.Status.ToString()}");
            }

            if (analyzer.PreIlcCompanions is { } companions)
            {
                state.EnsureManagedNativeIndexAsync();
                var localCount = companions.LocalReferences.Count;
                infoLines.Add("");
                infoLines.Add($"  Pre-ILC Sidecars: {companions.Root.FileName}"
                    + (localCount > 0 ? $" (+{localCount} local ref{(localCount == 1 ? "" : "s")})" : ""));
                infoLines.Add($"  Sidecar Version:  {companions.Root.AssemblyVersion ?? "(none)"}"
                    + $" ({companions.Root.TargetFramework ?? "unknown TFM"})");
                infoLines.Add($"  Sidecar PDB:      {analyzer.PreIlcSidecars?.PdbStatus.ToString() ?? "unknown"}");
                infoLines.Add(state.PreIlcIndex is { } index
                    ? $"  Correlation:      {index.ExactCount} of {index.Methods.Count} methods in native image"
                        + $" ({index.AmbiguousCount} ambiguous, {index.MstatOnlyCount} size-only,"
                        + $" {index.NotInImageCount} trimmed/inlined)"
                    : "  Correlation:      correlating IL ↔ native…");
            }
            else if (analyzer.PreIlcSidecars is { HasAttachableCompanion: true } offer)
            {
                infoLines.Add("");
                infoLines.Add($"  Pre-ILC Sidecars: found ({Path.GetFileName(offer.ManagedAssemblyPath!)})"
                    + " — press a to attach");
            }
        }

        var infoText = string.Join("\n", infoLines);

        // Border chrome adds 2 rows. The AOT layout gets one more so the editor's
        // horizontal scrollbar (long publish-dir PDB paths overflow the width)
        // doesn't cover the last info line.
        var infoHeight = infoLines.Count + (analyzer.NativeAotInfo is null ? 2 : 3);

        if (state.GeneralInfoEditorText != infoText)
        {
            state.GeneralInfoEditorText = infoText;
            state.GeneralInfoEditorState = new EditorState(new Hex1bDocument(infoText)) { IsReadOnly = true };
        }

        // Adjust word boundaries after double-click (consistent with IL Inspector)
        if (state.GeneralInfoEditorState is not null && state.CurrentTab == TabId.General)
        {
            IlInspectorView.AdjustWordSelectionCursorOneShot(
                state.GeneralInfoEditorState,
                ref state.GeneralInfoPrevSelectionAnchor,
                ref state.GeneralInfoPrevCursorPosition);
        }

        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>
            {
                // Assembly Info section (read-only editor for text selection + yank)
                outer.Border(
                    outer.ThemePanel(t => t
                        .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                        .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
                    outer.Editor(state.GeneralInfoEditorState!)
                        .ViewRenderer(InfoEditorViewRenderer.Instance)
                        .Decorations(new InfoLabelDecorationProvider())
                        .Decorations(state.GeneralInfoYankProvider)
                        .InputBindings(bindings =>
                        {
                            TextObjectHelper.ConfigureReadOnlyEditorBindings(
                                bindings,
                                state.GeneralInfoEditorState!,
                                () => state.VimPending,
                                () => state.VimPendingEditor,
                                () => state.VimPendingCursorOffset,
                                () => state.VimPendingTimestamp,
                                (s, e, o) => { state.VimPending = s; state.VimPendingEditor = e; state.VimPendingCursorOffset = o; state.VimPendingTimestamp = DateTime.UtcNow; },
                                state.PerformEditorYank,
                                () => state.App.Invalidate());
                        })
                        .FillWidth().FillHeight())
                ).Title(" Assembly Info ").FixedHeight(infoHeight)
            };

            // Search bar
            SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

            // Assembly References table
            widgets.Add(outer.Border(
                outer.Table(refs)
                    .RowKey(r => r.Name)
                    .Header(h =>
                    [
                        h.Cell("Name").Width(SizeHint.Fill),
                        h.Cell("Version").Width(SizeHint.Fixed(18)),
                        h.Cell("Culture").Width(SizeHint.Fixed(10)),
                        h.Cell("Public Key Token").Width(SizeHint.Fixed(20))
                    ])
                    .Row((r, asmRef, rs) =>
                    {
                        var flash = rs.IsFocused && state.YankFlashRow;
                        var fg = rs.IsFocused ? (flash ? YankFlashFg : FocusFg) : (Hex1bColor?)null;
                        var bg = rs.IsFocused ? (flash ? YankFlashBg : FocusBg) : (Hex1bColor?)null;
                        return
                        [
                            r.Cell(c => FocusStyle(c, HighlightHelper.HighlightCell(c, asmRef.Name, query,
                                !string.IsNullOrEmpty(query), fg, bg), rs.IsFocused, flash)),
                            r.Cell(c => FocusStyle(c, c.Text(asmRef.Version), rs.IsFocused, flash)),
                            r.Cell(c => FocusStyle(c, c.Text(asmRef.Culture), rs.IsFocused, flash)),
                            r.Cell(c => FocusStyle(c, c.Text(asmRef.PublicKeyToken ?? ""), rs.IsFocused, flash))
                        ];
                    })
                    .Focus(state.App.FocusedNode is EditorNode
                        ? null : state.GeneralFocusedDep)
                    .OnFocusChanged(key => state.GeneralFocusedDep = key)
                    .OnRowActivated((_, asmRef) =>
                    {
                        // Use full identity so net48 roots route through NetFxBinder and
                        // produce the same answer as the Dep Graph and IL navigation.
                        // The routed analyzer's own context resolves its references.
                        var resolution = AssemblyAnalyzer.ResolveAssemblyByIdentity(
                            state.MetadataAnalyzer.FilePath, asmRef,
                            state.MetadataAnalyzer.TargetFramework,
                            state.MetadataAnalyzer.PreferredRuntimePack,
                            state.MetadataAnalyzer.SourceBundlePath,
                            state.RootNetFxBindingContext);
                        if (resolution.Resolved is not null)
                        {
                            state.PushAssembly(resolution.Resolved);
                            state.App.Invalidate();
                        }
                    })
                    .Compact()
                    .Empty(e => e.Text("  No assembly references"))
                    .Fill()
                    .InputBindings(bindings =>
                    {
                        bindings.Key(Hex1bKey.Enter).Action(_ =>
                        {
                            var focusedName = state.GeneralFocusedDep as string
                                ?? (metadataAnalyzer.AssemblyRefs.Count > 0 ? metadataAnalyzer.AssemblyRefs[0].Name : null);
                            if (focusedName is null) return;
                            // Look up the AssemblyRef matching the focused simple name so the
                            // bind has full identity. Net48 roots route through NetFxBinder.
                            var asmRef = metadataAnalyzer.AssemblyRefs.FirstOrDefault(
                                r => string.Equals(r.Name, focusedName, StringComparison.OrdinalIgnoreCase));
                            if (asmRef is null) return;
                            var resolution = AssemblyAnalyzer.ResolveAssemblyByIdentity(
                                state.MetadataAnalyzer.FilePath, asmRef,
                                state.MetadataAnalyzer.TargetFramework,
                                state.MetadataAnalyzer.PreferredRuntimePack,
                                state.MetadataAnalyzer.SourceBundlePath,
                                state.RootNetFxBindingContext);
                            if (resolution.Resolved is not null)
                            {
                                state.PushAssembly(resolution.Resolved);
                                state.App.Invalidate();
                            }
                        }, "Drill into reference");
                    })
            ).Title(routed
                ? $" Assembly References (pre-ILC) ({refs.Count}) "
                : $" Assembly References ({refs.Count}) ").Fill());

            return [.. widgets];
        })
        .InputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.Tab).Global().Action(_ =>
            {
                state.VimPending = VimMotionState.Idle;
                if (state.App.FocusedNode is EditorNode)
                {
                    // Editor → Table: seed focus to first row if none selected
                    state.GeneralFocusedDep ??=
                        state.MetadataAnalyzer.AssemblyRefs.Count > 0
                            ? state.MetadataAnalyzer.AssemblyRefs[0].Name : null;
                    state.App.RequestFocus(node =>
                        node.GetType().Name.StartsWith("TableNode"));
                    state.App.Invalidate();
                }
                else
                {
                    // Table → Editor
                    state.App.RequestFocus(node => node is EditorNode);
                    state.App.Invalidate();
                }
            }, "Toggle focus");

            bindings.Key(Hex1bKey.Escape).OverridesCapture().Action(_ =>
            {
                if (search.IsActive)
                {
                    search.Dismiss();
                    state.App.Invalidate();
                }
            }, "Esc");

            var isSearchEditing = search.IsActive && !search.IsConfirmed;
            if (!isSearchEditing && !state.ModalDialogOpen)
            {
                // a: re-open the sidecar offer after a decline; d: detach an attached set.
                if (state.Analyzer.PreIlcSidecars is { HasAttachableCompanion: true }
                    && state.Analyzer.PreIlcCompanions is null)
                {
                    bindings.Key(Hex1bKey.A).Global().Action(_ =>
                    {
                        state.VimPending = VimMotionState.Idle;
                        state.PreIlcDialogOpen = true;
                        state.App.Invalidate();
                    }, "Attach sidecar");
                }

                if (state.Analyzer.PreIlcCompanions is not null)
                {
                    bindings.Key(Hex1bKey.D).Global().Action(_ =>
                    {
                        state.VimPending = VimMotionState.Idle;
                        state.DetachPreIlc();
                        state.App.Invalidate();
                    }, "Detach sidecar");
                }
            }
        })
        .Fill();
    }

    private static int FindRefIndex(IReadOnlyList<AssemblyRefInfo> refs, object? focusedKey)
    {
        if (focusedKey is not string key) return -1;
        for (var i = 0; i < refs.Count; i++)
        {
            if (refs[i].Name == key)
                return i;
        }
        return -1;
    }

    private static readonly Hex1bColor FocusFg = Hex1bColor.Black;
    private static readonly Hex1bColor FocusBg = Hex1bColor.FromRgb(0, 200, 180);
    private static readonly Hex1bColor YankFlashFg = Hex1bColor.FromRgb(24, 24, 37);
    private static readonly Hex1bColor YankFlashBg = Hex1bColor.FromRgb(126, 201, 216);

    private static Hex1bWidget FocusStyle<T>(WidgetContext<T> c, Hex1bWidget child, bool isFocused,
        bool yankFlash = false) where T : Hex1bWidget
    {
        if (!isFocused) return child;
        var fg = yankFlash ? YankFlashFg : FocusFg;
        var bg = yankFlash ? YankFlashBg : FocusBg;
        return c.ThemePanel(t => t
            .Set(GlobalTheme.ForegroundColor, fg)
            .Set(GlobalTheme.BackgroundColor, bg), child);
    }

    private static HStackWidget InfoLine<T>(WidgetContext<T> ctx, string label, string value) where T : Hex1bWidget
    {
        return ctx.HStack(row =>
        [
            row.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, LabelColor),
                row.Text($"  {label}: ")).FixedWidth(22),
            row.Text(value)
        ]).FixedHeight(1);
    }
}
