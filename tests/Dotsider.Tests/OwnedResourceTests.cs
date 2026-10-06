using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>Checks cleanup when resource acquisition or ownership transfer fails.</summary>
[TestClass]
public sealed class OwnedResourceTests
{
    /// <summary>An exception before transfer closes the acquired resource.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FailedOperation_DisposesAcquiredResource()
    {
        using var stream = new MemoryStream();
        void FailAfterAcquisition()
        {
            using var owner = new OwnedResource<MemoryStream>(stream);
            owner.Value.WriteByte(1);
            throw new IOException("Failed after acquisition");
        }
        Assert.ThrowsExactly<IOException>(FailAfterAcquisition);
        Assert.IsFalse(stream.CanRead);
    }

    /// <summary>A successfully transferred resource remains usable after scope exit.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TransferredResource_RemainsOpenForItsNewOwner()
    {
        using var stream = new MemoryStream();
        using (var owner = new OwnedResource<MemoryStream>(stream))
        {
            Assert.AreSame(stream, owner.Release());
        }
        stream.WriteByte(42);
        Assert.AreEqual(1L, stream.Length);
    }

    /// <summary>A failing disposer still releases resources acquired earlier.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void CleanupFailure_StillDisposesRemainingResources()
    {
        using var stream = new MemoryStream();
        using var resources = new OwnedResources<IDisposable>();
        resources.Add(stream);
        resources.Add(new FailingDisposable());
        Assert.ThrowsExactly<IOException>(resources.Dispose);
        Assert.IsFalse(stream.CanRead);
    }

    private sealed class FailingDisposable : IDisposable
    {
        public void Dispose() => throw new IOException("Cleanup failed");
    }
}
