namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="SearchState"/> state machine transitions.
/// </summary>
[TestClass]
public class SearchStateTests
{
    /// <summary>
    /// Verifies initial is inactive.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Initial_IsInactive()
    {
        var s = new SearchState();
        Assert.IsFalse(s.IsActive);
        Assert.IsFalse(s.IsConfirmed);
        Assert.IsNull(s.Query);
        Assert.AreEqual(-1, s.MatchCount);
    }

    /// <summary>
    /// Verifies activate or cycle inactive to editing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ActivateOrCycle_InactiveToEditing()
    {
        var s = new SearchState();
        s.ActivateOrCycle();
        Assert.IsTrue(s.IsActive);
        Assert.IsFalse(s.IsConfirmed);
    }

    /// <summary>
    /// Verifies activate or cycle editing to inactive.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ActivateOrCycle_EditingToInactive()
    {
        var s = new SearchState();
        s.ActivateOrCycle(); // Inactive → Editing
        s.ActivateOrCycle(); // Editing → Inactive
        Assert.IsFalse(s.IsActive);
        Assert.IsFalse(s.IsConfirmed);
        Assert.IsNull(s.Query);
    }

    /// <summary>
    /// Verifies activate or cycle confirmed to editing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ActivateOrCycle_ConfirmedToEditing()
    {
        var s = new SearchState();
        s.ActivateOrCycle(); // Inactive → Editing
        s.UpdateQuery("test");
        s.Confirm();        // Editing → Confirmed
        Assert.IsTrue(s.IsConfirmed);
        s.ActivateOrCycle(); // Confirmed → Editing
        Assert.IsTrue(s.IsActive);
        Assert.IsFalse(s.IsConfirmed);
        Assert.AreEqual("test", s.Query); // Query preserved
    }

    /// <summary>
    /// Verifies confirm sets is confirmed.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Confirm_SetsIsConfirmed()
    {
        var s = new SearchState();
        s.ActivateOrCycle();
        s.UpdateQuery("hello");
        s.Confirm();
        Assert.IsTrue(s.IsActive);
        Assert.IsTrue(s.IsConfirmed);
    }

    /// <summary>
    /// Verifies dismiss clears all.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Dismiss_ClearsAll()
    {
        var s = new SearchState();
        s.ActivateOrCycle();
        s.UpdateQuery("test");
        s.Confirm();
        s.SetMatchCount(5);
        s.Dismiss();
        Assert.IsFalse(s.IsActive);
        Assert.IsFalse(s.IsConfirmed);
        Assert.IsNull(s.Query);
        Assert.AreEqual(-1, s.MatchCount);
    }

    /// <summary>
    /// Verifies reset clears all.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Reset_ClearsAll()
    {
        var s = new SearchState();
        s.ActivateOrCycle();
        s.UpdateQuery("abc");
        s.Confirm();
        s.SetMatchCount(3);
        s.Reset();
        Assert.IsFalse(s.IsActive);
        Assert.IsFalse(s.IsConfirmed);
        Assert.IsNull(s.Query);
        Assert.AreEqual(-1, s.MatchCount);
    }

    /// <summary>
    /// Verifies update query resets confirmation.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void UpdateQuery_ResetsConfirmation()
    {
        var s = new SearchState();
        s.ActivateOrCycle();
        s.UpdateQuery("foo");
        s.Confirm();
        Assert.IsTrue(s.IsConfirmed);
        s.UpdateQuery("bar");
        Assert.IsFalse(s.IsConfirmed);
        Assert.AreEqual("bar", s.Query);
        Assert.AreEqual(-1, s.MatchCount);
    }

    /// <summary>
    /// Verifies set match count stores value.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SetMatchCount_StoresValue()
    {
        var s = new SearchState();
        s.SetMatchCount(42);
        Assert.AreEqual(42, s.MatchCount);
    }

    /// <summary>
    /// Verifies set match count zero means no matches.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SetMatchCount_ZeroMeansNoMatches()
    {
        var s = new SearchState();
        s.SetMatchCount(0);
        Assert.AreEqual(0, s.MatchCount);
    }

    /// <summary>
    /// Verifies activate or cycle inactive to editing clears query.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ActivateOrCycle_InactiveToEditing_ClearsQuery()
    {
        var s = new SearchState();
        s.ActivateOrCycle();   // → Editing
        s.UpdateQuery("test");
        s.ActivateOrCycle();   // → Inactive (clears query)
        Assert.IsNull(s.Query);
        s.ActivateOrCycle();   // → Editing (fresh)
        Assert.IsNull(s.Query);
    }
}
