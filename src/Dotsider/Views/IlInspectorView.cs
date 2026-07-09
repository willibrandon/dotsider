using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Analysis.ReadyToRun;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the IL Inspector tab (Tab 3), displaying a namespace/type/method
/// hierarchy tree on the left and the selected method's IL disassembly on the right.
/// </summary>
public static class IlInspectorView
{
    /// <summary>
    /// Builds the IL Inspector view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context from the parent tab panel.</param>
    /// <param name="state">The shared application state.</param>
    /// <returns>The root widget for the IL Inspector tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var search = state.Search[TabId.IlInspector];
        var query = search.Query;

        if (state.CurrentTab == TabId.IlInspector)
        {
            // Two-phase search:
            //   During typing (not confirmed): tree filters by name/type only (cheap).
            //   After confirm: compute text-level matches once, set up n/N navigation.
            // With a pre-ILC set attached, search follows the FOCUSED PANE, not the tree
            // mode: the native pair pane searches its own listing; everything else runs
            // the managed pipeline.
            var attached = state.Analyzer.PreIlcCompanions is not null;
            var nativeTextSearch = attached
                ? state.IlFocusedPane == IlPane.Native && state.IlPairNativeEditorState is not null
                : !state.Analyzer.HasManagedMetadata;
            if (!string.IsNullOrEmpty(query) && search.IsConfirmed)
            {
                if (nativeTextSearch)
                {
                    // Native listing: match the query against the pane's text and step the
                    // cursor through the hits with n/N (the search provider highlights all).
                    var nativeSearchEditor = attached ? state.IlPairNativeEditorState : state.IlEditorState;
                    if (query != state.IlLastSearchQuery || !state.IlSearchScopeNative)
                    {
                        state.IlNativeSearchOffsets = CollectNativeMatches(nativeSearchEditor, query);
                        state.IlLastSearchQuery = query;
                        state.IlCurrentMatchIndex = -1;
                        state.IlSearchScopeNative = true;
                    }

                    search.SetMatchCount(state.IlNativeSearchOffsets.Count);
                    if (state.IlNativeSearchOffsets.Count > 0)
                    {
                        state.NavigateNextMatch = () => NavigateToNativeMatch(state, forward: true);
                        state.NavigatePrevMatch = () => NavigateToNativeMatch(state, forward: false);
                    }
                    else
                    {
                        state.NavigateNextMatch = null;
                        state.NavigatePrevMatch = null;
                    }
                }
                else
                {
                    if (query != state.IlLastSearchQuery || state.IlSearchScopeNative)
                    {
                        state.IlSearchMatches = CollectTextMatches(state, query);
                        state.IlTextMatchMethodTokens = [.. state.IlSearchMatches.Select(m => (m.Owner, m.Method.Token))];
                        state.IlLastSearchQuery = query;
                        state.IlCurrentMatchIndex = -1;
                        state.IlSearchScopeNative = false;
                    }

                    search.SetMatchCount(state.IlSearchMatches.Count);

                    if (state.IlSearchMatches.Count > 0)
                    {
                        state.NavigateNextMatch = () =>
                        {
                            state.IlCurrentMatchIndex = (state.IlCurrentMatchIndex + 1) % state.IlSearchMatches.Count;
                            NavigateToMatch(state, state.IlSearchMatches[state.IlCurrentMatchIndex]);
                        };
                        state.NavigatePrevMatch = () =>
                        {
                            state.IlCurrentMatchIndex = state.IlCurrentMatchIndex <= 0
                                ? state.IlSearchMatches.Count - 1
                                : state.IlCurrentMatchIndex - 1;
                            NavigateToMatch(state, state.IlSearchMatches[state.IlCurrentMatchIndex]);
                        };
                    }
                    else
                    {
                        state.NavigateNextMatch = null;
                        state.NavigatePrevMatch = null;
                    }
                }
            }
            else
            {
                state.NavigateNextMatch = null;
                state.NavigatePrevMatch = null;

                // Clear confirmed search state when query changes during typing
                if (state.IlLastSearchQuery is not null && (!search.IsActive || !search.IsConfirmed))
                {
                    state.IlLastSearchQuery = null;
                    state.IlSearchMatches = [];
                    state.IlNativeSearchOffsets = [];
                    state.IlCurrentMatchIndex = -1;
                    state.IlTextMatchMethodTokens = null;
                }
            }

            // Update search decoration providers. The IL provider always carries the query
            // (passive highlight when the native pane owns the search); the pair provider
            // mirrors it so both panes highlight occurrences, with the current-match marker
            // only in the pane that owns n/N.
            state.IlSearchProvider.Query = search.IsActive ? query : null;
            state.IlPairSearchProvider.Query = search.IsActive && attached ? query : null;
            var nativeMatchEditor = attached ? state.IlPairNativeEditorState : state.IlEditorState;
            var nativeMatchProvider = attached ? state.IlPairSearchProvider : state.IlSearchProvider;
            if (!attached || state.IlCurrentMatchIndex < 0)
            {
                state.IlPairSearchProvider.CurrentMatchStart = null;
                state.IlPairSearchProvider.CurrentMatchLength = 0;
            }

            if (state.IlCurrentMatchIndex >= 0 && state.IlCurrentMatchIndex < state.IlSearchMatches.Count)
            {
                var currentMatch = state.IlSearchMatches[state.IlCurrentMatchIndex];
                state.IlSearchProvider.CurrentMatchStart = new DocumentPosition(currentMatch.Line, currentMatch.Column);
                state.IlSearchProvider.CurrentMatchLength = currentMatch.Length;
            }
            else if (state.IlCurrentMatchIndex >= 0
                && state.IlCurrentMatchIndex < state.IlNativeSearchOffsets.Count
                && nativeMatchEditor is { } nativeEditor)
            {
                nativeMatchProvider.CurrentMatchStart =
                    nativeEditor.Document.OffsetToPosition(new DocumentOffset(state.IlNativeSearchOffsets[state.IlCurrentMatchIndex]));
                nativeMatchProvider.CurrentMatchLength = state.IlLastSearchQuery?.Length ?? 0;
                if (attached)
                {
                    state.IlSearchProvider.CurrentMatchStart = null;
                    state.IlSearchProvider.CurrentMatchLength = 0;
                }
            }
            else
            {
                state.IlSearchProvider.CurrentMatchStart = null;
                state.IlSearchProvider.CurrentMatchLength = 0;
            }

            // Mouse clicks move focus between panes; keep the pane tracker in sync so
            // search and key dispatch follow the editor the user clicked into.
            if (attached)
            {
                if (state.App.FocusedNode is EditorNode focusedEditor)
                {
                    if (state.IlEditorState is not null && ReferenceEquals(focusedEditor.State, state.IlEditorState))
                        state.IlFocusedPane = IlPane.Il;
                    else if (state.IlPairNativeEditorState is not null
                        && ReferenceEquals(focusedEditor.State, state.IlPairNativeEditorState))
                        state.IlFocusedPane = IlPane.Native;
                }
                else if (state.App.FocusedNode is ScrollPanelNode)
                {
                    state.IlFocusedPane = IlPane.Tree;
                }
            }
        }

        // Build the flattened tree rows for the left pane list — native symbols for a
        // metadata-less binary (or the attached-mode native toggle), managed methods otherwise.
        var isNative = IsNativeTreeMode(state);
        var treeRows = isNative ? BuildNativeTreeRows(state) : BuildTreeRows(state);
        var formattedRows = treeRows.Select(r => FormatTreeRow(r, state)).ToList();

        SyncTreeScroll(state, treeRows);

        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>();

            // Search bar (shared helper)
            SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

            // Main content: HSplitter with tree list on left, disassembly editor on right
            widgets.Add(outer.HSplitter(
                // Left pane: windowed tree list (panel hosts the visible rows; a
                // standalone scrollbar gutter appears when content overflows)
                left =>
                [
                    IlTreeList.Build(
                        treeRows,
                        formattedRows,
                        getRows: () => IsNativeTreeMode(state) ? BuildNativeTreeRows(state) : BuildTreeRows(state),
                        state,
                        selectionChanged: row =>
                        {
                            // Direct assignment on keyboard/click moves does NOT arm
                            // IlScrollSelectionIntoViewPending. The keyboard handler
                            // calls EnsureSelectionVisible inline, so the pending
                            // path is reserved for external setters.
                            state.IlFocusedTreeKey = row.Key;
                            if (row is { Kind: IlTreeRowKind.Method, Method: not null })
                            {
                                state.IlSelectedMethod = row.Method;
                                state.IlSelectedMethodOwner = row.Owner;
                            }
                            else if (row is { Kind: IlTreeRowKind.Method, Symbol: not null })
                            {
                                state.IlSelectedNativeSymbol = row.Symbol;
                            }

                            state.App.Invalidate();
                        },
                        itemActivated: row =>
                        {
                            ActivateTreeRow(row, state);
                            state.App.Invalidate();
                        },
                        expandRow: row =>
                        {
                            if (row is { CanExpand: true, IsExpanded: false })
                            {
                                state.IlTreeExpansionState[row.ExpansionKey] = true;
                                state.App.Invalidate();
                            }
                        },
                        collapseRow: row =>
                        {
                            if (row is { CanExpand: true, IsExpanded: true })
                            {
                                state.IlTreeExpansionState[row.ExpansionKey] = false;
                                state.App.Invalidate();
                            }
                        })
                ],
                // Right pane: IL disassembly — with a pre-ILC set attached, IL and
                // native code side by side for correlated methods.
                right => BuildRightPane(right, state),
                leftWidth: 35).FillWidth().FillHeight());

            return [.. widgets];
        })
        .InputBindings(bindings =>
        {
            // Escape: search dismiss OR IL back navigation (local binding, not global)
            bindings.Key(Hex1bKey.Escape).OverridesCapture().Action(_ =>
            {
                if (search.IsActive)
                {
                    search.Dismiss();
                    state.IlSearchProvider.Query = null;
                    state.IlSearchProvider.CurrentMatchStart = null;
                    state.IlPairSearchProvider.Query = null;
                    state.IlPairSearchProvider.CurrentMatchStart = null;
                    state.App.Invalidate();
                }
                else if (state.IlBackStack.Count > 0)
                {
                    var entry = state.IlBackStack.Pop();
                    state.RestoreFromIlBackEntry(entry);
                }
            }, "Esc");

            // Tab cycles tree → IL → native when the pair pane exists.
            if (state.Analyzer.PreIlcCompanions is not null && !search.IsActive)
            {
                bindings.Key(Hex1bKey.Tab).Global().Action(_ => CyclePane(state), "Cycle panes");
            }
        })
        .FillWidth().FillHeight();
    }

    /// <summary>
    /// Whether the IL tree renders native symbols: always for a metadata-less binary with
    /// no pre-ILC companions, and on demand (the <c>t</c> toggle) when a set is attached.
    /// </summary>
    /// <param name="state">The shared application state.</param>
    internal static bool IsNativeTreeMode(DotsiderState state) =>
        !state.Analyzer.HasManagedMetadata
        && (state.Analyzer.PreIlcCompanions is null || state.IlAotTreeNativeView);

    /// <summary>Advances the focused pane: tree → IL → native → tree, skipping absent panes.</summary>
    /// <param name="state">The shared application state.</param>
    internal static void CyclePane(DotsiderState state)
    {
        var hasIl = state.IlEditorState is not null;
        var hasNative = state.IlPairNativeEditorState is not null;
        var next = state.IlFocusedPane switch
        {
            IlPane.Tree when hasIl => IlPane.Il,
            IlPane.Tree when hasNative => IlPane.Native,
            IlPane.Il when hasNative => IlPane.Native,
            _ => IlPane.Tree,
        };
        FocusPane(state, next);
    }

    /// <summary>Moves keyboard focus to a specific pane and records it as the search owner.</summary>
    /// <param name="state">The shared application state.</param>
    /// <param name="pane">The pane to focus.</param>
    internal static void FocusPane(DotsiderState state, IlPane pane)
    {
        state.IlFocusedPane = pane;
        switch (pane)
        {
            case IlPane.Il when state.IlEditorState is not null:
                state.App.RequestFocus(node =>
                    node is EditorNode e && ReferenceEquals(e.State, state.IlEditorState));
                break;
            case IlPane.Native when state.IlPairNativeEditorState is not null:
                state.App.RequestFocus(node =>
                    node is EditorNode e && ReferenceEquals(e.State, state.IlPairNativeEditorState));
                break;
            default:
                state.RequestContentFocus();
                break;
        }

        state.App.Invalidate();
    }

    /// <summary>
    /// Builds a flattened list of tree rows from the namespace → type → method hierarchy,
    /// respecting expansion state and search filtering. With a multi-assembly pre-ILC set
    /// attached, top-level assembly rows group each member; the root assembly keeps the
    /// plain key shapes every navigation site constructs, and local references get
    /// assembly-prefixed keys so tokens never collide across assemblies.
    /// </summary>
    internal static List<IlTreeRow> BuildTreeRows(DotsiderState state)
    {
        var rows = new List<IlTreeRow>();
        var set = state.Analyzer.PreIlcCompanions;

        if (set is null || set.LocalReferences.Count == 0)
        {
            AppendManagedRows(rows, state, state.MetadataAnalyzer, owner: null, keyPrefix: "", baseDepth: 0);
            return rows;
        }

        var searchQuery = state.Search[TabId.IlInspector].Query;
        foreach (var member in set.All)
        {
            var isRoot = ReferenceEquals(member, set.Root);
            var asmName = member.AssemblyName ?? member.FileName;
            var asmKey = $"asm:{asmName}";
            var expanded = GetExpansionState(state, asmKey, defaultExpanded: isRoot);
            rows.Add(new IlTreeRow(asmKey, 0, IlTreeRowKind.Assembly,
                HighlightHelper.HighlightSubstring(asmName, searchQuery),
                null, CanExpand: true, IsExpanded: expanded, ExpansionKey: asmKey));
            if (!expanded) continue;

            AppendManagedRows(rows, state, member,
                owner: isRoot ? null : member,
                keyPrefix: isRoot ? "" : $"{asmName}|",
                baseDepth: 1);
        }

        return rows;
    }

    private static void AppendManagedRows(
        List<IlTreeRow> rows, DotsiderState state, Dotsider.Core.Analysis.AssemblyAnalyzer analyzer,
        Dotsider.Core.Analysis.AssemblyAnalyzer? owner, string keyPrefix, int baseDepth)
    {
        var search = state.Search[TabId.IlInspector];
        var searchQuery = search.Query;

        var methodsByType = analyzer.MethodDefs
            .GroupBy(m => m.DeclaringType)
            .ToDictionary(g => g.Key, g => g.ToList());

        var typesByNamespace = analyzer.TypeDefs
            .GroupBy(td => string.IsNullOrEmpty(td.Namespace) ? "(global)" : td.Namespace)
            .OrderBy(g => g.Key);

        foreach (var nsGroup in typesByNamespace)
        {
            var nsTypes = nsGroup.ToList();

            // Apply search filter
            if (!string.IsNullOrEmpty(searchQuery))
            {
                nsTypes = [.. nsTypes.Where(td =>
                    td.FullName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    (methodsByType.TryGetValue(td.FullName, out var methods) &&
                     methods.Any(m => m.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                         (search.IsConfirmed && state.IlTextMatchMethodTokens?.Contains((owner, m.Token)) == true))))];

                if (nsTypes.Count == 0) continue;
            }

            var nsKey = $"ns:{keyPrefix}{nsGroup.Key}";
            var nsExpanded = GetExpansionState(state, nsKey, defaultExpanded: true);
            var nsLabel = HighlightHelper.HighlightSubstring(nsGroup.Key, searchQuery);

            rows.Add(new IlTreeRow(nsKey, baseDepth, IlTreeRowKind.Namespace, nsLabel,
                null, CanExpand: true, IsExpanded: nsExpanded, ExpansionKey: nsKey));

            if (!nsExpanded) continue;

            foreach (var typeDef in nsTypes)
            {
                if (!methodsByType.TryGetValue(typeDef.FullName, out var methods))
                    continue;

                var filteredMethods = methods;
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    filteredMethods = [.. methods
                        .Where(m => m.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                    typeDef.FullName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                    (search.IsConfirmed && state.IlTextMatchMethodTokens?.Contains((owner, m.Token)) == true))];

                    if (filteredMethods.Count == 0) continue;
                }

                var typeKey = $"type:{keyPrefix}{typeDef.FullName}";
                var typeExpanded = GetExpansionState(state, typeKey, defaultExpanded: false);
                var typeLabel = HighlightHelper.HighlightSubstring(typeDef.Name, searchQuery);

                rows.Add(new IlTreeRow(typeKey, baseDepth + 1, IlTreeRowKind.Type, typeLabel,
                    null, CanExpand: true, IsExpanded: typeExpanded, ExpansionKey: typeKey));

                if (!typeExpanded) continue;

                foreach (var m in filteredMethods)
                {
                    var methodKey = $"method:{keyPrefix}{m.Token}";
                    var methodText = $"{m.Name}{m.Signature}";
                    var methodLabel = HighlightHelper.HighlightSubstring(methodText, searchQuery);

                    rows.Add(new IlTreeRow(methodKey, baseDepth + 2, IlTreeRowKind.Method, methodLabel,
                        m, CanExpand: false, IsExpanded: false, ExpansionKey: "", Owner: owner));
                }
            }
        }
    }

    /// <summary>
    /// Builds the native IL-inspector tree for a non-managed (Native AOT) binary: the executable
    /// symbols (functions, stubs, unwind boundaries) bucketed namespace → type → function the same
    /// way the managed tree buckets methods, using <see cref="NativeSymbolName.Parse"/> for managed-
    /// named functions and synthetic buckets — <c>(runtime)</c>, <c>(stubs)</c>, <c>(functions)</c> —
    /// for the rest. Method rows carry their <see cref="NativeSymbol"/> in <see cref="IlTreeRow.Symbol"/>.
    /// </summary>
    /// <param name="state">The current application state.</param>
    internal static List<IlTreeRow> BuildNativeTreeRows(DotsiderState state)
    {
        if (state.Analyzer.WasmModuleInfo is { } wasm)
            return BuildWasmTreeRows(state, wasm);

        var rows = new List<IlTreeRow>();
        var info = state.Analyzer.NativeSymbols;
        if (info is null) return rows;

        var search = state.Search[TabId.IlInspector];
        var query = search.Query ?? string.Empty;

        // Bucket every executable symbol into (namespace, type, member).
        var buckets = new SortedDictionary<string, SortedDictionary<string, List<(string Member, NativeSymbol Symbol)>>>(StringComparer.Ordinal);
        foreach (var s in info.Symbols)
        {
            if (s.Kind is not (NativeSymbolKind.Function or NativeSymbolKind.Stub or NativeSymbolKind.Boundary))
                continue;

            string ns, type, member;
            if (s.Kind == NativeSymbolKind.Function && s.ManagedName is { } managed)
            {
                var parsed = NativeSymbolName.Parse(managed);
                ns = parsed.Namespace.Length == 0 ? "(global)" : parsed.Namespace;
                type = parsed.TypeName.Length == 0 ? "(functions)" : parsed.TypeName;
                member = parsed.MemberName;
            }
            else
            {
                ns = s.Kind == NativeSymbolKind.Stub ? "(stubs)"
                    : s.Kind == NativeSymbolKind.Function ? "(runtime)" : "(functions)";
                type = ns;
                member = s.Name;
            }

            if (query.Length > 0 && !member.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !type.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!buckets.TryGetValue(ns, out var byType))
                byType = buckets[ns] = new SortedDictionary<string, List<(string, NativeSymbol)>>(StringComparer.Ordinal);
            if (!byType.TryGetValue(type, out var list))
                list = byType[type] = [];
            list.Add((member, s));
        }

        foreach (var (ns, byType) in buckets)
        {
            var nsKey = $"nns:{ns}";
            var nsExpanded = GetExpansionState(state, nsKey, defaultExpanded: query.Length > 0);
            rows.Add(new IlTreeRow(nsKey, 0, IlTreeRowKind.Namespace,
                HighlightHelper.HighlightSubstring(ns, query), null, CanExpand: true, IsExpanded: nsExpanded, nsKey));
            if (!nsExpanded) continue;

            foreach (var (type, members) in byType)
            {
                var typeKey = $"ntype:{ns}/{type}";
                var typeExpanded = GetExpansionState(state, typeKey, defaultExpanded: query.Length > 0);
                rows.Add(new IlTreeRow(typeKey, 1, IlTreeRowKind.Type,
                    HighlightHelper.HighlightSubstring(type, query), null, CanExpand: true, IsExpanded: typeExpanded, typeKey));
                if (!typeExpanded) continue;

                foreach (var (member, symbol) in members.OrderBy(m => m.Symbol.VirtualAddress))
                {
                    var funcKey = $"nfunc:{symbol.VirtualAddress:x}";
                    rows.Add(new IlTreeRow(funcKey, 2, IlTreeRowKind.Method,
                        HighlightHelper.HighlightSubstring(member, query), null, CanExpand: false, IsExpanded: false, "", symbol));
                }
            }
        }

        return rows;
    }

    private static List<IlTreeRow> BuildWasmTreeRows(DotsiderState state, WasmModuleInfo wasm)
    {
        var rows = new List<IlTreeRow>();
        var query = state.Search[TabId.IlInspector].Query ?? string.Empty;
        IReadOnlyDictionary<long, NativeSymbol> symbolByOffset = state.Analyzer.NativeSymbols?.Symbols
            .Where(static s => s.FileOffset is not null)
            .ToDictionary(static s => s.FileOffset!.Value)
            ?? [];

        AddWasmImportRows(state, rows, wasm, query);
        AddWasmFunctionGroup(state, rows, wasm, symbolByOffset, query,
            "(exports)",
            static f => !f.IsImported && f.IsExported,
            static f => $"func[{f.Index}] {f.ExportNames[0]} -> {f.Name}");
        AddWasmFunctionGroup(state, rows, wasm, symbolByOffset, query,
            "(functions)",
            static f => !f.IsImported && !f.IsExported && f.NameSource != "synthetic",
            static f => $"func[{f.Index}] {f.Name}");
        AddWasmFunctionGroup(state, rows, wasm, symbolByOffset, query,
            "(synthetic)",
            static f => !f.IsImported && !f.IsExported && f.NameSource == "synthetic",
            static f => $"func[{f.Index}] {f.Name}");

        return rows;
    }

    private static void AddWasmImportRows(
        DotsiderState state,
        List<IlTreeRow> rows,
        WasmModuleInfo wasm,
        string query)
    {
        var imports = wasm.Functions
            .Where(static f => f.IsImported)
            .Where(f => WasmFunctionMatches(f, query))
            .OrderBy(static f => f.Index)
            .ToList();
        if (imports.Count == 0) return;

        var groupKey = "wasm:imports";
        var expanded = GetExpansionState(state, groupKey, defaultExpanded: query.Length > 0);
        rows.Add(new IlTreeRow(groupKey, 0, IlTreeRowKind.Namespace,
            "(imports)", null, CanExpand: true, IsExpanded: expanded, groupKey));
        if (!expanded) return;

        foreach (var byModule in imports.GroupBy(static f => f.ImportModule ?? "(module)", StringComparer.Ordinal))
        {
            var moduleKey = $"wasm:import-module:{byModule.Key}";
            var moduleExpanded = GetExpansionState(state, moduleKey, defaultExpanded: query.Length > 0);
            rows.Add(new IlTreeRow(moduleKey, 1, IlTreeRowKind.Type,
                HighlightHelper.HighlightSubstring(byModule.Key, query), null,
                CanExpand: true, IsExpanded: moduleExpanded, moduleKey));
            if (!moduleExpanded) continue;

            foreach (var function in byModule)
            {
                var label = $"func[{function.Index}] {function.ImportName ?? function.Name}";
                rows.Add(new IlTreeRow($"wasm:import:{function.Index}", 2, IlTreeRowKind.Method,
                    HighlightHelper.HighlightSubstring(label, query), null,
                    CanExpand: false, IsExpanded: false, ""));
            }
        }
    }

    private static void AddWasmFunctionGroup(
        DotsiderState state,
        List<IlTreeRow> rows,
        WasmModuleInfo wasm,
        IReadOnlyDictionary<long, NativeSymbol> symbolByOffset,
        string query,
        string groupName,
        Func<WasmFunctionInfo, bool> predicate,
        Func<WasmFunctionInfo, string> label)
    {
        var functions = wasm.Functions
            .Where(predicate)
            .Where(f => f.CodeOffset is not null && symbolByOffset.ContainsKey(f.CodeOffset.Value))
            .Where(f => WasmFunctionMatches(f, query))
            .OrderBy(static f => f.Index)
            .ToList();
        if (functions.Count == 0) return;

        var groupKey = $"wasm:{groupName}";
        var expanded = GetExpansionState(state, groupKey, defaultExpanded: query.Length > 0);
        rows.Add(new IlTreeRow(groupKey, 0, IlTreeRowKind.Namespace,
            groupName, null, CanExpand: true, IsExpanded: expanded, groupKey));
        if (!expanded) return;

        foreach (var function in functions)
        {
            var symbol = symbolByOffset[function.CodeOffset!.Value];
            var rowLabel = label(function);
            rows.Add(new IlTreeRow($"wasm:func:{function.Index}", 1, IlTreeRowKind.Method,
                HighlightHelper.HighlightSubstring(rowLabel, query), null,
                CanExpand: false, IsExpanded: false, "", symbol));
        }
    }

    private static bool WasmFunctionMatches(WasmFunctionInfo function, string query)
    {
        if (query.Length == 0) return true;
        return function.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || function.ImportModule?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
            || function.ImportName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
            || function.ExportNames.Any(e => e.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Formats a tree row for display in the table cell, adding indentation,
    /// expand/collapse glyphs, and the selection marker.
    /// </summary>
    private static string FormatTreeRow(IlTreeRow row, DotsiderState state)
    {
        var indent = new string(' ', row.Depth * 2);
        var glyph = row.CanExpand
            ? (row.IsExpanded ? "▼ " : "▶ ")
            : "  ";
        var marker = row.Kind switch
        {
            // row.Method must be non-null: native leaves carry a null Method, and with
            // no managed selection `null == null` would mark every native row.
            IlTreeRowKind.Method when row.Method is { } m && m.Token == state.IlSelectedMethod?.Token
                && ReferenceEquals(row.Owner, state.IlSelectedMethodOwner) => "● ",
            IlTreeRowKind.Method when row.Symbol is { } nsym
                && nsym.VirtualAddress == state.IlSelectedNativeSymbol?.VirtualAddress => "● ",
            IlTreeRowKind.Type when row.Method is null
                && state.IlSelectedMethod?.DeclaringType is { } dt
                && row.Key.EndsWith($"type:{dt}", StringComparison.Ordinal) => "● ",
            IlTreeRowKind.Namespace when state.IlSelectedMethod is { } sm
                && IsMethodInNamespace(sm, row.Label, state) => "● ",
            _ => ""
        };
        return $"{indent}{glyph}{marker}{CorrelationGlyph(row, state)}{row.Label}";
    }

    /// <summary>
    /// The per-method correlation glyph shown when a pre-ILC set is attached: owned
    /// evidence (✓), shared with overloads (~), mstat size only (±), or absent from the
    /// native image (–). Empty while the index is still building.
    /// </summary>
    internal static string CorrelationGlyph(IlTreeRow row, DotsiderState state)
    {
        if (row is not { Kind: IlTreeRowKind.Method, Method: { } method }) return "";

        // ReadyToRun: precompiled (✓) vs IL-only (–), keyed off the R2R index.
        if (state.Analyzer.IsReadyToRun)
        {
            if (state.Analyzer.ReadyToRunIndex is not { } r2rIndex) return "";
            var ownerName = (row.Owner ?? state.MetadataAnalyzer).AssemblyName ?? "";
            return r2rIndex.Find(ownerName, method.Token) is not null ? "✓ " : "– ";
        }

        if (state.Analyzer.PreIlcCompanions is not { } companions || state.PreIlcIndex is not { } index)
            return "";

        var pilcOwnerName = (row.Owner ?? companions.Root).AssemblyName ?? "";
        return index.Find(pilcOwnerName, method.Token)?.Status switch
        {
            MethodCorrelationStatus.CorrelatedExact => "✓ ",
            MethodCorrelationStatus.CorrelatedAmbiguous => "~ ",
            MethodCorrelationStatus.CorrelatedByMstatOnly => "± ",
            MethodCorrelationStatus.NotInNativeImage => "– ",
            _ => "",
        };
    }

    /// <summary>
    /// Checks whether the given method belongs to the namespace identified by label.
    /// </summary>
    private static bool IsMethodInNamespace(MethodDefInfo method, string nsLabel, DotsiderState state)
    {
        var td = state.MetadataAnalyzer.TypeDefs.FirstOrDefault(t => t.FullName == method.DeclaringType);
        if (td is null) return false;
        var ns = !string.IsNullOrEmpty(td.Namespace) ? td.Namespace : "(global)";
        // nsLabel may have search highlight markup — compare against the raw namespace
        return nsLabel.Contains(ns, StringComparison.Ordinal);
    }

    /// <summary>
    /// Handles activation (Enter key or click) on a tree row.
    /// Methods get selected; namespaces/types toggle expansion.
    /// </summary>
    private static void ActivateTreeRow(IlTreeRow row, DotsiderState state)
    {
        switch (row.Kind)
        {
            case IlTreeRowKind.Method when row.Method is not null:
                state.IlSelectedMethod = row.Method;
                state.IlSelectedMethodOwner = row.Owner;
                state.IlFocusedTreeKey = row.Key;
                break;
            case IlTreeRowKind.Method when row.Symbol is not null:
                state.IlSelectedNativeSymbol = row.Symbol;
                state.IlFocusedTreeKey = row.Key;
                break;
            case IlTreeRowKind.Namespace:
            case IlTreeRowKind.Type:
            case IlTreeRowKind.Assembly:
                // Toggle expansion — the row carries its effective state, which also
                // covers assembly rows whose default depends on being the root member.
                state.IlTreeExpansionState[row.ExpansionKey] = !row.IsExpanded;
                break;
        }
    }

    /// <summary>
    /// Builds the right pane for a non-managed binary: the selected native symbol's disassembly,
    /// rebuilding the editor only when the symbol or analyzer changes, and stashing the decoded
    /// instructions and header line count so the span-driven decoration providers can consume them.
    /// </summary>
    private static Hex1bWidget[] BuildNativeEditorPane<T>(
        WidgetContext<T> ctx, DotsiderState state) where T : Hex1bWidget
    {
        if (state.IlSelectedNativeSymbol is not { } symbol)
            return [ctx.Text("  Select a function to view its disassembly").FillHeight()];

        if (state.IlEditorNativeSymbol?.VirtualAddress != symbol.VirtualAddress
            || !ReferenceEquals(state.IlEditorAnalyzer, state.Analyzer))
        {
            if (state.IlEditorKey is not null && state.IlEditorState is not null)
                state.IlCachedEditors[state.IlEditorKey] = state.IlEditorState;

            var result = NativeDisassembler.DisassembleSymbol(state.Analyzer, symbol);
            var disassembly = result?.Text
                ?? $"// {symbol.ManagedName ?? symbol.Name}\n// No disassemblable bytes.";
            var doc = new Hex1bDocument(disassembly);
            state.IlEditorState = new EditorState(doc) { IsReadOnly = true };
            state.IlEditorNativeSymbol = symbol;
            state.IlEditorMethod = null;
            state.IlEditorField = null;
            state.IlEditorAnalyzer = state.Analyzer;
            state.IlNativeInstructions = result?.Instructions;
            state.IlNativeHeaderLineCount = result?.HeaderLineCount ?? 0;
            state.IlNativeSyntaxProvider.Instructions = state.IlNativeInstructions;
            state.IlNativeNavigationProvider.Instructions = state.IlNativeInstructions;
            state.IlEditorKey = state.GetOrCreateNativeEditorKey(state.Analyzer, symbol.VirtualAddress);
            state.IlCachedEditors.Remove(state.IlEditorKey);

            var firstLine = result?.Instructions.FirstOrDefault(i => i.DisplayLine is not null)?.DisplayLine
                ?? state.IlNativeHeaderLineCount + 1;
            state.IlEditorState.SetCursorPosition(new DocumentOffset(GetLineStartOffset(disassembly, firstLine)));
        }

        return WrapInStatePanels(state);
    }

    /// <summary>
    /// Builds the right pane for the detached case: native disassembly for a
    /// metadata-less binary, IL for a managed one. With a pre-ILC set attached,
    /// <see cref="BuildRightPane"/> composes the panes instead.
    /// </summary>
    private static Hex1bWidget[] BuildEditorPane<T>(
        WidgetContext<T> ctx, DotsiderState state) where T : Hex1bWidget
    {
        // Native (metadata-less) mode: render the selected symbol's native disassembly.
        if (!state.Analyzer.HasManagedMetadata && state.Analyzer.PreIlcCompanions is null)
            return BuildNativeEditorPane(ctx, state);

        return BuildManagedEditorPane(ctx, state,
            state.IlSelectedMethodOwner ?? state.MetadataAnalyzer, methodOverride: null);
    }

    /// <summary>
    /// Composes the right pane when a pre-ILC companion set is attached: the selected
    /// method's IL and its correlated native code side by side under a status line, a
    /// single pane when only one side exists, and a collapsed single pane (swapped with
    /// <c>l</c>) when the area is too narrow to split.
    /// </summary>
    private static Hex1bWidget[] BuildRightPane<T>(
        WidgetContext<T> ctx, DotsiderState state) where T : Hex1bWidget
    {
        if (state.Analyzer.IsReadyToRun)
            return BuildReadyToRunRightPane(ctx, state);

        if (state.Analyzer.PreIlcCompanions is not { } companions)
            return BuildEditorPane(ctx, state);

        state.EnsureManagedNativeIndexAsync();
        var index = state.PreIlcIndex;
        MeasureRightPane(state);

        Dotsider.Core.Analysis.AssemblyAnalyzer owner;
        MethodDefInfo? method;
        NativeSymbol? symbol;
        string status;

        if (!IsNativeTreeMode(state))
        {
            owner = state.IlSelectedMethodOwner ?? companions.Root;
            method = state.IlSelectedMethod;
            if (method is null)
                return BuildManagedEditorPane(ctx, state, owner, methodOverride: null);

            var correlation = index?.Find(owner.AssemblyName ?? "", method.Token);
            symbol = correlation?.NativeSymbols.FirstOrDefault(s => s.FileOffset is not null)
                ?? (correlation?.NativeSymbols is { Count: > 0 } candidates ? candidates[0] : null);
            status = correlation switch
            {
                null => index is null ? " correlating IL ↔ native…" : " no correlation data for this method",
                { Status: MethodCorrelationStatus.CorrelatedExact } when symbol is not null =>
                    $" native: {symbol.ManagedName ?? symbol.Name} @ 0x{symbol.VirtualAddress:X} · {correlation.NativeSize}B"
                    + (correlation.NativeSymbols.Count > 1 ? $" ({correlation.NativeSymbols.Count} instantiations)" : ""),
                { Status: MethodCorrelationStatus.CorrelatedAmbiguous } when symbol is not null =>
                    $" native: {symbol.ManagedName ?? symbol.Name} (+{correlation.NativeSymbols.Count - 1} candidates,"
                    + $" {correlation.SharedCandidateSize}B shared)",
                { Status: MethodCorrelationStatus.CorrelatedByMstatOnly } =>
                    $" size only ({correlation.NativeSize}B from mstat); no native symbol",
                _ => " not in native image — trimmed or inlined",
            };
        }
        else
        {
            symbol = state.IlSelectedNativeSymbol;
            if (symbol is null)
                return [ctx.Text("  Select a function to view its disassembly").FillHeight()];

            var correlation = index?.FindByNativeSymbol(symbol);
            if (correlation is not null)
            {
                method = correlation.Method;
                owner = companions.FindByAssemblyName(correlation.AssemblyName) ?? companions.Root;
                status = correlation.Status == MethodCorrelationStatus.CorrelatedAmbiguous
                    ? $" managed: {correlation.Method.DeclaringType}.{correlation.Method.Name} (ambiguous overloads)"
                    : $" managed: {correlation.Method.DeclaringType}.{correlation.Method.Name}";
            }
            else
            {
                method = null;
                owner = companions.Root;
                status = index is null
                    ? " correlating IL ↔ native…"
                    : " no managed source — runtime/stub code";
            }
        }

        var showBoth = method is not null && symbol is not null;
        var collapsed = showBoth && state.IlRightPaneWidth > 0 && state.IlRightPaneWidth < 80;
        if (collapsed) status += "  |  l: swap pane";

        Hex1bWidget content;
        if (showBoth && !collapsed)
        {
            var ownerCaptured = owner;
            var methodCaptured = method!;
            var symbolCaptured = symbol!;
            content = ctx.HSplitter(
                l => BuildManagedEditorPane(l, state, ownerCaptured, methodCaptured),
                r => BuildPairNativePane(r, state, symbolCaptured),
                leftWidth: 60).FillWidth().FillHeight();
        }
        else if (method is not null && (!showBoth || state.IlFocusedPane != IlPane.Native))
        {
            content = new VStackWidget([.. BuildManagedEditorPane(ctx, state, owner, method)])
                .FillWidth().FillHeight();
        }
        else
        {
            content = new VStackWidget([.. BuildPairNativePane(ctx, state, symbol!)])
                .FillWidth().FillHeight();
        }

        return
        [
            ctx.Text(status).FixedHeight(1),
            content,
        ];
    }

    /// <summary>
    /// Builds the right pane for a ReadyToRun image: the selected managed method's IL beside its
    /// precompiled native body (all code ranges), or IL alone with an honest status when the method
    /// is not precompiled, the owner composite is missing, or the architecture could not be identified.
    /// Reuses the pre-ILC pair pane with a ReadyToRun-sourced native side.
    /// </summary>
    private static Hex1bWidget[] BuildReadyToRunRightPane<T>(
        WidgetContext<T> ctx, DotsiderState state) where T : Hex1bWidget
    {
        MeasureRightPane(state);
        var owner = state.MetadataAnalyzer;
        var method = state.IlSelectedMethod;
        if (method is null)
            return BuildManagedEditorPane(ctx, state, owner, methodOverride: null);

        var index = state.Analyzer.ReadyToRunIndex;
        var entry = index?.Find(owner.AssemblyName ?? "", method.Token);
        var codeImage = state.Analyzer.ReadyToRunCodeImage;

        string status;
        (string Text, IReadOnlyList<NativeInstruction> Instructions)? nativeDisasm = null;
        NativeSymbol? hotSymbol = null;

        if (entry is null)
        {
            status = " IL only — not precompiled in this image";
        }
        else if (codeImage is null)
        {
            status = " owner composite missing; native code unavailable";
        }
        else if (codeImage.NativeSymbols?.Architecture is NativeArchitecture.Unknown)
        {
            status = " precompiled; architecture unknown";
        }
        else
        {
            string? Resolver(ulong va) =>
                index!.FindByAddress(va) is { DeclaringType: not null } e ? $"{e.DeclaringType}.{e.Name}" : null;
            if (ReadyToRunDisassembler.DisassembleMethod(codeImage, entry, Resolver) is { } d
                && codeImage.NativeSymbols!.TryFindByAddress(entry.CodeRanges[0].VirtualAddress, out var sym))
            {
                nativeDisasm = (d.Text, d.Instructions);
                hotSymbol = sym;
                var rangeNote = entry.CodeRanges.Count > 1 ? $" · {entry.CodeRanges.Count} ranges" : "";
                status = $" native: {entry.DeclaringType}.{entry.Name} @ 0x{entry.CodeRanges[0].VirtualAddress:X}"
                    + $" · {entry.TotalSize}B{rangeNote}";
            }
            else
            {
                status = " no disassemblable native code";
            }
        }

        var showBoth = hotSymbol is not null && nativeDisasm is not null;
        var collapsed = showBoth && state.IlRightPaneWidth is > 0 and < 80;
        if (collapsed) status += "  |  l: swap pane";

        Hex1bWidget content;
        if (showBoth && !collapsed)
        {
            var native = nativeDisasm;
            var sym = hotSymbol!;
            content = ctx.HSplitter(
                l => BuildManagedEditorPane(l, state, owner, method),
                r => BuildPairNativePane(r, state, sym, native),
                leftWidth: 60).FillWidth().FillHeight();
        }
        else if (!showBoth || state.IlFocusedPane != IlPane.Native)
        {
            content = new VStackWidget([.. BuildManagedEditorPane(ctx, state, owner, method)])
                .FillWidth().FillHeight();
        }
        else
        {
            content = new VStackWidget([.. BuildPairNativePane(ctx, state, hotSymbol!, nativeDisasm)])
                .FillWidth().FillHeight();
        }

        return
        [
            ctx.Text(status).FixedHeight(1),
            content,
        ];
    }

    /// <summary>
    /// Captures the width of the right-pane area from the arranged editor nodes (the same
    /// last-frame node pattern the tree list uses for viewport height). Both pane
    /// viewports sum to the area regardless of split state; zero until first arrival,
    /// which renders split by default.
    /// </summary>
    private static void MeasureRightPane(DotsiderState state)
    {
        var il = 0;
        var native = 0;
        foreach (var node in state.App.Focusables)
        {
            if (node is not EditorNode editor) continue;
            if (state.IlEditorState is not null && ReferenceEquals(editor.State, state.IlEditorState))
                il = editor.ViewportColumns;
            else if (state.IlPairNativeEditorState is not null
                && ReferenceEquals(editor.State, state.IlPairNativeEditorState))
                native = editor.ViewportColumns;
        }

        var total = il + native + (il > 0 && native > 0 ? 1 : 0);
        if (total > 0) state.IlRightPaneWidth = total;
    }

    /// <summary>
    /// Builds the native pair pane: the correlated symbol's disassembly with
    /// correlation-aware target names, cached per (analyzer, address) in its own
    /// StatePanelWidget scope so it never shares identity with the solo-native pipeline.
    /// </summary>
    private static Hex1bWidget[] BuildPairNativePane<T>(
        WidgetContext<T> ctx, DotsiderState state, NativeSymbol symbol,
        (string Text, IReadOnlyList<NativeInstruction> Instructions)? precomputed = null) where T : Hex1bWidget
    {
        _ = ctx;
        // Rebuild on a symbol change, or once when the correlation index arrives after a symbol
        // was already selected — otherwise the resolver keeps emitting reduced target names.
        var indexAvailable = state.PreIlcIndex is not null;
        if (state.IlPairNativeSymbol?.VirtualAddress != symbol.VirtualAddress
            || (indexAvailable && !state.IlPairNativeBuiltWithIndex))
        {
            if (state.IlPairEditorKey is not null && state.IlPairNativeEditorState is not null)
                state.IlPairCachedEditors[state.IlPairEditorKey] = state.IlPairNativeEditorState;

            // Offsets from the previous listing no longer map after a rebuild.
            state.IlPairNativeBackStack.Clear();

            string? ManagedNameResolver(ulong va) =>
                state.PreIlcIndex?.FindByAddress(va) is { } c
                    ? $"{c.Method.DeclaringType}.{c.Method.Name}"
                    : null;

            // A ReadyToRun method's full body (all ranges) is precomputed and rebased; otherwise
            // disassemble the single selected symbol from the analyzer.
            var result = precomputed is { } pc
                ? (pc.Text, pc.Instructions, HeaderLineCount: 0)
                : NativeDisassembler.DisassembleSymbol(state.Analyzer, symbol, ManagedNameResolver);
            var disassembly = result?.Text
                ?? $"// {symbol.ManagedName ?? symbol.Name}\n// No disassemblable bytes.";
            var doc = new Hex1bDocument(disassembly);
            state.IlPairNativeEditorState = new EditorState(doc) { IsReadOnly = true };
            state.IlPairNativeSymbol = symbol;
            state.IlPairNativeInstructions = result?.Instructions;
            state.IlPairNativeHeaderLineCount = result?.HeaderLineCount ?? 0;
            state.IlPairNativeSyntaxProvider.Instructions = state.IlPairNativeInstructions;
            state.IlPairNativeNavigationProvider.Instructions = state.IlPairNativeInstructions;
            state.IlPairEditorKey = state.GetOrCreatePairNativeEditorKey(state.Analyzer, symbol.VirtualAddress);
            state.IlPairCachedEditors.Remove(state.IlPairEditorKey);
            state.IlPairNativeBuiltWithIndex = indexAvailable;

            var firstLine = result?.Instructions.FirstOrDefault(i => i.DisplayLine is not null)?.DisplayLine
                ?? state.IlPairNativeHeaderLineCount + 1;
            state.IlPairNativeEditorState.SetCursorPosition(
                new DocumentOffset(GetLineStartOffset(disassembly, firstLine)));
        }

        return WrapInPairStatePanels(state);
    }

    /// <summary>
    /// Wraps the pair pane's editor in its own StatePanelWidget scope with hidden cached
    /// editors — the pair mirror of <see cref="WrapInStatePanels"/>.
    /// </summary>
    private static Hex1bWidget[] WrapInPairStatePanels(DotsiderState state)
    {
        if (state.IlPairEditorKey is null || state.IlPairNativeEditorState is null)
            return [new TextBlockWidget("  No native code")];

        return
        [
            new StatePanelWidget(state.IlPairEditorScopeKey, _ =>
            {
                var children = new List<Hex1bWidget>
                {
                    new StatePanelWidget(state.IlPairEditorKey, _ =>
                        IlPairNativeEditorHost.Build(state.IlPairNativeEditorState!, state))
                        .FillWidth().FillHeight()
                };

                var hidden = new Dictionary<object, EditorState>(ReferenceEqualityComparer.Instance);
                foreach (var (key, es) in state.IlPairCachedEditors)
                    hidden.TryAdd(key, es);
                hidden.Remove(state.IlPairEditorKey);

                if (hidden.Count > 0)
                {
                    var branches = new List<ConditionalWidget>();
                    foreach (var (key, es) in hidden)
                    {
                        branches.Add(new ConditionalWidget((_, _) => false,
                            new StatePanelWidget(key, _ =>
                                new ThemePanelWidget(
                                    t => t
                                        .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                                        .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
                                    new EditorWidget(es)))));
                    }
                    children.Add(new ResponsiveWidget(branches).FixedHeight(0));
                }

                return new VStackWidget([.. children]).FillWidth().FillHeight();
            }).FillWidth().FillHeight()
        ];
    }

    /// <summary>
    /// Builds the managed IL editor pane for a method owned by <paramref name="owner"/> —
    /// the current analyzer, the pre-ILC root, or a local reference. Creates or reuses
    /// EditorState based on the selected method.
    /// </summary>
    private static Hex1bWidget[] BuildManagedEditorPane<T>(
        WidgetContext<T> ctx, DotsiderState state,
        Dotsider.Core.Analysis.AssemblyAnalyzer owner, MethodDefInfo? methodOverride) where T : Hex1bWidget
    {
        if ((methodOverride ?? state.IlSelectedMethod) is not { } method)
        {
            if (state.IlSelectedField is { } field)
            {
                // Only recreate editor state when the field changes
                if (state.IlEditorField?.Token != field.Token
                    || !ReferenceEquals(state.IlEditorAnalyzer, owner))
                {
                    // Save outgoing editor to cache
                    if (state.IlEditorKey is not null && state.IlEditorState is not null)
                        state.IlCachedEditors[state.IlEditorKey] = state.IlEditorState;

                    var fieldInfo = $"// Field: {field.DeclaringType}::{field.Name}\n"
                        + $"// Type: {field.Signature}\n"
                        + $"// Attributes: {field.Attributes}\n"
                        + $"// Token: 0x{field.Token:X8}\n"
                        + "\n"
                        + "// Fields do not have IL bodies.\n"
                        + "// Press Esc to go back.";
                    var fieldDoc = new Hex1bDocument(fieldInfo);
                    state.IlEditorState = new EditorState(fieldDoc) { IsReadOnly = true };
                    state.IlEditorField = field;
                    state.IlEditorMethod = null;
                    state.IlEditorAnalyzer = owner;
                    state.IlEditorKey = state.GetOrCreateEditorKey(owner, field.Token);
                    state.IlCachedEditors.Remove(state.IlEditorKey);
                }

                return WrapInStatePanels(state);
            }

            return [ctx.Text("  Select a method to view IL disassembly").FillHeight()];
        }

        // Create new editor state when the method changes or when the analyzer
        // was replaced (e.g. after SaveHexChanges swaps in a new image).
        if (state.IlEditorMethod?.Token != method.Token
            || !ReferenceEquals(state.IlEditorAnalyzer, owner))
        {
            var disassembler = state.GetMetadataIlDisassembler(owner);
            if (disassembler is null)
                return [ctx.Text("  No IL disassembler for this assembly").FillHeight()];

            // Save outgoing editor to cache
            if (state.IlEditorKey is not null && state.IlEditorState is not null)
                state.IlCachedEditors[state.IlEditorKey] = state.IlEditorState;

            var result = disassembler.DisassembleWithText(method);
            var disassembly = result?.Text ?? disassembler.FormatDisassembly(method);
            var doc = new Hex1bDocument(disassembly);
            state.IlEditorState = new EditorState(doc) { IsReadOnly = true };
            state.IlEditorMethod = method;
            state.IlEditorField = null;
            state.IlEditorAnalyzer = owner;
            state.IlInstructions = result?.Instructions;
            state.IlHeaderLineCount = result?.HeaderLineCount ?? 0;
            state.IlNavigationProvider.Instructions = state.IlInstructions;
            state.IlNavigationProvider.HeaderLineCount = state.IlHeaderLineCount;
            state.IlSourceLinkProvider.Instructions = state.IlInstructions;
            state.IlEditorKey = state.GetOrCreateEditorKey(owner, method.Token);
            state.IlCachedEditors.Remove(state.IlEditorKey);

            var firstInstructionLine = state.IlInstructions?
                .FirstOrDefault(i => i.DisplayLine is not null)
                ?.DisplayLine;
            var targetLine = firstInstructionLine ?? state.IlHeaderLineCount + 1;
            state.IlEditorState.SetCursorPosition(new DocumentOffset(GetLineStartOffset(disassembly, targetLine)));
        }

        // Consume pending cursor match (from search n/N navigation)
        if (state.IlPendingCursorMatch is { } match && state.IlEditorState is not null)
        {
            state.IlPendingCursorMatch = null;
            var matchText = state.IlEditorState.Document.GetText();
            var line = 1;
            var col = 1;
            var targetOffset = 0;
            for (var i = 0; i < matchText.Length; i++)
            {
                if (line == match.Line && col == match.Column) { targetOffset = i; break; }
                if (matchText[i] == '\n') { line++; col = 1; } else col++;
            }
            state.IlEditorState.SetCursorPosition(new DocumentOffset(targetOffset));
        }

        // After double-click word selection, the cursor lands one past the word
        // on punctuation. Detect this as a one-shot (both anchor and position changed
        // since last frame) and adjust once — Shift+Arrow only moves position, so it
        // is never affected.
        if (state.CurrentTab == TabId.IlInspector)
            AdjustWordSelectionCursorOneShot(state);

        return WrapInStatePanels(state);
    }

    /// <summary>
    /// Wraps the current IL editor in nested <see cref="StatePanelWidget"/>s that cache
    /// EditorNode instances by method/field identity. Hidden zero-height editors keep
    /// back-stack and previously-visited editor nodes alive with preserved scroll.
    /// </summary>
    private static Hex1bWidget[] WrapInStatePanels(DotsiderState state)
    {
        if (state.IlEditorKey is null || state.IlEditorState is null)
            return [new TextBlockWidget("  Select a method to view IL disassembly")];

        return
        [
            new StatePanelWidget(state.IlEditorScopeKey, _ =>
            {
                var children = new List<Hex1bWidget>
                {
                    // Current editor — full-size with all bindings and decorations
                    new StatePanelWidget(state.IlEditorKey, _ =>
                        IlEditorHost.Build(state.IlEditorState!, state))
                        .FillWidth().FillHeight()
                };

                // Merge cached editors + back-stack entries → hidden StatePanelWidgets.
                // These keep EditorNodes alive with preserved scroll for revisits.
                var hidden = new Dictionary<object, EditorState>(ReferenceEqualityComparer.Instance);
                foreach (var (key, es) in state.IlCachedEditors)
                    hidden.TryAdd(key, es);
                foreach (var entry in state.IlBackStack)
                    if (entry.EditorKey is not null)
                        hidden.TryAdd(entry.EditorKey, entry.EditorState);
                hidden.Remove(state.IlEditorKey);

                // Wrap hidden editors in a ResponsiveWidget with always-false conditions.
                // ResponsiveNode reconciles ALL branches (keeping EditorNodes alive with
                // preserved scroll) but GetFocusableNodes only yields from the active branch.
                // With all conditions false, no branch is active → hidden editors are excluded
                // from the focus ring, preventing stale focus after method switches.
                if (hidden.Count > 0)
                {
                    var branches = new List<ConditionalWidget>();
                    foreach (var (key, es) in hidden)
                    {
                        branches.Add(new ConditionalWidget((_, _) => false,
                            new StatePanelWidget(key, _ =>
                                new ThemePanelWidget(
                                    t => t
                                        .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                                        .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
                                    new EditorWidget(es)))));
                    }
                    children.Add(new ResponsiveWidget(branches).FixedHeight(0));
                }

                return new VStackWidget([.. children]).FillWidth().FillHeight();
            }).FillWidth().FillHeight()
        ];
    }

    private static int GetLineStartOffset(string text, int lineNumber)
    {
        if (lineNumber <= 1)
            return 0;

        var line = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
                continue;

            line++;
            if (line == lineNumber)
                return i + 1;
        }

        return 0;
    }

    /// <summary>
    /// Detects double-click word selection (both anchor and position changed since last
    /// frame) and adjusts the cursor once so it sits on the last character of the word
    /// instead of the trailing punctuation. Shift+Arrow only changes position, so this
    /// never fires during keyboard selection.
    /// </summary>
    internal static void AdjustWordSelectionCursorOneShot(DotsiderState state)
    {
        if (state.IlEditorState is null)
            return;

        AdjustWordSelectionCursorOneShot(
            state.IlEditorState,
            ref state.IlPrevSelectionAnchor,
            ref state.IlPrevCursorPosition);
    }

    /// <summary>
    /// Core one-shot logic: if both anchor and position changed since last call
    /// (double-click pattern) and the selection is a single word ending on punctuation,
    /// pull the cursor back onto the last word character.
    /// </summary>
    internal static void AdjustWordSelectionCursorOneShot(
        EditorState es,
        ref DocumentOffset? prevAnchor,
        ref DocumentOffset? prevPosition)
    {
        var anchor = es.Cursor.SelectionAnchor;
        var position = es.Cursor.Position;

        // Detect: both anchor and position changed since last frame (double-click pattern).
        var anchorChanged = anchor != prevAnchor;
        var positionChanged = position != prevPosition;

        if (anchorChanged && positionChanged
            && es.Cursor is { HasSelection: true, SelectionAnchor: { } a }
            && position.Value > a.Value)
        {
            var text = es.Document.GetText();
            var cursorVal = position.Value;
            if (cursorVal < text.Length && !char.IsLetterOrDigit(text[cursorVal]))
            {
                var sel = es.Document.GetText(es.Cursor.SelectionRange);
                if (sel.Length > 0 && sel.All(char.IsLetterOrDigit))
                    es.Cursor.Position = new DocumentOffset(cursorVal - 1);
            }
        }

        // Record current state for next frame comparison.
        prevAnchor = es.Cursor.SelectionAnchor;
        prevPosition = es.Cursor.Position;
    }

    /// <summary>
    /// Per-render scroll bookkeeping for the tree, called by <see cref="Build"/> with the
    /// freshly built rows: captures the panel node from <see cref="Hex1bApp.Focusables"/>,
    /// consumes the pending scroll-into-view request once the panel is arranged, and keeps
    /// invalidating until first arrival completes. Internal so the virtualization tests
    /// drive the same capture/pending logic the view runs.
    /// </summary>
    /// <param name="state">The shared application state.</param>
    /// <param name="treeRows">The rows built for this render.</param>
    internal static void SyncTreeScroll(DotsiderState state, IReadOnlyList<IlTreeRow> treeRows)
    {
        // Only the root build advances BuildGeneration (DotsiderApp.Build; the tests'
        // harness builder mirrors it). Advancing it here — mid-frame — would make a
        // nudger armed concurrently from a socket thread believe a later build already
        // ran and exit without nudging, dropping the very invalidation it protects.

        // Capture the ScrollPanelNode that hosts the tree from App.Focusables.
        // ScrollPanelNode enters the focus ring after the focus-ring rebuild, which
        // happens on frame 2 after a tab switch. The bootstrap invalidate below kicks
        // a second frame so first-arrival rendering and pending-scroll consumption
        // do not have to wait for user input.
        if (state.IlScrollPanelNode is null || !state.App.Focusables.Contains(state.IlScrollPanelNode))
        {
            state.IlScrollPanelNode = null;
            foreach (var node in state.App.Focusables)
            {
                if (node is ScrollPanelNode sp)
                {
                    state.IlScrollPanelNode = sp;
                    break;
                }
            }
        }

        // Pending-scroll consumer. The flag is armed by SetIlFocusedTreeKey at every
        // non-user-driven mutation site (cross-view jumps, search match navigation,
        // back navigation). It is cleared only when the consumer can make a final,
        // well-clamped decision: the panel is captured and arranged, so its viewport
        // height and the freshly built rows fully determine the offset. Until then,
        // request another frame and try again — this is how external first-arrival
        // jumps land in the viewport even though the panel is null on frame 1.
        if (state.IlScrollSelectionIntoViewPending)
        {
            if (state.IlFocusedTreeKey is not string pendingKey)
            {
                state.IlScrollSelectionIntoViewPending = false;
            }
            else if (IlTreeList.FindRowIndex(treeRows, pendingKey) is var pendingIdx && pendingIdx < 0)
            {
                state.IlScrollSelectionIntoViewPending = false;
            }
            else if (state.IlScrollPanelNode is not { } pendingSp || pendingSp.ViewportSize <= 0)
            {
                state.RequestExtraFrame();
            }
            else
            {
                EnsureSelectionVisible(state, pendingSp, pendingIdx, treeRows.Count);
                state.IlScrollSelectionIntoViewPending = false;
            }
        }

        // First-arrival bootstrap: the ScrollPanelNode is reconciled but does not enter
        // App.Focusables until the next focus-ring rebuild. Without an extra frame,
        // the tree renders at its fallback window and the scrollbar stays invisible
        // until the user presses a key. The retry self-terminates the moment the panel
        // is captured.
        if (state.CurrentTab == TabId.IlInspector
            && state.IlScrollPanelNode is null
            && treeRows.Count > 0)
        {
            state.RequestExtraFrame();
        }
    }

    /// <summary>
    /// Adjusts <see cref="DotsiderState.IlTreeScrollOffset"/> so the row at
    /// <paramref name="rowIndex"/> sits inside the viewport. No-op when the row is
    /// already visible or the panel has not been arranged yet. The result is clamped
    /// to <c>[0, rowCount - viewport]</c>; the next build renders the new window.
    /// </summary>
    /// <param name="state">The shared application state owning the scroll offset.</param>
    /// <param name="sp">The IL tree's scroll panel (the viewport-height source).</param>
    /// <param name="rowIndex">The target row index in the flattened tree.</param>
    /// <param name="rowCount">The current flattened row count, for clamping.</param>
    internal static void EnsureSelectionVisible(DotsiderState state, ScrollPanelNode sp, int rowIndex, int rowCount)
    {
        var viewport = sp.ViewportSize;
        if (viewport <= 0) return;

        var offset = state.IlTreeScrollOffset;
        if (rowIndex < offset)
            offset = rowIndex;
        else if (rowIndex >= offset + viewport)
            offset = rowIndex - viewport + 1;

        state.IlTreeScrollOffset = Math.Clamp(offset, 0, Math.Max(0, rowCount - viewport));
    }

    internal static bool GetExpansionState(DotsiderState state, string key, bool defaultExpanded) =>
        state.IlTreeExpansionState.TryGetValue(key, out var expanded) ? expanded : defaultExpanded;

    /// <summary>
    /// Collects all text-level search matches across ALL methods' IL disassembly,
    /// ordered to mirror the actual tree traversal order:
    /// namespaces alphabetical, types in TypeDefs order, methods in MethodDefs order,
    /// then line → column within each method's disassembly.
    /// </summary>
    /// <summary>Collects the character offsets of the query within the current native listing (case-insensitive).</summary>
    private static List<int> CollectNativeMatches(EditorState? editor, string query)
    {
        if (editor is null) return [];
        var text = editor.Document.GetText();
        var offsets = new List<int>();
        for (var i = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
             i >= 0;
             i = text.IndexOf(query, i + query.Length, StringComparison.OrdinalIgnoreCase))
            offsets.Add(i);
        return offsets;
    }

    /// <summary>Moves the searched native listing's cursor to the next/previous confirmed-search offset (n/N).</summary>
    private static void NavigateToNativeMatch(DotsiderState state, bool forward)
    {
        var offsets = state.IlNativeSearchOffsets;
        var attached = state.Analyzer.PreIlcCompanions is not null;
        var editor = attached ? state.IlPairNativeEditorState : state.IlEditorState;
        if (offsets.Count == 0 || editor is null) return;

        state.IlCurrentMatchIndex = forward
            ? (state.IlCurrentMatchIndex + 1) % offsets.Count
            : state.IlCurrentMatchIndex <= 0 ? offsets.Count - 1 : state.IlCurrentMatchIndex - 1;
        editor.SetCursorPosition(new DocumentOffset(offsets[state.IlCurrentMatchIndex]));
        state.App.RequestFocus(node => node is EditorNode e && ReferenceEquals(e.State, editor));
        state.App.Invalidate();
    }

    private static List<IlMatch> CollectTextMatches(DotsiderState state, string query)
    {
        var result = new List<IlMatch>();

        // Sweep every assembly the tree shows: the routed metadata analyzer plus every
        // attached local reference. Each is disassembled through its own owner-scoped
        // disassembler so matches that exist only in a local reference are found too, and
        // each match carries its owner so navigation and the token filter never collide
        // across assemblies.
        var set = state.Analyzer.PreIlcCompanions;
        var companions = set is not null ? set.All : [state.MetadataAnalyzer];

        foreach (var companion in companions)
        {
            var owner = set is not null && !ReferenceEquals(companion, set.Root) ? companion : null;
            var disassembler = state.GetMetadataIlDisassembler(companion);
            if (disassembler is null) continue;

            var methodsByType = companion.MethodDefs
                .GroupBy(m => m.DeclaringType)
                .ToDictionary(g => g.Key, g => g.ToList());

            var typesByNamespace = companion.TypeDefs
                .GroupBy(td => string.IsNullOrEmpty(td.Namespace) ? "(global)" : td.Namespace)
                .OrderBy(g => g.Key);

            foreach (var nsGroup in typesByNamespace)
            {
                foreach (var typeDef in nsGroup)
                {
                    if (!methodsByType.TryGetValue(typeDef.FullName, out var methods)) continue;

                    foreach (var method in methods)
                    {
                        var disassembly = disassembler.FormatDisassembly(method);
                        var lines = disassembly.Split('\n');

                        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
                        {
                            var line = lines[lineIdx];
                            var pos = 0;
                            while (pos < line.Length)
                            {
                                var idx = line.IndexOf(query, pos, StringComparison.OrdinalIgnoreCase);
                                if (idx < 0) break;

                                result.Add(new IlMatch(method, lineIdx + 1, idx + 1, query.Length, owner));
                                pos = idx + query.Length;
                            }
                        }
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Simulates a search-driven method switch for testing. Exercises the same code path
    /// as search n/N when the match is in a different method.
    /// </summary>
    internal static void NavigateToMatchForTest(DotsiderState state, MethodDefInfo method)
    {
        NavigateToMatch(state, new IlMatch(method, 1, 1, 1));
    }

    /// <summary>
    /// Navigates to a specific text match, switching methods and expanding tree nodes as needed.
    /// </summary>
    private static void NavigateToMatch(DotsiderState state, IlMatch match)
    {
        var owner = match.Owner;
        var source = owner ?? state.MetadataAnalyzer;
        var prefix = owner is null ? "" : $"{owner.AssemblyName ?? owner.FileName}|";

        // Switch method if needed — owner-qualified, since tokens collide across assemblies.
        if (state.IlSelectedMethod?.Token != match.Method.Token
            || !ReferenceEquals(state.IlSelectedMethodOwner, owner))
        {
            state.IlSelectedMethod = match.Method;
            state.IlSelectedMethodOwner = owner;

            // Expand assembly (for a local reference), namespace, and type in the tree.
            if (owner is not null)
                state.IlTreeExpansionState[$"asm:{owner.AssemblyName ?? owner.FileName}"] = true;
            var typeDef = source.TypeDefs.FirstOrDefault(t => t.FullName == match.Method.DeclaringType);
            var ns = typeDef is not null && !string.IsNullOrEmpty(typeDef.Namespace)
                ? typeDef.Namespace : "(global)";
            state.IlTreeExpansionState[$"ns:{prefix}{ns}"] = true;
            state.IlTreeExpansionState[$"type:{prefix}{match.Method.DeclaringType}"] = true;

            // When method switches, the old editor moves to the hidden cache.
            // Request focus on the visible editor so input stays on the displayed method.
            state.App.RequestFocus(node => node is EditorNode);
        }

        // Focus the method row in the tree table
        state.SetIlFocusedTreeKey($"method:{prefix}{match.Method.Token}");

        // Set pending cursor match — consumed by BuildEditorPane on next frame
        state.IlPendingCursorMatch = match;

        state.App.Invalidate();
    }
}
