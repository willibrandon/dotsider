namespace Dotsider.Views;

/// <summary>
/// Describes the result of resolving and starting an embedded-source editor.
/// </summary>
internal enum EditorLaunchStatus
{
    /// <summary>
    /// No eligible configured editor was found.
    /// </summary>
    NotFound,

    /// <summary>
    /// An editor or platform association handler was started.
    /// </summary>
    Started,

    /// <summary>
    /// The configuration was malformed or an eligible target failed to start.
    /// </summary>
    Failed
}
