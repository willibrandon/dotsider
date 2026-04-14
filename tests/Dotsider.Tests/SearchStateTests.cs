namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="SearchState"/> state machine transitions.
/// </summary>
public class SearchStateTests
{
    /// <summary>
    /// Verifies initial is inactive.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Initial_IsInactive()
    {
        var s = new SearchState();
        Assert.False(s.IsActive);
        Assert.False(s.IsConfirmed);
        Assert.Null(s.Query);
        Assert.Equal(-1, s.MatchCount);
    }

    /// <summary>
    /// Verifies activate or cycle inactive to editing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ActivateOrCycle_InactiveToEditing()
    {
        var s = new SearchState();
        s.ActivateOrCycle();
        Assert.True(s.IsActive);
        Assert.False(s.IsConfirmed);
    }

    /// <summary>
    /// Verifies activate or cycle editing to inactive.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ActivateOrCycle_EditingToInactive()
    {
        var s = new SearchState();
        s.ActivateOrCycle(); // Inactive → Editing
        s.ActivateOrCycle(); // Editing → Inactive
        Assert.False(s.IsActive);
        Assert.False(s.IsConfirmed);
        Assert.Null(s.Query);
    }

    /// <summary>
    /// Verifies activate or cycle confirmed to editing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ActivateOrCycle_ConfirmedToEditing()
    {
        var s = new SearchState();
        s.ActivateOrCycle(); // Inactive → Editing
        s.UpdateQuery("test");
        s.Confirm();        // Editing → Confirmed
        Assert.True(s.IsConfirmed);
        s.ActivateOrCycle(); // Confirmed → Editing
        Assert.True(s.IsActive);
        Assert.False(s.IsConfirmed);
        Assert.Equal("test", s.Query); // Query preserved
    }

    /// <summary>
    /// Verifies confirm sets is confirmed.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Confirm_SetsIsConfirmed()
    {
        var s = new SearchState();
        s.ActivateOrCycle();
        s.UpdateQuery("hello");
        s.Confirm();
        Assert.True(s.IsActive);
        Assert.True(s.IsConfirmed);
    }

    /// <summary>
    /// Verifies dismiss clears all.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Dismiss_ClearsAll()
    {
        var s = new SearchState();
        s.ActivateOrCycle();
        s.UpdateQuery("test");
        s.Confirm();
        s.SetMatchCount(5);
        s.Dismiss();
        Assert.False(s.IsActive);
        Assert.False(s.IsConfirmed);
        Assert.Null(s.Query);
        Assert.Equal(-1, s.MatchCount);
    }

    /// <summary>
    /// Verifies reset clears all.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Reset_ClearsAll()
    {
        var s = new SearchState();
        s.ActivateOrCycle();
        s.UpdateQuery("abc");
        s.Confirm();
        s.SetMatchCount(3);
        s.Reset();
        Assert.False(s.IsActive);
        Assert.False(s.IsConfirmed);
        Assert.Null(s.Query);
        Assert.Equal(-1, s.MatchCount);
    }

    /// <summary>
    /// Verifies update query resets confirmation.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void UpdateQuery_ResetsConfirmation()
    {
        var s = new SearchState();
        s.ActivateOrCycle();
        s.UpdateQuery("foo");
        s.Confirm();
        Assert.True(s.IsConfirmed);
        s.UpdateQuery("bar");
        Assert.False(s.IsConfirmed);
        Assert.Equal("bar", s.Query);
        Assert.Equal(-1, s.MatchCount);
    }

    /// <summary>
    /// Verifies set match count stores value.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void SetMatchCount_StoresValue()
    {
        var s = new SearchState();
        s.SetMatchCount(42);
        Assert.Equal(42, s.MatchCount);
    }

    /// <summary>
    /// Verifies set match count zero means no matches.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void SetMatchCount_ZeroMeansNoMatches()
    {
        var s = new SearchState();
        s.SetMatchCount(0);
        Assert.Equal(0, s.MatchCount);
    }

    /// <summary>
    /// Verifies activate or cycle inactive to editing clears query.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ActivateOrCycle_InactiveToEditing_ClearsQuery()
    {
        var s = new SearchState();
        s.ActivateOrCycle();   // → Editing
        s.UpdateQuery("test");
        s.ActivateOrCycle();   // → Inactive (clears query)
        Assert.Null(s.Query);
        s.ActivateOrCycle();   // → Editing (fresh)
        Assert.Null(s.Query);
    }
}
