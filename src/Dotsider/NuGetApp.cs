using Dotsider.Views;
using Hex1b;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// Root application class for NuGet package mode. Browse package contents and inspect DLLs.
/// </summary>
/// <remarks>
/// Creates a new NuGet application with the specified state.
/// </remarks>
/// <param name="state">The NuGet state holding the package analyzer and UI state.</param>
public sealed class NuGetApp(NuGetState state)
{
    private readonly NuGetState _state = state;

    /// <summary>
    /// Builds the root widget tree for the NuGet package browser.
    /// </summary>
    /// <param name="ctx">The Hex1b root context for widget construction.</param>
    /// <returns>The root widget of the NuGet application.</returns>
    public Hex1bWidget Build(RootContext ctx) =>
        ctx.VStack(outer =>
        [
            // Title bar
            outer.InfoBar(bar =>
            [
                bar.Section(" dotsider nupkg ").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.Black)
                    .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(160, 100, 200))),
                bar.Separator(" "),
                bar.Section(_state.Package.FileName).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(80, 80, 100))),
                bar.Spacer(),
                bar.Section(_state.IsBrowsingPackage ? "Package Browser" : "DLL Inspector").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(130, 110, 30))),
                bar.Separator(" | "),
                bar.Section($"{_state.Package.DllFiles.Count} DLLs").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(130, 110, 30)))
            ]),

            // Main content: browser or inspector
            _state.IsBrowsingPackage
                ? NuGetBrowserView.Build(outer, _state)
                : BuildDllInspector(outer),

            // Hints bar
            outer.InfoBar(s =>
            {
                var hints = new List<IInfoBarChild>();
                hints.Add(s.Section(_state.IsBrowsingPackage ? "Enter: Open DLL" : "1-5: Tabs"));
                hints.Add(s.Section("Backspace: Back"));
                if (_state.IsBrowsingPackage)
                {
                    if (_state.BrowserSearch.IsActive)
                        hints.Add(s.Section("Esc: Clear"));
                    hints.Add(s.Section("/: Search"));
                }
                hints.Add(s.Spacer());

                // Navigation error in DLL inspector (right side)
                if (!_state.IsBrowsingPackage && _state.SelectedDllState is { NavigationError: { } navError })
                {
                    hints.Add(s.Section(navError).Theme(t => t
                        .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(200, 80, 60))));
                    hints.Add(s.Separator(" "));
                }

                hints.Add(s.Section("q: Quit"));
                return hints;
            }).WithDefaultSeparator(" | ")
        ])
        .WithInputBindings(bindings =>
        {
            var browserSearch = _state.BrowserSearch;
            var isSearchEditing = browserSearch.IsActive && !browserSearch.IsConfirmed;

            if (_state.IsBrowsingPackage)
            {
                // Search toggle (same dual-binding strategy as DotsiderApp/DiffApp)
                Action searchToggle = () =>
                {
                    browserSearch.ActivateOrCycle();
                    if (browserSearch.IsActive && !browserSearch.IsConfirmed)
                        _state.App.RequestFocus(node => node is TextBoxNode);
                    _state.App.Invalidate();
                };
                bindings.Key(Hex1bKey.OemQuestion).Global().OverridesCapture().Action(_ => searchToggle(), "Search");
                if (!isSearchEditing)
                {
                    bindings.Key(Hex1bKey.None).Global().OverridesCapture().Action(_ => searchToggle(), "Search");
                }
                if (isSearchEditing)
                {
                    bindings.Key(Hex1bKey.Enter).Global().OverridesCapture().Action(_ =>
                    {
                        if (!string.IsNullOrEmpty(browserSearch.Query))
                        {
                            browserSearch.Confirm();
                            _state.App.Invalidate();
                        }
                    }, "Confirm search");
                }

                bindings.Key(Hex1bKey.Escape).OverridesCapture().Action(_ =>
                {
                    if (browserSearch.IsActive)
                    {
                        browserSearch.Dismiss();
                        _state.App.Invalidate();
                    }
                }, "Esc");

                bindings.Key(Hex1bKey.Enter).Action(_ =>
                {
                    // Filter against search query so Enter cannot open a hidden DLL
                    var visibleDlls = (IReadOnlyList<Analysis.Models.NuGetFileEntry>)_state.Package.DllFiles;
                    var q = browserSearch.Query;
                    if (!string.IsNullOrEmpty(q))
                    {
                        visibleDlls = visibleDlls.Where(d =>
                            d.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            d.Directory.Contains(q, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }

                    var focusedKey = _state.FileTreeFocusedKey as string;
                    var entry = focusedKey is not null
                        ? visibleDlls.FirstOrDefault(d => d.FullPath == focusedKey)
                        : visibleDlls.FirstOrDefault();

                    if (entry is null) return;

                    try
                    {
                        var analyzer = _state.Package.OpenDll(entry);
                        _state.SelectedDllState?.Dispose();
                        _state.SelectedDllState = new DotsiderState(_state.App, analyzer);
                        _state.SelectedDllEntry = entry;
                        _state.IsBrowsingPackage = false;
                        _state.App.Invalidate();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to open DLL: {ex.Message}");
                    }
                }, "Open DLL");
            }

            bindings.Key(Hex1bKey.Backspace).Action(_ =>
            {
                if (!_state.IsBrowsingPackage)
                {
                    _state.SelectedDllState?.Dispose();
                    _state.SelectedDllState = null;
                    _state.SelectedDllEntry = null;
                    _state.IsBrowsingPackage = true;
                    _state.App.Invalidate();
                }
            }, "Back to package");

            if (!_state.IsBrowsingPackage && _state.SelectedDllState is not null)
            {
                for (var i = 0; i < 5; i++)
                {
                    var tabIndex = i;
                    var key = (Hex1bKey)((int)Hex1bKey.D1 + i);
                    bindings.Key(key).Global().Action(_ =>
                    {
                        _state.SelectedDllState!.CurrentTab = tabIndex;
                        _state.App.Invalidate();
                    }, $"Tab {tabIndex + 1}");
                }
            }

            if (!isSearchEditing)
                bindings.Key(Hex1bKey.Q).Global().Action(ctx => ctx.RequestStop(), "Quit");
            bindings.Ctrl().Key(Hex1bKey.C).Global().OverridesCapture()
                .Action(ctx => ctx.RequestStop(), "Quit");
        });

    private Hex1bWidget BuildDllInspector(WidgetContext<VStackWidget> outer)
    {
        if (_state.SelectedDllState is null)
            return outer.Text("  No DLL selected").Fill();

        var dllState = _state.SelectedDllState;

        return outer.TabPanel(tp =>
        [
            tp.Tab("General", t => [GeneralView.Build(t, dllState)])
                .Selected(dllState.CurrentTab == 0),
            tp.Tab("PE/Metadata", t => [PeMetadataView.Build(t, dllState)])
                .Selected(dllState.CurrentTab == 1),
            tp.Tab("IL Inspector", t => [IlInspectorView.Build(t, dllState)])
                .Selected(dllState.CurrentTab == 2),
            tp.Tab("Strings", t => [StringsView.Build(t, dllState)])
                .Selected(dllState.CurrentTab == 3),
            tp.Tab("Hex Dump", t => [HexDumpView.Build(t, dllState)])
                .Selected(dllState.CurrentTab == 4)
        ])
        .OnSelectionChanged(e =>
        {
            dllState.CurrentTab = e.SelectedIndex;
            _state.App.Invalidate();
        })
        .Full()
        .Fill();
    }
}
