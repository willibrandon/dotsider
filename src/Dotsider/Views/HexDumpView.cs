using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the Hex Dump tab (Tab 5), displaying the raw assembly bytes
/// in a full-size hex editor with ASCII sidebar.
/// </summary>
public static class HexDumpView
{
    /// <summary>
    /// Builds the Hex Dump view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context from the parent tab panel.</param>
    /// <param name="state">The shared application state.</param>
    /// <returns>The root widget for the Hex Dump tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        return ctx.Editor(state.HexEditorState)
            .WithViewRenderer(new HexEditorViewRenderer())
            .FillWidth()
            .FillHeight();
    }
}
