using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the PE/Metadata tab (Tab 2), showing PE headers, CLR header,
/// and sub-tabbed metadata tables for sections, types, methods, and more.
/// </summary>
public static class PeMetadataView
{
    private static readonly Hex1bColor LabelColor = Hex1bColor.FromRgb(100, 130, 160);
    private static readonly Hex1bColor AddressColor = Hex1bColor.FromRgb(100, 100, 130);

    /// <summary>
    /// Builds the PE/Metadata view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context for building widgets.</param>
    /// <param name="state">The shared application state.</param>
    /// <returns>The root widget for the PE/Metadata tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var analyzer = state.Analyzer;
        var search = state.Search[TabId.PeMetadata];

        // Set up match navigation
        state.NavigateNextMatch = null;
        state.NavigatePrevMatch = null;

        return ctx.ZStack(z =>
        [
            // Layer 0: Main content
            z.VStack(outer =>
            {
                var widgets = new List<Hex1bWidget>();

                // Top section: PE Headers | CLR Header (side by side)
                widgets.Add(outer.HSplitter(
                    left =>
                    [
                        left.Border(
                            left.VScrollPanel(scroll =>
                            {
                                var lines = new List<Hex1bWidget>();
                                if (analyzer.PeHeaders is { } pe)
                                {
                                    lines.Add(PeLine(scroll, "Machine", pe.Machine.ToString()));
                                    lines.Add(PeLine(scroll, "Magic", pe.Magic.ToString()));
                                    lines.Add(PeLine(scroll, "Characteristics", pe.Characteristics.ToString()));
                                    lines.Add(PeLine(scroll, "Timestamp", $"0x{pe.TimeDateStamp:X8}"));
                                    lines.Add(PeLine(scroll, "Linker Version", $"{pe.MajorLinkerVersion}.{pe.MinorLinkerVersion}"));
                                    lines.Add(PeLine(scroll, "Size of Code", FormatSize(pe.SizeOfCode, state)));
                                    lines.Add(PeLine(scroll, "Entry Point RVA", $"0x{pe.EntryPointRva:X8}"));
                                    lines.Add(PeLine(scroll, "Image Base", $"0x{pe.ImageBase:X16}"));
                                    lines.Add(PeLine(scroll, "Section Alignment", FormatSize(pe.SectionAlignment, state)));
                                    lines.Add(PeLine(scroll, "File Alignment", FormatSize(pe.FileAlignment, state)));
                                    lines.Add(PeLine(scroll, "Size of Image", FormatSize(pe.SizeOfImage, state)));
                                    lines.Add(PeLine(scroll, "Size of Headers", FormatSize(pe.SizeOfHeaders, state)));
                                    lines.Add(PeLine(scroll, "Subsystem", pe.Subsystem.ToString()));
                                    lines.Add(PeLine(scroll, "DLL Characteristics", pe.DllCharacteristics.ToString()));
                                    lines.Add(PeLine(scroll, "Number of Sections", pe.NumberOfSections.ToString()));
                                }
                                else
                                {
                                    lines.Add(scroll.Text("  No PE headers available"));
                                }
                                return lines.ToArray();
                            })
                        ).Title(" PE Headers ").Fill()
                    ],
                    right =>
                    [
                        right.Border(
                            right.VScrollPanel(scroll =>
                            {
                                var lines = new List<Hex1bWidget>();
                                if (analyzer.ClrHeader is { } clr)
                                {
                                    lines.Add(PeLine(scroll, "Runtime Version", $"{clr.MajorRuntimeVersion}.{clr.MinorRuntimeVersion}"));
                                    lines.Add(PeLine(scroll, "Metadata RVA", $"0x{clr.MetadataRva:X8}"));
                                    lines.Add(PeLine(scroll, "Metadata Size", FormatSize(clr.MetadataSize, state)));
                                    lines.Add(PeLine(scroll, "Flags", clr.Flags.ToString()));
                                    lines.Add(PeLine(scroll, "Entry Point Token", $"0x{clr.EntryPointToken:X8}"));
                                    lines.Add(PeLine(scroll, "Resources RVA", $"0x{clr.ResourcesRva:X8}"));
                                    lines.Add(PeLine(scroll, "Resources Size", FormatSize(clr.ResourcesSize, state)));
                                    lines.Add(PeLine(scroll, "Strong Name RVA", $"0x{clr.StrongNameSignatureRva:X8}"));
                                    lines.Add(PeLine(scroll, "Strong Name Size", FormatSize(clr.StrongNameSignatureSize, state)));
                                }
                                else
                                {
                                    lines.Add(scroll.Text("  No CLR header (not a .NET assembly)"));
                                }
                                return lines.ToArray();
                            })
                        ).Title(" CLR Header ").Fill()
                    ],
                    leftWidth: 50).FixedHeight(12));

                // Search bar (shared helper)
                SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

                // Bottom section: Metadata tables in sub-tabs
                widgets.Add(outer.TabPanel(tp =>
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
                        .Selected(state.PeSubTab == PeSubTabId.Resources)
                ])
                .OnSelectionChanged(e =>
                {
                    state.PeSubTab = e.SelectedIndex;
                    search.Reset();
                    state.PeFocusedKey = null;
                    state.App.Invalidate();
                })
                .Compact()
                .Fill());

                return widgets.ToArray();
            })
            .WithInputBindings(bindings =>
            {
                var isSearchEditing = search.IsActive && !search.IsConfirmed;

                if (!isSearchEditing)
                {
                    bindings.Key(Hex1bKey.LeftArrow).Global().Action(_ =>
                    {
                        if (state.PeSubTab > 0)
                        {
                            state.PeSubTab--;
                            search.Reset();
                            state.PeFocusedKey = null;
                            state.App.Invalidate();
                        }
                    }, "Previous sub-tab");

                    bindings.Key(Hex1bKey.RightArrow).Global().Action(_ =>
                    {
                        if (state.PeSubTab < PeSubTabId.Count - 1)
                        {
                            state.PeSubTab++;
                            search.Reset();
                            state.PeFocusedKey = null;
                            state.App.Invalidate();
                        }
                    }, "Next sub-tab");

                    // g: Go to IL Inspector for focused TypeDef or MethodDef
                    if (state.PeSubTab is PeSubTabId.TypeDef or PeSubTabId.MethodDef)
                    {
                        bindings.Key(Hex1bKey.G).Global().Action(_ =>
                        {
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

                bindings.Key(Hex1bKey.Escape).OverridesCapture().Action(_ =>
                {
                    if (search.IsActive)
                    {
                        search.Dismiss();
                        state.App.Invalidate();
                        return;
                    }
                    if (state.PeDetailContent is not null)
                    {
                        state.PeDetailContent = null;
                        state.App.Invalidate();
                    }
                }, "Esc");
            })
            .Fill(),

            // Layer 1: Detail popup overlay (conditional)
            state.PeDetailContent is not null
                ? z.Backdrop(
                    z.Border(
                        z.VScrollPanel(scroll =>
                            state.PeDetailContent.Split('\n')
                                .Select(line => scroll.Text($"  {line}"))
                                .ToArray()
                        )
                    ).Title(" Detail ").FixedWidth(60).FixedHeight(12)
                ).OnClickAway(() =>
                {
                    state.PeDetailContent = null;
                    state.App.Invalidate();
                })
                : null
        ]).Fill();
    }

    private static Hex1bWidget BuildSectionsTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
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
            .Row((r, s, _) =>
            [
                r.Cell(c => HighlightHelper.HighlightCell(c, s.Name, query, true)),
                r.Cell(c => HexCell(c, $"0x{s.VirtualAddress:X8}")),
                r.Cell(FormatSize(s.VirtualSize, state)),
                r.Cell(c => HexCell(c, $"0x{s.RawDataOffset:X8}")),
                r.Cell(FormatSize(s.RawDataSize, state)),
                r.Cell(c => HighlightHelper.HighlightCell(c, s.Characteristics.ToString(), query, true))
            ])
            .Focus(state.PeFocusedKey)
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
            })
            .Compact().Fill();
    }

    private static Hex1bWidget BuildTypeDefsTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
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
            .Row((r, t, _) =>
            [
                r.Cell(c => HexCell(c, $"0x{t.Token:X8}")),
                r.Cell(c => HighlightHelper.HighlightCell(c, t.FullName, query, true)),
                r.Cell(c => HighlightHelper.HighlightCell(c, t.BaseType ?? "", query, true)),
                r.Cell(c => HighlightHelper.HighlightCell(c, t.Attributes.ToString(), query, true)),
                r.Cell(t.MethodCount.ToString()),
                r.Cell(t.FieldCount.ToString())
            ])
            .Focus(state.PeFocusedKey)
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
            })
            .Compact().Fill();
    }

    private static Hex1bWidget BuildMethodDefsTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
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
            .Row((r, m, _) =>
            [
                r.Cell(c => HexCell(c, $"0x{m.Token:X8}")),
                r.Cell(c => HighlightHelper.HighlightCell(c, m.DeclaringType, query, true)),
                r.Cell(c => HighlightHelper.HighlightCell(c, m.Name, query, true)),
                r.Cell(c => HighlightHelper.HighlightCell(c, m.Signature, query, true)),
                r.Cell(m.Attributes.ToString()),
                r.Cell(c => m.Rva == 0 ? c.Text("") : HexCell(c, $"0x{m.Rva:X8}"))
            ])
            .Focus(state.PeFocusedKey)
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
            })
            .Compact().Fill();
    }

    private static Hex1bWidget BuildTypeRefsTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
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
            .Row((r, t, _) =>
            [
                r.Cell(c => HexCell(c, $"0x{t.Token:X8}")),
                r.Cell(c => HighlightHelper.HighlightCell(c, t.FullName, query, true)),
                r.Cell(c => HighlightHelper.HighlightCell(c, t.ResolutionScope, query, true))
            ])
            .Focus(state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, t) =>
            {
                state.PeDetailContent = string.Join("\n",
                    $"TypeRef: {t.FullName}",
                    $"Token: 0x{t.Token:X8}",
                    $"Namespace: {t.Namespace}",
                    $"Name: {t.Name}",
                    $"Resolution Scope: {t.ResolutionScope}");
            })
            .Compact().Fill();
    }

    private static Hex1bWidget BuildMemberRefsTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
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
            .Row((r, m, _) =>
            [
                r.Cell(c => HexCell(c, $"0x{m.Token:X8}")),
                r.Cell(c => HighlightHelper.HighlightCell(c, m.DeclaringType, query, true)),
                r.Cell(c => HighlightHelper.HighlightCell(c, m.Name, query, true))
            ])
            .Focus(state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, m) =>
            {
                state.PeDetailContent = string.Join("\n",
                    $"MemberRef: {m.DeclaringType}::{m.Name}",
                    $"Token: 0x{m.Token:X8}");
            })
            .Compact().Fill();
    }

    private static Hex1bWidget BuildAttributesTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
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
            .Row((r, a, _) =>
            [
                r.Cell(c => HighlightHelper.HighlightCell(c, a.Parent, query, true)),
                r.Cell(c => HighlightHelper.HighlightCell(c, a.Constructor, query, true)),
                r.Cell(c => HighlightHelper.HighlightCell(c, a.Value ?? "", query, true))
            ])
            .Focus(state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, a) =>
            {
                state.PeDetailContent = string.Join("\n",
                    "Custom Attribute",
                    $"Parent: {a.Parent}",
                    $"Constructor: {a.Constructor}",
                    $"Value: {a.Value ?? "null"}");
            })
            .Compact().Fill();
    }

    private static Hex1bWidget BuildResourcesTable(WidgetContext<VStackWidget> ctx, DotsiderState state)
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
            .Row((r, res, _) =>
            [
                r.Cell(c => HighlightHelper.HighlightCell(c, res.Name, query, true)),
                r.Cell(c => HighlightHelper.HighlightCell(c, res.Visibility, query, true)),
                r.Cell(c => HexCell(c, $"0x{res.Offset:X8}")),
                r.Cell(res.Size >= 0 ? FormatSize((int)res.Size, state) : "?"),
                r.Cell(res.IsLinked ? "Yes" : "No")
            ])
            .Focus(state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, res) =>
            {
                state.PeDetailContent = string.Join("\n",
                    $"Resource: {res.Name}",
                    $"Visibility: {res.Visibility}",
                    $"Offset: 0x{res.Offset:X8}",
                    $"Size: {(res.Size >= 0 ? res.Size.ToString() : "unknown")}",
                    $"Linked: {res.IsLinked}");
            })
            .Compact().Fill();
    }

    private static string FormatSize(int size, DotsiderState state) =>
        state.FormatSizeToggleable(size);

    private static IReadOnlyList<T> ApplySearch<T>(
        IReadOnlyList<T> items, string? query, Func<T, string> toSearchable)
    {
        if (string.IsNullOrEmpty(query)) return items;
        return items
            .Where(i => toSearchable(i).Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static Hex1bWidget HexCell<T>(WidgetContext<T> c, string text) where T : Hex1bWidget =>
        c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, AddressColor), c.Text(text));

    private static Hex1bWidget PeLine<T>(WidgetContext<T> ctx, string label, string value) where T : Hex1bWidget
    {
        return ctx.HStack(row =>
        [
            row.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, LabelColor),
                row.Text($"  {label}: ")).FixedWidth(22),
            row.Text(value)
        ]).FixedHeight(1);
    }
}
