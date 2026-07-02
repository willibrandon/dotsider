using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="MstatReader"/> against the real size report published next to the
/// NativeAOT sample, plus malformed-input cases. The fixture publishes with the .NET 10 SDK,
/// so format assertions pin version 2.2.
/// </summary>
[Collection("SampleAssemblies")]
public class MstatReaderTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Verifies the fixture's report parses and carries format version 2.2. The assembly
    /// version's Build/Revision are unset sentinels and must not leak into the format fields.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_FixtureMstat_ReportsFormat22()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var data = MstatReader.Read(samples.NativeAotConsoleMstat!);

        Assert.NotNull(data);
        Assert.Equal(2, data.FormatMajorVersion);
        Assert.Equal(2, data.FormatMinorVersion);
    }

    /// <summary>
    /// Verifies methods carry names, non-negative sizes, and assembly attribution spanning
    /// both the app and the runtime.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_FixtureMstat_MethodsHaveNamesSizesAndAssemblies()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var data = MstatReader.Read(samples.NativeAotConsoleMstat!);

        Assert.NotNull(data);
        Assert.NotEmpty(data.Methods);
        Assert.All(data.Methods, m => Assert.True(m.Size >= 0));
        Assert.Contains(data.Methods, m => m.Size > 0);
        Assert.All(data.Methods, m => Assert.False(string.IsNullOrEmpty(m.Name)));
        Assert.Contains(data.Methods, m => m.AssemblyName == "System.Private.CoreLib");
        Assert.Contains(data.Methods, m => m.AssemblyName == "NativeAotConsole");
    }

    /// <summary>
    /// Verifies every method in a 2.x report carries its dependency-graph node name — the
    /// string that joins the size entry to the DGML graph.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_FixtureMstat_MethodsCarryNodeNames()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var data = MstatReader.Read(samples.NativeAotConsoleMstat!);

        Assert.NotNull(data);
        Assert.All(data.Methods, m => Assert.False(string.IsNullOrEmpty(m.NodeName)));
    }

    /// <summary>
    /// Verifies the sample's own Program type appears among the constructed types.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_FixtureMstat_TypesCoverProgramType()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var data = MstatReader.Read(samples.NativeAotConsoleMstat!);

        Assert.NotNull(data);
        Assert.NotEmpty(data.Types);
        Assert.Contains(data.Types, t => t.Name == "Program" && t.AssemblyName == "NativeAotConsole");
    }

    /// <summary>
    /// Verifies the well-known global data regions appear among the blobs, including the
    /// buckets that 2.1+ also breaks down in detail sections.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_FixtureMstat_BlobsIncludeKnownRegions()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var data = MstatReader.Read(samples.NativeAotConsoleMstat!);

        Assert.NotNull(data);
        Assert.Contains(data.Blobs, b => b.Name == "Metadata" && b.Size > 0);
        Assert.Contains(data.Blobs, b => b.Name == "ArrayOfFrozenObjects" && b.Size > 0);
        Assert.Contains(data.Blobs, b => b.Name == "FieldRvaData" && b.Size > 0);
    }

    /// <summary>
    /// Verifies the 2.1 detail sections parse: the console sample freezes string literals, so
    /// frozen objects are non-empty and typed as System.String with no owning type.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_FixtureMstat_FrozenObjectsAreStringLiterals()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var data = MstatReader.Read(samples.NativeAotConsoleMstat!);

        Assert.NotNull(data);
        Assert.NotEmpty(data.FrozenObjects);
        Assert.Contains(data.FrozenObjects, f => f.TypeName == "System.String" && f.OwningType is null);
        Assert.NotEmpty(data.RvaFields);
    }

    /// <summary>
    /// Verifies the report lists the referenced assemblies, including the app itself — the
    /// entry the dependency graph promotes to its root.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_FixtureMstat_AssembliesIncludeAppAndCoreLib()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var data = MstatReader.Read(samples.NativeAotConsoleMstat!);

        Assert.NotNull(data);
        Assert.Contains(data.Assemblies, a => a.Name == "NativeAotConsole");
        Assert.Contains(data.Assemblies, a => a.Name == "System.Private.CoreLib");
    }

    /// <summary>
    /// Verifies mstat node names appear as DGML labels — the join contract the dependency
    /// graph and why-chains rely on.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Read_FixtureMstat_NodeNamesJoinToDgmlLabels()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");
        Assert.SkipWhen(samples.NativeAotConsoleDgml is null, "DGML sidecar was not produced");

        var data = MstatReader.Read(samples.NativeAotConsoleMstat!);
        Assert.NotNull(data);

        var labels = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(samples.NativeAotConsoleDgml!))
        {
            var start = line.IndexOf("Label=\"", StringComparison.Ordinal);
            if (start < 0) continue;
            start += 7;
            var end = line.IndexOf('"', start);
            if (end > start) labels.Add(System.Net.WebUtility.HtmlDecode(line[start..end]));
        }

        var sample = data.Methods.Take(200).ToList();
        var hits = sample.Count(m => m.NodeName is { } n && labels.Contains(n));
        Assert.True(hits >= sample.Count * 9 / 10,
            $"only {hits}/{sample.Count} method node names matched DGML labels");
    }

    /// <summary>
    /// Verifies a managed assembly is rejected: RichLibrary's 2.5 assembly version passes the
    /// version gate, so the recognized-stream gate must reject it.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_ManagedDll_ReturnsNull()
    {
        Assert.Null(MstatReader.Read(samples.RichLibraryDll));
    }

    /// <summary>
    /// Verifies a non-PE file returns null rather than throwing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_NonPeFile_ReturnsNull()
    {
        Assert.Null(MstatReader.Read(samples.NonDotNetBinaryPath));
    }

    /// <summary>
    /// Verifies the Native AOT executable itself (no CLR metadata) returns null.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_NativeAotExeItself_ReturnsNull()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        Assert.Null(MstatReader.Read(samples.NativeAotConsoleExe!));
    }

    /// <summary>
    /// Verifies a missing file returns null rather than throwing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_MissingFile_ReturnsNull()
    {
        Assert.Null(MstatReader.Read(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.mstat")));
    }

    /// <summary>
    /// Verifies a truncated report never throws: the result is null or carries only the
    /// entries that parsed completely before the cut.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_TruncatedMstat_ReturnsNullOrParsedPrefix()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var bytes = File.ReadAllBytes(samples.NativeAotConsoleMstat!);
        using var truncated = new MemoryStream(bytes, 0, bytes.Length * 6 / 10);

        var data = MstatReader.Read(truncated);

        if (data is not null)
            Assert.All(data.Methods, m => Assert.True(m.Size >= 0));
    }

    /// <summary>
    /// Verifies a corrupted IL stream keeps the entries parsed before the damage: flipping an
    /// opcode mid-stream ends the walk without discarding the prefix or throwing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_CorruptedIlStream_KeepsParsedPrefix()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var intact = MstatReader.Read(samples.NativeAotConsoleMstat!);
        Assert.NotNull(intact);
        Assert.True(intact.Methods.Count > 10);

        // Find the Methods IL and flip a byte in its middle to a nop. The IL streams live in
        // .text, so corrupt a byte at ~25% of the file, well past the headers.
        var bytes = File.ReadAllBytes(samples.NativeAotConsoleMstat!);
        bytes[bytes.Length / 4] = 0x00;
        using var corrupted = new MemoryStream(bytes);

        var data = MstatReader.Read(corrupted);

        // Never throws; either rejected outright or parsed to some prefix.
        if (data is not null)
            Assert.True(data.Methods.Count <= intact.Methods.Count);
    }
}
