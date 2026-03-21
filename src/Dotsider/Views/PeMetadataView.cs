using System.Text;
using Dotsider.Core.Analysis.Models;
using Hex1b;
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
    private static readonly Hex1bColor LabelColor = Hex1bColor.FromRgb(100, 130, 160);
    private static readonly Hex1bColor AddressColor = Hex1bColor.FromRgb(100, 100, 130);
    private static readonly string AddressAnsi = AddressColor.ToForegroundAnsi();
    private const string AnsiReset = "\x1b[0m";

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
                    _ => null
                };
        }

        return ctx.ZStack(z =>
        [
            // Layer 0: Main content
            z.VStack(outer =>
            {
                var widgets = new List<Hex1bWidget>
                {
                    // Top section: PE Headers | CLR Header (side by side)
                    outer.HSplitter(
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
                                return [.. lines];
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
                                return [.. lines];
                            })
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
                        .Selected(state.PeSubTab == PeSubTabId.Resources)
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

                // Suppress the teal selected-tab highlight when the detail popup
                // is open so it doesn't bleed through the transparent backdrop.
                if (state.PeDetailContent is not null)
                {
                    metadataTabs = outer.ThemePanel(t => t
                        .Set(TabBarTheme.SelectedForegroundColor, Hex1bColor.FromRgb(140, 140, 160))
                        .Set(TabBarTheme.SelectedBackgroundColor, Hex1bColor.Default),
                        metadataTabs);
                }

                widgets.Add(metadataTabs);

                return [.. widgets];
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
                            state.RequestContentFocus();
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
                            state.RequestContentFocus();
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

                // Detail popup dismiss — only register when search is not active
                // to avoid conflicting with DotsiderApp's global "Clear search" binding
                if (!search.IsActive && state.PeDetailContent is not null)
                {
                    bindings.Key(Hex1bKey.Escape).Global().OverridesCapture().Action(_ =>
                    {
                        state.PeDetailContent = null;
                        state.RequestContentFocus();
                        state.App.Invalidate();
                    }, "Dismiss detail");
                }
            })
            .Fill(),

            // Layer 1: Detail popup overlay (conditional)
            state.PeDetailContent is not null
                ? z.Backdrop(
                    z.Border(
                        z.VStack(dlg =>
                            [.. state.PeDetailContent.Split('\n')
                                .Select(line => DetailLine(dlg, line))]
                        )
                    ).Title(" Detail ").FixedWidth(60).FixedHeight(12)
                ).OnClickAway(() =>
                {
                    state.PeDetailContent = null;
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
                r.Cell(c => FocusStyle(c, HexCell(c, $"0x{s.VirtualAddress:X8}"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(FormatSize(s.VirtualSize, state)), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, HexCell(c, $"0x{s.RawDataOffset:X8}"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(FormatSize(s.RawDataSize, state)), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,s.Characteristics.ToString(), query, true, rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null ? null : state.PeFocusedKey)
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
                r.Cell(c => FocusStyle(c, HexCell(c, $"0x{t.Token:X8}"), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,t.FullName, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,t.BaseType ?? "", query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,t.Attributes.ToString(), query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(t.MethodCount.ToString()), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(t.FieldCount.ToString()), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null ? null : state.PeFocusedKey)
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
                r.Cell(c => FocusStyle(c, HexCell(c, $"0x{m.Token:X8}"), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,m.DeclaringType, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,m.Name, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,m.Signature, query, true, rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(m.Attributes.ToString()), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, m.Rva == 0 ? c.Text("") : HexCell(c, $"0x{m.Rva:X8}"), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null ? null : state.PeFocusedKey)
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
                r.Cell(c => FocusStyle(c, HexCell(c, $"0x{t.Token:X8}"), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,t.FullName, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,t.ResolutionScope, query, true, rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, t) =>
            {
                state.PeDetailContent = string.Join("\n",
                    $"TypeRef: {t.FullName}",
                    $"Token: 0x{t.Token:X8}",
                    $"Namespace: {t.Namespace}",
                    $"Name: {t.Name}",
                    $"Resolution Scope: {t.ResolutionScope}");
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
                r.Cell(c => FocusStyle(c, HexCell(c, $"0x{m.Token:X8}"), rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,m.DeclaringType, query, true, rs.IsFocused)),
                r.Cell(c => FocusHighlightCell(c,m.Name, query, true, rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, m) =>
            {
                state.PeDetailContent = string.Join("\n",
                    $"MemberRef: {m.DeclaringType}::{m.Name}",
                    $"Token: 0x{m.Token:X8}");
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
            .Focus(state.PeDetailContent is not null ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, a) =>
            {
                state.PeDetailContent = string.Join("\n",
                    "Custom Attribute",
                    $"Parent: {a.Parent}",
                    $"Constructor: {a.Constructor}",
                    $"Value: {a.Value ?? "null"}");
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
                r.Cell(c => FocusStyle(c, HexCell(c, $"0x{res.Offset:X8}"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(res.Size >= 0 ? FormatSize((int)res.Size, state) : "?"), rs.IsFocused)),
                r.Cell(c => FocusStyle(c, c.Text(res.IsLinked ? "Yes" : "No"), rs.IsFocused))
            ])
            .Focus(state.PeDetailContent is not null ? null : state.PeFocusedKey)
            .OnFocusChanged(key => state.PeFocusedKey = key)
            .OnRowActivated((_, res) =>
            {
                state.PeDetailContent = string.Join("\n",
                    $"Resource: {res.Name}",
                    $"Visibility: {res.Visibility}",
                    $"Offset: 0x{res.Offset:X8}",
                    $"Size: {(res.Size >= 0 ? res.Size.ToString() : "unknown")}",
                    $"Linked: {res.IsLinked}");
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

    private static Hex1bWidget FocusStyle<T>(WidgetContext<T> c, Hex1bWidget child, bool isFocused)
        where T : Hex1bWidget =>
        isFocused ? c.ThemePanel(t => t
            .Set(GlobalTheme.ForegroundColor, FocusFg)
            .Set(GlobalTheme.BackgroundColor, FocusBg), child) : child;

    private static Hex1bWidget FocusHighlightCell<T>(
        WidgetContext<T> c, string text, string? query, bool isMatch, bool isFocused)
        where T : Hex1bWidget =>
        FocusStyle(c, HighlightHelper.HighlightCell(c, text, query, isMatch,
            isFocused ? FocusFg : null, isFocused ? FocusBg : null), isFocused);

    private static HStackWidget PeLine<T>(WidgetContext<T> ctx, string label, string value) where T : Hex1bWidget
    {
        return ctx.HStack(row =>
        [
            row.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, LabelColor),
                row.Text($"  {label}: ")).FixedWidth(22),
            row.Text(value)
        ]).FixedHeight(1);
    }

    private static Hex1bWidget DetailLine<T>(WidgetContext<T> ctx, string line) where T : Hex1bWidget
    {
        var colonIdx = line.IndexOf(": ", StringComparison.Ordinal);
        if (colonIdx < 0)
            return ctx.Text($"  {line}");

        var label = line[..colonIdx];
        var value = line[(colonIdx + 2)..];

        return ctx.HStack(row =>
        [
            row.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, LabelColor),
                row.Text($"  {label}: ")).FixedWidth(22),
            row.Text(ColorizeHexValues(value))
        ]).FixedHeight(1);
    }

    private static string ColorizeHexValues(string text)
    {
        var sb = new StringBuilder(text.Length + 32);
        var i = 0;
        while (i < text.Length)
        {
            if (i + 2 < text.Length && text[i] == '0' && text[i + 1] is 'x' or 'X')
            {
                var start = i;
                i += 2;
                while (i < text.Length && IsHexDigit(text[i]))
                    i++;
                if (i > start + 2)
                {
                    sb.Append(AddressAnsi);
                    sb.Append(text, start, i - start);
                    sb.Append(AnsiReset);
                }
                else
                {
                    sb.Append(text, start, i - start);
                }
            }
            else
            {
                sb.Append(text[i]);
                i++;
            }
        }

        return sb.ToString();
    }

    private static bool IsHexDigit(char c) =>
        c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

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
            _ => []
        };
    }

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
