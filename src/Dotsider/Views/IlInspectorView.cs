using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;
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
            if (!string.IsNullOrEmpty(query) && search.IsConfirmed)
            {
                if (state.Analyzer.BinaryKind != BinaryKind.Managed)
                {
                    // Native mode: match the query against the current listing text and step the
                    // cursor through the hits with n/N (IlSearchProvider highlights every occurrence).
                    if (query != state.IlLastSearchQuery)
                    {
                        state.IlNativeSearchOffsets = CollectNativeMatches(state, query);
                        state.IlLastSearchQuery = query;
                        state.IlCurrentMatchIndex = -1;
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
                    if (query != state.IlLastSearchQuery)
                    {
                        state.IlSearchMatches = CollectTextMatches(state, query);
                        state.IlTextMatchMethodTokens = [.. state.IlSearchMatches.Select(m => m.Method.Token)];
                        state.IlLastSearchQuery = query;
                        state.IlCurrentMatchIndex = -1;
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

            // Update search decoration provider
            state.IlSearchProvider.Query = search.IsActive ? query : null;
            if (state.IlCurrentMatchIndex >= 0 && state.IlCurrentMatchIndex < state.IlSearchMatches.Count)
            {
                var currentMatch = state.IlSearchMatches[state.IlCurrentMatchIndex];
                state.IlSearchProvider.CurrentMatchStart = new DocumentPosition(currentMatch.Line, currentMatch.Column);
                state.IlSearchProvider.CurrentMatchLength = currentMatch.Length;
            }
            else if (state.IlCurrentMatchIndex >= 0
                && state.IlCurrentMatchIndex < state.IlNativeSearchOffsets.Count
                && state.IlEditorState is { } nativeEditor)
            {
                state.IlSearchProvider.CurrentMatchStart =
                    nativeEditor.Document.OffsetToPosition(new DocumentOffset(state.IlNativeSearchOffsets[state.IlCurrentMatchIndex]));
                state.IlSearchProvider.CurrentMatchLength = state.IlLastSearchQuery?.Length ?? 0;
            }
            else
            {
                state.IlSearchProvider.CurrentMatchStart = null;
                state.IlSearchProvider.CurrentMatchLength = 0;
            }
        }

        // Build the flattened tree rows for the left pane list — native symbols for a non-managed
        // binary, managed methods otherwise.
        var isNative = state.Analyzer.BinaryKind != BinaryKind.Managed;
        var treeRows = isNative ? BuildNativeTreeRows(state) : BuildTreeRows(state);
        var formattedRows = treeRows.Select(r => FormatTreeRow(r, state)).ToList();

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
        // well-clamped decision: panel captured AND ContentSize matches the freshly
        // built rows. Until both are true, request another frame and try again — this
        // is how external first-arrival jumps land in the viewport even though the
        // panel is null on frame 1, and how same-frame tree expansions still scroll
        // correctly after the next layout grows the content.
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
            else if (state.IlScrollPanelNode is not { } pendingSp
                     || pendingSp.ViewportSize <= 0
                     || pendingSp.ContentSize != treeRows.Count)
            {
                state.App.Invalidate();
            }
            else
            {
                EnsureSelectionVisible(pendingSp, pendingIdx);
                state.IlScrollSelectionIntoViewPending = false;
            }
        }

        // First-arrival bootstrap: the ScrollPanelNode is reconciled but does not enter
        // App.Focusables until the next focus-ring rebuild. Without an extra frame,
        // the scrollbar would stay invisible until the user pressed a key. The
        // invalidate self-terminates the moment the panel is captured.
        if (state.CurrentTab == TabId.IlInspector
            && state.IlScrollPanelNode is null
            && treeRows.Count > 0)
        {
            state.App.Invalidate();
        }

        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>();

            // Search bar (shared helper)
            SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

            // Main content: HSplitter with tree list on left, disassembly editor on right
            widgets.Add(outer.HSplitter(
                // Left pane: scroll-panel-hosted tree list (provides its own scrollbar)
                left =>
                [
                    IlTreeList.Build(
                        treeRows,
                        formattedRows,
                        state,
                        selectionChanged: index =>
                        {
                            if (index >= 0 && index < treeRows.Count)
                            {
                                var row = treeRows[index];
                                // Direct assignment on keyboard/click moves — does NOT arm
                                // IlScrollSelectionIntoViewPending. The keyboard handler
                                // calls EnsureSelectionVisible inline, so the pending
                                // path is reserved for external setters.
                                state.IlFocusedTreeKey = row.Key;
                                if (row is { Kind: IlTreeRowKind.Method, Method: not null })
                                    state.IlSelectedMethod = row.Method;
                                else if (row is { Kind: IlTreeRowKind.Method, Symbol: not null })
                                    state.IlSelectedNativeSymbol = row.Symbol;
                            }
                            state.App.Invalidate();
                        },
                        itemActivated: index =>
                        {
                            if (index >= 0 && index < treeRows.Count)
                                ActivateTreeRow(treeRows[index], state);
                            state.App.Invalidate();
                        },
                        expandRow: index =>
                        {
                            if (index >= 0 && index < treeRows.Count)
                            {
                                var row = treeRows[index];
                                if (row is { CanExpand: true, IsExpanded: false })
                                {
                                    state.IlTreeExpansionState[row.ExpansionKey] = true;
                                    state.App.Invalidate();
                                }
                            }
                        },
                        collapseRow: index =>
                        {
                            if (index >= 0 && index < treeRows.Count)
                            {
                                var row = treeRows[index];
                                if (row is { CanExpand: true, IsExpanded: true })
                                {
                                    state.IlTreeExpansionState[row.ExpansionKey] = false;
                                    state.App.Invalidate();
                                }
                            }
                        })
                ],
                // Right pane: IL disassembly via EditorWidget
                right => BuildEditorPane(right, state),
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
                    state.App.Invalidate();
                }
                else if (state.IlBackStack.Count > 0)
                {
                    var entry = state.IlBackStack.Pop();
                    state.RestoreFromIlBackEntry(entry);
                }
            }, "Esc");
        })
        .FillWidth().FillHeight();
    }

    /// <summary>
    /// Builds a flattened list of tree rows from the namespace → type → method hierarchy,
    /// respecting expansion state and search filtering.
    /// </summary>
    internal static List<IlTreeRow> BuildTreeRows(DotsiderState state)
    {
        var rows = new List<IlTreeRow>();
        var search = state.Search[TabId.IlInspector];
        var searchQuery = search.Query;

        var methodsByType = state.Analyzer.MethodDefs
            .GroupBy(m => m.DeclaringType)
            .ToDictionary(g => g.Key, g => g.ToList());

        var typesByNamespace = state.Analyzer.TypeDefs
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
                         (search.IsConfirmed && state.IlTextMatchMethodTokens?.Contains(m.Token) == true))))];

                if (nsTypes.Count == 0) continue;
            }

            var nsKey = $"ns:{nsGroup.Key}";
            var nsExpanded = GetExpansionState(state, nsKey, defaultExpanded: true);
            var nsLabel = HighlightHelper.HighlightSubstring(nsGroup.Key, searchQuery);

            rows.Add(new IlTreeRow(nsKey, 0, IlTreeRowKind.Namespace, nsLabel,
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
                                    (search.IsConfirmed && state.IlTextMatchMethodTokens?.Contains(m.Token) == true))];

                    if (filteredMethods.Count == 0) continue;
                }

                var typeKey = $"type:{typeDef.FullName}";
                var typeExpanded = GetExpansionState(state, typeKey, defaultExpanded: false);
                var typeLabel = HighlightHelper.HighlightSubstring(typeDef.Name, searchQuery);

                rows.Add(new IlTreeRow(typeKey, 1, IlTreeRowKind.Type, typeLabel,
                    null, CanExpand: true, IsExpanded: typeExpanded, ExpansionKey: typeKey));

                if (!typeExpanded) continue;

                foreach (var m in filteredMethods)
                {
                    var methodKey = $"method:{m.Token}";
                    var methodText = $"{m.Name}{m.Signature}";
                    var methodLabel = HighlightHelper.HighlightSubstring(methodText, searchQuery);

                    rows.Add(new IlTreeRow(methodKey, 2, IlTreeRowKind.Method, methodLabel,
                        m, CanExpand: false, IsExpanded: false, ExpansionKey: ""));
                }
            }
        }

        return rows;
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
            IlTreeRowKind.Method when row.Method?.Token == state.IlSelectedMethod?.Token => "● ",
            IlTreeRowKind.Method when row.Symbol is { } nsym
                && nsym.VirtualAddress == state.IlSelectedNativeSymbol?.VirtualAddress => "● ",
            IlTreeRowKind.Type when row.Method is null
                && state.IlSelectedMethod?.DeclaringType is { } dt
                && row.Key == $"type:{dt}" => "● ",
            IlTreeRowKind.Namespace when state.IlSelectedMethod is { } sm
                && IsMethodInNamespace(sm, row.Label, state) => "● ",
            _ => ""
        };
        return $"{indent}{glyph}{marker}{row.Label}";
    }

    /// <summary>
    /// Checks whether the given method belongs to the namespace identified by label.
    /// </summary>
    private static bool IsMethodInNamespace(MethodDefInfo method, string nsLabel, DotsiderState state)
    {
        var td = state.Analyzer.TypeDefs.FirstOrDefault(t => t.FullName == method.DeclaringType);
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
                state.IlFocusedTreeKey = row.Key;
                break;
            case IlTreeRowKind.Method when row.Symbol is not null:
                state.IlSelectedNativeSymbol = row.Symbol;
                state.IlFocusedTreeKey = row.Key;
                break;
            case IlTreeRowKind.Namespace:
            case IlTreeRowKind.Type:
                // Toggle expansion
                var current = GetExpansionState(state, row.ExpansionKey,
                    defaultExpanded: row.Kind == IlTreeRowKind.Namespace);
                state.IlTreeExpansionState[row.ExpansionKey] = !current;
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
    /// Builds the right pane with an EditorWidget for IL disassembly.
    /// Creates or reuses EditorState based on the selected method.
    /// </summary>
    private static Hex1bWidget[] BuildEditorPane<T>(
        WidgetContext<T> ctx, DotsiderState state) where T : Hex1bWidget
    {
        // Native (non-managed) mode: render the selected symbol's native disassembly.
        if (state.Analyzer.BinaryKind != BinaryKind.Managed)
            return BuildNativeEditorPane(ctx, state);

        if (state.IlSelectedMethod is not { } method)
        {
            if (state.IlSelectedField is { } field)
            {
                // Only recreate editor state when the field changes
                if (state.IlEditorField?.Token != field.Token
                    || !ReferenceEquals(state.IlEditorAnalyzer, state.Analyzer))
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
                    state.IlEditorAnalyzer = state.Analyzer;
                    state.IlEditorKey = state.GetOrCreateEditorKey(state.Analyzer, field.Token);
                    state.IlCachedEditors.Remove(state.IlEditorKey);
                }

                return WrapInStatePanels(state);
            }

            return [ctx.Text("  Select a method to view IL disassembly").FillHeight()];
        }

        // Create new editor state when the method changes or when the analyzer
        // was replaced (e.g. after SaveHexChanges swaps in a new image).
        if (state.IlEditorMethod?.Token != method.Token
            || !ReferenceEquals(state.IlEditorAnalyzer, state.Analyzer))
        {
            // Save outgoing editor to cache
            if (state.IlEditorKey is not null && state.IlEditorState is not null)
                state.IlCachedEditors[state.IlEditorKey] = state.IlEditorState;

            var result = state.IlDisassembler!.DisassembleWithText(method);
            var disassembly = result?.Text ?? state.IlDisassembler.FormatDisassembly(method);
            var doc = new Hex1bDocument(disassembly);
            state.IlEditorState = new EditorState(doc) { IsReadOnly = true };
            state.IlEditorMethod = method;
            state.IlEditorField = null;
            state.IlEditorAnalyzer = state.Analyzer;
            state.IlInstructions = result?.Instructions;
            state.IlHeaderLineCount = result?.HeaderLineCount ?? 0;
            state.IlNavigationProvider.Instructions = state.IlInstructions;
            state.IlNavigationProvider.HeaderLineCount = state.IlHeaderLineCount;
            state.IlSourceLinkProvider.Instructions = state.IlInstructions;
            state.IlEditorKey = state.GetOrCreateEditorKey(state.Analyzer, method.Token);
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
    /// Sets <paramref name="sp"/>'s <see cref="ScrollPanelNode.Offset"/> so the row at
    /// <paramref name="rowIndex"/> sits inside the viewport. No-op when the row is
    /// already visible. The setter clamps to <c>[0, MaxOffset]</c> internally.
    /// </summary>
    /// <param name="sp">The IL tree's scroll panel.</param>
    /// <param name="rowIndex">The target row index in the flattened tree.</param>
    internal static void EnsureSelectionVisible(ScrollPanelNode sp, int rowIndex)
    {
        if (rowIndex < sp.Offset)
            sp.Offset = rowIndex;
        else if (rowIndex >= sp.Offset + sp.ViewportSize)
            sp.Offset = rowIndex - sp.ViewportSize + 1;
    }

    /// <summary>
    /// Convenience overload that resolves the panel from
    /// <see cref="DotsiderState.IlScrollPanelNode"/>. No-op when the panel is not yet
    /// captured — callers wanting first-arrival scroll should also arm
    /// <see cref="DotsiderState.IlScrollSelectionIntoViewPending"/> via
    /// <see cref="DotsiderState.SetIlFocusedTreeKey"/>.
    /// </summary>
    /// <param name="state">The shared application state.</param>
    /// <param name="rowIndex">The target row index in the flattened tree.</param>
    internal static void EnsureSelectionVisible(DotsiderState state, int rowIndex)
    {
        if (state.IlScrollPanelNode is { } sp)
            EnsureSelectionVisible(sp, rowIndex);
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
    private static List<int> CollectNativeMatches(DotsiderState state, string query)
    {
        if (state.IlEditorState is not { } editor) return [];
        var text = editor.Document.GetText();
        var offsets = new List<int>();
        for (var i = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
             i >= 0;
             i = text.IndexOf(query, i + query.Length, StringComparison.OrdinalIgnoreCase))
            offsets.Add(i);
        return offsets;
    }

    /// <summary>Moves the native listing's cursor to the next/previous confirmed-search offset (n/N).</summary>
    private static void NavigateToNativeMatch(DotsiderState state, bool forward)
    {
        var offsets = state.IlNativeSearchOffsets;
        if (offsets.Count == 0 || state.IlEditorState is not { } editor) return;

        state.IlCurrentMatchIndex = forward
            ? (state.IlCurrentMatchIndex + 1) % offsets.Count
            : state.IlCurrentMatchIndex <= 0 ? offsets.Count - 1 : state.IlCurrentMatchIndex - 1;
        editor.SetCursorPosition(new DocumentOffset(offsets[state.IlCurrentMatchIndex]));
        state.App.RequestFocus(node => node is EditorNode);
        state.App.Invalidate();
    }

    private static List<IlMatch> CollectTextMatches(DotsiderState state, string query)
    {
        var result = new List<IlMatch>();
        var methodsByType = state.Analyzer.MethodDefs
            .GroupBy(m => m.DeclaringType)
            .ToDictionary(g => g.Key, g => g.ToList());

        var typesByNamespace = state.Analyzer.TypeDefs
            .GroupBy(td => string.IsNullOrEmpty(td.Namespace) ? "(global)" : td.Namespace)
            .OrderBy(g => g.Key);

        foreach (var nsGroup in typesByNamespace)
        {
            foreach (var typeDef in nsGroup)
            {
                if (!methodsByType.TryGetValue(typeDef.FullName, out var methods)) continue;

                foreach (var method in methods)
                {
                    var disassembly = state.IlDisassembler!.FormatDisassembly(method);
                    var lines = disassembly.Split('\n');

                    for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
                    {
                        var line = lines[lineIdx];
                        var pos = 0;
                        while (pos < line.Length)
                        {
                            var idx = line.IndexOf(query, pos, StringComparison.OrdinalIgnoreCase);
                            if (idx < 0) break;

                            result.Add(new IlMatch(method, lineIdx + 1, idx + 1, query.Length));
                            pos = idx + query.Length;
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
        // Switch method if needed
        if (state.IlSelectedMethod?.Token != match.Method.Token)
        {
            state.IlSelectedMethod = match.Method;

            // Expand namespace and type in the tree
            var typeDef = state.Analyzer.TypeDefs.FirstOrDefault(t => t.FullName == match.Method.DeclaringType);
            var ns = typeDef is not null && !string.IsNullOrEmpty(typeDef.Namespace)
                ? typeDef.Namespace : "(global)";
            state.IlTreeExpansionState[$"ns:{ns}"] = true;
            state.IlTreeExpansionState[$"type:{match.Method.DeclaringType}"] = true;

            // When method switches, the old editor moves to the hidden cache.
            // Request focus on the visible editor so input stays on the displayed method.
            state.App.RequestFocus(node => node is EditorNode);
        }

        // Focus the method row in the tree table
        state.SetIlFocusedTreeKey($"method:{match.Method.Token}");

        // Set pending cursor match — consumed by BuildEditorPane on next frame
        state.IlPendingCursorMatch = match;

        state.App.Invalidate();
    }
}
