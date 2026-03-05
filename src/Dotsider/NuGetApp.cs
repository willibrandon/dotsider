using Dotsider.Views;
using Hex1b;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// Root application class for NuGet package mode. Browse package contents and inspect DLLs.
/// </summary>
public sealed class NuGetApp
{
    private readonly NuGetState _state;
    
    /// <summary>
    /// Creates a new NuGet application with the specified state.
    /// </summary>
    /// <param name="state">The NuGet state holding the package analyzer and UI state.</param>
    public NuGetApp(NuGetState state) => _state = state;

    /// <summary>
    /// Builds the root widget tree for the NuGet package browser.
    /// </summary>
    /// <param name="ctx">The Hex1b root context for widget construction.</param>
    /// <returns>The root widget of the NuGet application.</returns>
    public Hex1bWidget Build(RootContext ctx)
    {
        return ctx.VStack(outer =>
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
            [
                s.Section(_state.IsBrowsingPackage ? "Enter: Open DLL" : "1-5: Tabs"),
                s.Separator(" | "),
                s.Section("Backspace: Back"),
                s.Spacer(),
                s.Section("q: Quit")
            ]).WithDefaultSeparator(" | ")
        ])
        .WithInputBindings(bindings =>
        {
            if (_state.IsBrowsingPackage)
            {
                bindings.Key(Hex1bKey.Enter).Action(_ =>
                {
                    var focusedKey = _state.FileTreeFocusedKey as string;
                    var entry = focusedKey is not null
                        ? _state.Package.DllFiles.FirstOrDefault(d => d.FullPath == focusedKey)
                        : _state.Package.DllFiles.FirstOrDefault();

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

            bindings.Key(Hex1bKey.Q).Global().Action(ctx => ctx.RequestStop(), "Quit");
            bindings.Ctrl().Key(Hex1bKey.C).Global().OverridesCapture()
                .Action(ctx => ctx.RequestStop(), "Quit");
        });
    }

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
