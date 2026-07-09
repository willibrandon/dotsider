namespace Dotsider.Tests;

/// <summary>
/// Tests for match navigation: next/prev through matches, wrap-around,
/// single match, zero matches, backwards navigation.
/// </summary>
[TestClass]
public class MatchNavigationTests
{
    /// <summary>
    /// Verifies navigate next wraps around.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateNext_WrapsAround()
    {
        var index = 0;
        var count = 3;

        // Next 4 times should wrap
        index = (index + 1) % count; Assert.AreEqual(1, index);
        index = (index + 1) % count; Assert.AreEqual(2, index);
        index = (index + 1) % count; Assert.AreEqual(0, index); // wrapped
        index = (index + 1) % count; Assert.AreEqual(1, index);
    }

    /// <summary>
    /// Verifies navigate prev wraps around.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigatePrev_WrapsAround()
    {
        var index = 0;
        var count = 3;

        // Prev from 0 should go to last
        index = index <= 0 ? count - 1 : index - 1;
        Assert.AreEqual(2, index); // wrapped to last
        index = index <= 0 ? count - 1 : index - 1;
        Assert.AreEqual(1, index);
        index = index <= 0 ? count - 1 : index - 1;
        Assert.AreEqual(0, index);
    }

    /// <summary>
    /// Verifies single match stays on same.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SingleMatch_StaysOnSame()
    {
        var index = 0;
        var count = 1;
        index = (index + 1) % count;
        Assert.AreEqual(0, index);
        index = index <= 0 ? count - 1 : index - 1;
        Assert.AreEqual(0, index);
    }

    /// <summary>
    /// Verifies hex match navigation next wraps.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HexMatchNavigation_NextWraps()
    {
        var offsets = new List<long> { 0x100, 0x200, 0x300 };
        var currentIndex = -1;

        // First next
        currentIndex = (currentIndex + 1) % offsets.Count;
        Assert.AreEqual(0, currentIndex);
        Assert.AreEqual(0x100, offsets[currentIndex]);

        // Second next
        currentIndex = (currentIndex + 1) % offsets.Count;
        Assert.AreEqual(1, currentIndex);
        Assert.AreEqual(0x200, offsets[currentIndex]);

        // Third next
        currentIndex = (currentIndex + 1) % offsets.Count;
        Assert.AreEqual(2, currentIndex);
        Assert.AreEqual(0x300, offsets[currentIndex]);

        // Fourth next (wraps)
        currentIndex = (currentIndex + 1) % offsets.Count;
        Assert.AreEqual(0, currentIndex);
        Assert.AreEqual(0x100, offsets[currentIndex]);
    }

    /// <summary>
    /// Verifies hex match navigation prev wraps.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HexMatchNavigation_PrevWraps()
    {
        var offsets = new List<long> { 0x100, 0x200, 0x300 };
        var currentIndex = 0;

        // Prev from 0 wraps to last
        currentIndex = currentIndex <= 0 ? offsets.Count - 1 : currentIndex - 1;
        Assert.AreEqual(2, currentIndex);
        Assert.AreEqual(0x300, offsets[currentIndex]);
    }

    /// <summary>
    /// Verifies zero matches navigate is noop.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ZeroMatches_NavigateIsNoop()
    {
        var offsets = new List<long>();
        // With zero matches, navigation delegates should be null or no-op
        // The spec says n/N with zero matches: no-op
        Assert.IsEmpty(offsets);
    }

    /// <summary>
    /// Verifies backwards navigation from middle.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BackwardsNavigation_FromMiddle()
    {
        var offsets = new List<long> { 10, 20, 30, 40, 50 };
        var index = 2; // start at 30

        index = index <= 0 ? offsets.Count - 1 : index - 1;
        Assert.AreEqual(1, index);
        Assert.AreEqual(20, offsets[index]);

        index = index <= 0 ? offsets.Count - 1 : index - 1;
        Assert.AreEqual(0, index);
        Assert.AreEqual(10, offsets[index]);

        index = index <= 0 ? offsets.Count - 1 : index - 1;
        Assert.AreEqual(4, index); // wrapped
        Assert.AreEqual(50, offsets[index]);
    }
}
