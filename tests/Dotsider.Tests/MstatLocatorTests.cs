using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="MstatLocator"/> — the size-comparison input resolver — against the
/// real published samples, plus a byte-truncated copy for the damaged-input path.
/// </summary>
[Collection("SampleAssemblies")]
public class MstatLocatorTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Verifies a bare .mstat resolves with no binary attribution and picks up the DGML
    /// sitting beside it (the publish target copies both to the same directory).
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Resolve_BareMstat_ReturnsSourceWithDgmlProbe()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var source = MstatLocator.Resolve(samples.NativeAotConsoleMstat!);

        Assert.NotNull(source);
        Assert.Equal(samples.NativeAotConsoleMstat, source.MstatPath);
        Assert.Null(source.BinaryPath);
        Assert.Null(source.BinaryFileSize);
        Assert.NotEmpty(source.Data.Methods);
        if (samples.NativeAotConsoleDgml is not null)
            Assert.NotNull(source.DgmlPath);
    }

    /// <summary>
    /// Verifies a Native AOT binary resolves through its sidecar discovery and carries its
    /// file size for the file-size basis.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Resolve_AotBinary_ResolvesSidecar()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "AOT binary was not produced");
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var source = MstatLocator.Resolve(samples.NativeAotConsoleExe!);

        Assert.NotNull(source);
        Assert.Equal(samples.NativeAotConsoleExe, source.BinaryPath);
        Assert.Equal(new FileInfo(samples.NativeAotConsoleExe!).Length, source.BinaryFileSize);
        Assert.NotEmpty(source.Data.Methods);
    }

    /// <summary>
    /// Verifies a managed assembly is rejected by the bounded probe — it never resolves as an
    /// mstat even though an mstat is itself a valid managed assembly.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Resolve_ManagedDll_ReturnsNull()
    {
        Assert.Null(MstatLocator.Resolve(samples.RichLibraryDll));
        Assert.Null(MstatLocator.Resolve(samples.HelloWorldDll));
    }

    /// <summary>
    /// Verifies a missing path resolves to null rather than throwing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Resolve_MissingFile_ReturnsNull()
    {
        Assert.Null(MstatLocator.Resolve(Path.Combine(Path.GetTempPath(), "does-not-exist.mstat")));
    }

    /// <summary>
    /// Verifies a byte-truncated copy of the real report resolves cleanly: the reader either
    /// recovers a partial prefix or gives up with null, and the locator surfaces whichever
    /// without throwing — the CLI turns null into a precise error.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Resolve_TruncatedMstatCopy_NullOrPartialWithoutThrow()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var truncated = Path.Combine(Path.GetTempPath(), $"dotsider-truncated-{Guid.NewGuid():N}.mstat");
        try
        {
            var bytes = File.ReadAllBytes(samples.NativeAotConsoleMstat!);
            File.WriteAllBytes(truncated, bytes[..(bytes.Length / 3)]);

            var source = MstatLocator.Resolve(truncated);

            if (source is not null)
            {
                Assert.Equal(truncated, source.MstatPath);
                Assert.Null(source.BinaryPath);
            }
        }
        finally
        {
            File.Delete(truncated);
        }
    }
}
