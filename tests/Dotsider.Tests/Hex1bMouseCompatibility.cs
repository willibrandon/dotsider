using Hex1b;
using System.Reflection;

namespace Dotsider.Tests;

/// <summary>
/// Makes multi-click integration tests deterministic with Hex1b versions that recompute
/// click counts from wall-clock time instead of honoring the count carried by automation events.
/// </summary>
internal static class Hex1bMouseCompatibility
{
    private static readonly FieldInfo s_lastClickTime = typeof(Hex1bApp)
        .GetField("_lastClickTime", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(Hex1bApp).FullName, "_lastClickTime");

    /// <summary>
    /// Resets Hex1b's click clock so the next real mouse-down begins a new click sequence.
    /// </summary>
    /// <param name="app">The application that will process the mouse event.</param>
    internal static void BeginClickSequence(Hex1bApp app) =>
        s_lastClickTime.SetValue(app, DateTime.MinValue);

    /// <summary>
    /// Moves Hex1b's click clock ahead of the current time so the next real mouse-down
    /// deterministically continues the current click sequence regardless of scheduler delay.
    /// </summary>
    /// <param name="app">The application that will process the mouse event.</param>
    internal static void ContinueClickSequence(Hex1bApp app) =>
        s_lastClickTime.SetValue(app, DateTime.MaxValue);
}
