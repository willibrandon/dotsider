namespace Dotsider;

/// <summary>
/// Tracks the state of a pending vim text-object sequence (iw, iW, yiw, yiW).
/// </summary>
public enum VimMotionState
{
    /// <summary>No pending motion.</summary>
    Idle,

    /// <summary>Pressed <c>i</c>, expecting <c>w</c> or <c>W</c>.</summary>
    WaitingForTextObject,

    /// <summary>Pressed <c>y</c> on editor without selection, expecting <c>i</c>.</summary>
    WaitingForYMotion,

    /// <summary>Pressed <c>y</c> then <c>i</c>, expecting <c>w</c> or <c>W</c>.</summary>
    WaitingForYTextObject
}
