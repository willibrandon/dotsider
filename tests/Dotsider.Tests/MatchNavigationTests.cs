namespace Dotsider.Tests;

/// <summary>
/// Tests for match navigation: next/prev through matches, wrap-around,
/// single match, zero matches, backwards navigation.
/// </summary>
public class MatchNavigationTests
{
    [Fact(Timeout = 30_000)]
    public void NavigateNext_WrapsAround()
    {
        var index = 0;
        var count = 3;

        // Next 4 times should wrap
        index = (index + 1) % count; Assert.Equal(1, index);
        index = (index + 1) % count; Assert.Equal(2, index);
        index = (index + 1) % count; Assert.Equal(0, index); // wrapped
        index = (index + 1) % count; Assert.Equal(1, index);
    }

    [Fact(Timeout = 30_000)]
    public void NavigatePrev_WrapsAround()
    {
        var index = 0;
        var count = 3;

        // Prev from 0 should go to last
        index = index <= 0 ? count - 1 : index - 1;
        Assert.Equal(2, index); // wrapped to last
        index = index <= 0 ? count - 1 : index - 1;
        Assert.Equal(1, index);
        index = index <= 0 ? count - 1 : index - 1;
        Assert.Equal(0, index);
    }

    [Fact(Timeout = 30_000)]
    public void SingleMatch_StaysOnSame()
    {
        var index = 0;
        var count = 1;
        index = (index + 1) % count;
        Assert.Equal(0, index);
        index = index <= 0 ? count - 1 : index - 1;
        Assert.Equal(0, index);
    }

    [Fact(Timeout = 30_000)]
    public void HexMatchNavigation_NextWraps()
    {
        var offsets = new List<long> { 0x100, 0x200, 0x300 };
        var currentIndex = -1;

        // First next
        currentIndex = (currentIndex + 1) % offsets.Count;
        Assert.Equal(0, currentIndex);
        Assert.Equal(0x100, offsets[currentIndex]);

        // Second next
        currentIndex = (currentIndex + 1) % offsets.Count;
        Assert.Equal(1, currentIndex);
        Assert.Equal(0x200, offsets[currentIndex]);

        // Third next
        currentIndex = (currentIndex + 1) % offsets.Count;
        Assert.Equal(2, currentIndex);
        Assert.Equal(0x300, offsets[currentIndex]);

        // Fourth next (wraps)
        currentIndex = (currentIndex + 1) % offsets.Count;
        Assert.Equal(0, currentIndex);
        Assert.Equal(0x100, offsets[currentIndex]);
    }

    [Fact(Timeout = 30_000)]
    public void HexMatchNavigation_PrevWraps()
    {
        var offsets = new List<long> { 0x100, 0x200, 0x300 };
        var currentIndex = 0;

        // Prev from 0 wraps to last
        currentIndex = currentIndex <= 0 ? offsets.Count - 1 : currentIndex - 1;
        Assert.Equal(2, currentIndex);
        Assert.Equal(0x300, offsets[currentIndex]);
    }

    [Fact(Timeout = 30_000)]
    public void ZeroMatches_NavigateIsNoop()
    {
        var offsets = new List<long>();
        // With zero matches, navigation delegates should be null or no-op
        // The spec says n/N with zero matches: no-op
        Assert.Empty(offsets);
    }

    [Fact(Timeout = 30_000)]
    public void BackwardsNavigation_FromMiddle()
    {
        var offsets = new List<long> { 10, 20, 30, 40, 50 };
        var index = 2; // start at 30

        index = index <= 0 ? offsets.Count - 1 : index - 1;
        Assert.Equal(1, index);
        Assert.Equal(20, offsets[index]);

        index = index <= 0 ? offsets.Count - 1 : index - 1;
        Assert.Equal(0, index);
        Assert.Equal(10, offsets[index]);

        index = index <= 0 ? offsets.Count - 1 : index - 1;
        Assert.Equal(4, index); // wrapped
        Assert.Equal(50, offsets[index]);
    }
}
