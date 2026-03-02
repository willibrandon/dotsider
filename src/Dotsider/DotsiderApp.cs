using Dotsider.Views;
using Hex1b;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// The root application class that builds the entire dotsider widget tree.
/// Manages the top-level layout: title bar, tab panel, and keybinding hints bar.
/// </summary>
public sealed class DotsiderApp
{
    private readonly DotsiderState _state;

    /// <summary>
    /// Creates a new dotsider application with the specified state.
    /// </summary>
    /// <param name="state">The application state holding the analyzer and all UI state.</param>
    public DotsiderApp(DotsiderState state)
    {
        _state = state;
    }

    /// <summary>
    /// Builds the root widget tree for the current frame.
    /// </summary>
    /// <param name="ctx">The Hex1b root context for widget construction.</param>
    /// <returns>The root widget of the application.</returns>
    public Hex1bWidget Build(RootContext ctx)
    {
        return ctx.VStack(outer =>
        [
            // Title bar
            outer.InfoBar(bar =>
            [
                bar.Section($" dotsider ").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.Black)
                    .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(0, 200, 180))),
                bar.Separator(" "),
                bar.Section(_state.NavigationStack.Count > 0
                    ? $"{_state.Analyzer.FileName} (depth {_state.NavigationStack.Count + 1})"
                    : _state.Analyzer.FileName).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(180, 180, 200))),
                bar.Spacer(),
                bar.Section(_state.Analyzer.Architecture).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(200, 180, 100))),
                bar.Separator(" | "),
                bar.Section(DotsiderState.FormatSize(_state.Analyzer.FileSize)).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(200, 180, 100)))
            ]),

            // Main content: Tab panel with 7 tabs (controlled via CurrentTab)
            outer.TabPanel(tp =>
            [
                tp.Tab("General", t => [GeneralView.Build(t, _state)])
                    .Selected(_state.CurrentTab == 0),
                tp.Tab("PE/Metadata", t => [PeMetadataView.Build(t, _state)])
                    .Selected(_state.CurrentTab == 1),
                tp.Tab("IL Inspector", t => [IlInspectorView.Build(t, _state)])
                    .Selected(_state.CurrentTab == 2),
                tp.Tab("Strings", t => [StringsView.Build(t, _state)])
                    .Selected(_state.CurrentTab == 3),
                tp.Tab("Hex Dump", t => [HexDumpView.Build(t, _state)])
                    .Selected(_state.CurrentTab == 4),
                tp.Tab("Dep Graph", t => [DependencyGraphView.Build(t, _state)])
                    .Selected(_state.CurrentTab == 5),
                tp.Tab("Size Map", t => [SizeTreemapView.Build(t, _state)])
                    .Selected(_state.CurrentTab == 6)
            ])
            .OnSelectionChanged(e =>
            {
                _state.CurrentTab = e.SelectedIndex;
                _state.App.Invalidate();
            })
            .Full()
            .Fill(),

            // Keybinding hints bar
            BuildHintsBar(outer)
        ])
        .WithInputBindings(bindings =>
        {
            // Number keys 1-7 to switch tabs
            for (var i = 0; i < 7; i++)
            {
                var tabIndex = i;
                var key = (Hex1bKey)((int)Hex1bKey.D1 + i);
                bindings.Key(key).Global().Action(_ =>
                {
                    _state.CurrentTab = tabIndex;
                    _state.App.Invalidate();
                }, $"Tab {tabIndex + 1}");
            }

            // Global keybindings matching binsider
            bindings.Key(Hex1bKey.Q).Global().Action(ctx => ctx.RequestStop(), "Quit");
            bindings.Ctrl().Key(Hex1bKey.C).Global().OverridesCapture()
                .Action(ctx => ctx.RequestStop(), "Quit");
        });
    }

    private Hex1bWidget BuildHintsBar(WidgetContext<VStackWidget> ctx)
    {
        return ctx.InfoBar(s =>
        {
            var hints = new List<IInfoBarChild>();
            hints.Add(s.Section("1-7: Tabs"));

            if (_state.NavigationStack.Count > 0)
                hints.Add(s.Section("Backspace: Back"));

            if (_state.CurrentTab is 1 or 3)
            {
                hints.Add(s.Section("Enter: Detail"));
                hints.Add(s.Section("/: Search"));
            }
            else if (_state.CurrentTab == 2)
            {
                hints.Add(s.Section("/: Search"));
            }
            else if (_state.CurrentTab == 6)
            {
                hints.Add(s.Section("Backspace: Up"));
            }

            hints.Add(s.Section("s: Sizes"));
            hints.Add(s.Spacer());
            hints.Add(s.Section("q: Quit"));
            return hints;
        }).WithDefaultSeparator(" | ");
    }
}
