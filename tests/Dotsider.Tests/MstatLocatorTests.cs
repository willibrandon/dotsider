using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="MstatLocator"/> — the size-comparison input resolver — against the
/// real published samples, plus a byte-truncated copy for the damaged-input path.
/// </summary>
[TestClass]
public class MstatLocatorTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies a bare .mstat resolves with no binary attribution and picks up the DGML
    /// sitting beside it (the publish target copies both to the same directory).
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Resolve_BareMstat_ReturnsSourceWithDgmlProbe()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var source = MstatLocator.Resolve(Samples.NativeAotConsoleMstat!);

        Assert.IsNotNull(source);
        Assert.AreEqual(Samples.NativeAotConsoleMstat, source.MstatPath);
        Assert.IsNull(source.BinaryPath);
        Assert.IsNull(source.BinaryFileSize);
        Assert.IsNotEmpty(source.Data.Methods);
        if (Samples.NativeAotConsoleDgml is not null)
            Assert.IsNotNull(source.DgmlPath);
    }

    /// <summary>
    /// Verifies a Native AOT binary resolves through its sidecar discovery and carries its
    /// file size for the file-size basis.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Resolve_AotBinary_ResolvesSidecar()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "AOT binary was not produced");
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var source = MstatLocator.Resolve(Samples.NativeAotConsoleExe!);

        Assert.IsNotNull(source);
        Assert.AreEqual(Samples.NativeAotConsoleExe, source.BinaryPath);
        Assert.AreEqual(new FileInfo(Samples.NativeAotConsoleExe!).Length, source.BinaryFileSize);
        Assert.IsNotEmpty(source.Data.Methods);
    }

    /// <summary>
    /// Verifies a managed assembly is rejected by the bounded probe — it never resolves as an
    /// mstat even though an mstat is itself a valid managed assembly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Resolve_ManagedDll_ReturnsNull()
    {
        Assert.IsNull(MstatLocator.Resolve(Samples.RichLibraryDll));
        Assert.IsNull(MstatLocator.Resolve(Samples.HelloWorldDll));
    }

    /// <summary>
    /// Verifies a missing path resolves to null rather than throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Resolve_MissingFile_ReturnsNull()
    {
        Assert.IsNull(MstatLocator.Resolve(Path.Combine(Path.GetTempPath(), "does-not-exist.mstat")));
    }

    /// <summary>
    /// Verifies a byte-truncated copy of the real report resolves cleanly: the reader either
    /// recovers a partial prefix or gives up with null, and the locator surfaces whichever
    /// without throwing — the CLI turns null into a precise error.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Resolve_TruncatedMstatCopy_NullOrPartialWithoutThrow()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var truncated = Path.Combine(Path.GetTempPath(), $"dotsider-truncated-{Guid.NewGuid():N}.mstat");
        try
        {
            var bytes = File.ReadAllBytes(Samples.NativeAotConsoleMstat!);
            File.WriteAllBytes(truncated, bytes[..(bytes.Length / 3)]);

            var source = MstatLocator.Resolve(truncated);

            if (source is not null)
            {
                Assert.AreEqual(truncated, source.MstatPath);
                Assert.IsNull(source.BinaryPath);
            }
        }
        finally
        {
            File.Delete(truncated);
        }
    }
}
