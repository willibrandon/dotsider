namespace Dotsider;

/// <summary>
/// Manages search state for a single tab using a state machine:
/// Inactive → Editing → Confirmed → Editing (cycle) or → Inactive.
/// Composed into state objects (DotsiderState, DiffState), not subclassed.
/// </summary>
public sealed class SearchState
{
    /// <summary>The current search query text, or null if no query has been entered.</summary>
    public string? Query { get; private set; }

    /// <summary>Whether search mode is currently active (editing or confirmed).</summary>
    public bool IsActive { get; private set; }

    /// <summary>Whether the search query has been confirmed (TextBox replaced with static text).</summary>
    public bool IsConfirmed { get; private set; }

    /// <summary>
    /// The number of matches found, or -1 if not yet computed.
    /// A value of 0 means the search was performed but found no matches.
    /// </summary>
    public int MatchCount { get; private set; } = -1;

    /// <summary>
    /// Cycles the search state: Inactive → Editing, Editing → Inactive, Confirmed → Editing.
    /// When transitioning to Inactive, clears the query and match count.
    /// When transitioning from Confirmed to Editing, preserves the query.
    /// </summary>
    public void ActivateOrCycle()
    {
        if (IsActive && IsConfirmed) { IsConfirmed = false; return; }
        IsActive = !IsActive;
        if (!IsActive) { Query = null; IsConfirmed = false; MatchCount = -1; }
    }

    /// <summary>
    /// Confirms the current search query, transitioning from Editing to Confirmed state.
    /// </summary>
    public void Confirm() => IsConfirmed = true;

    /// <summary>
    /// Dismisses the search entirely, clearing all state back to Inactive.
    /// </summary>
    public void Dismiss()
    {
        IsActive = false;
        IsConfirmed = false;
        Query = null;
        MatchCount = -1;
    }

    /// <summary>
    /// Updates the search query text and resets confirmation and match count.
    /// </summary>
    /// <param name="text">The new query text.</param>
    public void UpdateQuery(string? text)
    {
        Query = text;
        IsConfirmed = false;
        MatchCount = -1;
    }

    /// <summary>
    /// Sets the match count after a search has been performed.
    /// </summary>
    /// <param name="count">The number of matches found.</param>
    public void SetMatchCount(int count) => MatchCount = count;

    /// <summary>
    /// Resets all search state back to defaults. Used when navigating to a new assembly.
    /// </summary>
    public void Reset()
    {
        Query = null;
        IsActive = false;
        IsConfirmed = false;
        MatchCount = -1;
    }
}
