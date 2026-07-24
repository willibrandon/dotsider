using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Assembly Analyzer.
/// </summary>
[TestClass]
public class AssemblyAnalyzerTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    // --- HelloWorld (Exe, minimal) ---

    /// <summary>
    /// Verifies hello world has correct name.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HelloWorld_HasCorrectName()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        Assert.AreEqual("HelloWorld.dll", a.FileName);
        Assert.AreEqual("HelloWorld", a.AssemblyName);
    }

    /// <summary>
    /// Verifies hello world has metadata.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HelloWorld_HasMetadata()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        Assert.IsTrue(a.HasMetadata);
        Assert.IsNotNull(a.GetMetadataReader());
    }

    /// <summary>
    /// Verifies hello world has target framework.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HelloWorld_HasTargetFramework()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        Assert.IsNotNull(a.TargetFramework);
        Assert.Contains("10.0", a.TargetFramework);
    }

    /// <summary>
    /// Verifies hello world has clr header with entry point.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HelloWorld_HasClrHeaderWithEntryPoint()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        Assert.IsNotNull(a.ClrHeader);
        Assert.IsGreaterThan(0, a.ClrHeader!.EntryPointToken);
    }

    /// <summary>
    /// Verifies hello world has pe headers.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HelloWorld_HasPeHeaders()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        Assert.IsNotNull(a.PeHeaders);
    }

    /// <summary>
    /// Verifies hello world has text section.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HelloWorld_HasTextSection()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        Assert.Contains(s => s.Name == ".text", a.Sections);
    }

    /// <summary>
    /// Verifies hello world raw bytes match file size.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HelloWorld_RawBytesMatchFileSize()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        Assert.AreEqual(a.FileSize, (long)a.RawBytes.Length);
    }

    /// <summary>
    /// Verifies hello world has type defs.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HelloWorld_HasTypeDefs()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        Assert.IsNotEmpty(a.TypeDefs);
    }

    /// <summary>
    /// Verifies hello world has method defs.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HelloWorld_HasMethodDefs()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        Assert.IsNotEmpty(a.MethodDefs);
    }

    // --- RichLibrary (Library, NuGet deps) ---

    /// <summary>
    /// Verifies rich library has correct version.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_HasCorrectVersion()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        Assert.IsNotNull(a.AssemblyVersion);
        Assert.Contains("2.5.1", a.AssemblyVersion);
    }

    /// <summary>
    /// Verifies rich library has newton soft ref.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_HasNewtonSoftRef()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        Assert.Contains(r => r.Name == "Newtonsoft.Json", a.AssemblyRefs);
    }

    /// <summary>
    /// Verifies rich library has system text json ref.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_HasSystemTextJsonRef()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        Assert.Contains(r => r.Name == "System.Text.Json", a.AssemblyRefs);
    }

    /// <summary>
    /// Verifies rich library has service types.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_HasServiceTypes()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        Assert.Contains(t => t.FullName == "RichLibrary.Services.UserService", a.TypeDefs);
        Assert.Contains(t => t.FullName == "RichLibrary.Services.ProductCatalog", a.TypeDefs);
    }

    /// <summary>
    /// Verifies rich library has no entry point.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_HasNoEntryPoint()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        Assert.IsNotNull(a.ClrHeader);
        Assert.AreEqual(0, a.ClrHeader!.EntryPointToken);
    }

    /// <summary>
    /// Verifies rich library has many methods.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_HasManyMethods()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        Assert.IsGreaterThan(10, a.MethodDefs.Count);
    }

    /// <summary>
    /// Verifies rich library opens its matching portable PDB sidecar.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_OpensPortablePdbSidecar()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);

        Assert.IsTrue(a.HasPortablePdb);
        Assert.AreEqual(PdbProvenanceKind.Sidecar, a.PdbProvenance.Kind);
        Assert.IsNotNull(a.PdbProvenance.Path);
        Assert.IsNotNull(a.GetPdbReader());
        Assert.Contains(entry => entry.Type == DebugDirectoryEntryType.CodeView, a.DebugDirectory);
    }

    /// <summary>
    /// Verifies rich library source link mappings resolve method documents.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_SourceLink_ResolvesMethodDocument()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var method = FindMethod(a, "RichLibrary.Services.UserService", "Add");
        var debugInfo = a.GetMethodDebugInfo(method);
        var document = debugInfo.SequencePoints
            .First(point => point.Document?.EndsWith("UserService.cs", StringComparison.OrdinalIgnoreCase) == true)
            .Document!;

        var url = a.ResolveSourceLinkUrl(document);

        Assert.IsTrue(a.SourceLink.IsPresent);
        Assert.IsNotEmpty(a.SourceLink.Mappings);
        Assert.IsNotNull(url);
        Assert.Contains("raw.githubusercontent.com", url);
        Assert.EndsWith("UserService.cs", url, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies method debug info exposes sequence points and PDB local names.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_MethodDebugInfo_IncludesSequencePointsAndLocals()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var method = FindMethod(a, "RichLibrary.Services.UserService", "Add");

        var debugInfo = a.GetMethodDebugInfo(method);

        Assert.AreEqual(PdbProvenanceKind.Sidecar, debugInfo.Pdb.Kind);
        Assert.Contains(point => point.Document?.EndsWith("UserService.cs", StringComparison.OrdinalIgnoreCase) == true
                && point.SourceLinkUrl is not null, debugInfo.SequencePoints);
        Assert.Contains(local => local.Name == "id", debugInfo.Locals);
        Assert.Contains(local => local.Name == "user", debugInfo.Locals);
    }

    // --- ComplexApp (Exe, embedded resources) ---

    /// <summary>
    /// Verifies complex app has embedded resources.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ComplexApp_HasEmbeddedResources()
    {
        using var a = new AssemblyAnalyzer(Samples.ComplexAppDll);
        Assert.Contains(r => r.Name.Contains("config.json"), a.Resources);
        Assert.Contains(r => r.Name.Contains("banner.txt"), a.Resources);
    }

    /// <summary>
    /// Verifies complex app has version.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ComplexApp_HasVersion()
    {
        using var a = new AssemblyAnalyzer(Samples.ComplexAppDll);
        Assert.IsNotNull(a.AssemblyVersion);
        Assert.Contains("1.0.0", a.AssemblyVersion);
    }

    // --- MinimalApi (Web SDK) ---

    /// <summary>
    /// Verifies minimal api has asp net refs.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MinimalApi_HasAspNetRefs()
    {
        using var a = new AssemblyAnalyzer(Samples.MinimalApiDll);
        // Web SDK assemblies reference ASP.NET Core packages
        Assert.IsGreaterThan(0, a.AssemblyRefs.Count);
    }

    /// <summary>
    /// Verifies minimal api has record types.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MinimalApi_HasRecordTypes()
    {
        using var a = new AssemblyAnalyzer(Samples.MinimalApiDll);
        Assert.Contains(t => t.Name == "GreetingResponse", a.TypeDefs);
        Assert.Contains(t => t.Name == "EchoRequest", a.TypeDefs);
    }

    // --- NativeLib (unsafe, P/Invoke) ---

    /// <summary>
    /// Verifies native lib has p invoke methods.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeLib_HasPInvokeMethods()
    {
        using var a = new AssemblyAnalyzer(Samples.NativeLibDll);
        Assert.Contains(t => t.FullName == "NativeLib.NativeInterop", a.TypeDefs);
        Assert.Contains(t => t.FullName == "NativeLib.UnsafeOperations", a.TypeDefs);
    }

    /// <summary>
    /// Verifies native lib has fixed buffer struct.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeLib_HasFixedBufferStruct()
    {
        using var a = new AssemblyAnalyzer(Samples.NativeLibDll);
        Assert.Contains(t => t.Name == "FixedBuffer", a.TypeDefs);
    }

    // --- EmptyLib (minimal) ---

    /// <summary>
    /// Verifies empty lib has minimal type defs.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void EmptyLib_HasMinimalTypeDefs()
    {
        using var a = new AssemblyAnalyzer(Samples.EmptyLibDll);
        // <Module> + internal Module class
        Assert.IsLessThanOrEqualTo(3, a.TypeDefs.Count);
        Assert.IsGreaterThanOrEqualTo(1, a.TypeDefs.Count);
    }

    /// <summary>
    /// Verifies empty lib has metadata.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void EmptyLib_HasMetadata()
    {
        using var a = new AssemblyAnalyzer(Samples.EmptyLibDll);
        Assert.IsTrue(a.HasMetadata);
        Assert.IsNotNull(a.ClrHeader);
    }

    /// <summary>
    /// Verifies embedded source can be decoded from an embedded portable PDB.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void EmbeddedSourceLib_DecodesEmbeddedSource()
    {
        using var a = new AssemblyAnalyzer(Samples.EmbeddedSourceLibDll);
        var method = FindMethod(a, "EmbeddedSourceLib.EmbeddedSourceFixture", "Compute");
        int embeddedPdbSize = EmbeddedPortablePdbTestImage.ReadDeclaredSize(
            Samples.EmbeddedSourceLibDll);

        var debugInfo = a.GetMethodDebugInfo(method);
        var source = a.GetEmbeddedSource(method);

        Assert.IsTrue(a.HasPortablePdb);
        Assert.AreEqual(PdbProvenanceKind.Embedded, a.PdbProvenance.Kind);
        DebugDirectoryInfo embeddedEntry = Assert.ContainsSingle(
            static entry => entry.Type == DebugDirectoryEntryType.EmbeddedPortablePdb,
            a.DebugDirectory);
        Assert.AreEqual(
            $"present; uncompressed size: {embeddedPdbSize} bytes",
            embeddedEntry.Payload);
        Assert.Contains(point => point.HasEmbeddedSource, debugInfo.SequencePoints);
        Assert.IsNotNull(source);
        Assert.Contains("return doubled + 1;", source.Text);
        Assert.IsNotEmpty(source.Bytes);
    }

    /// <summary>
    /// An oversized embedded portable PDB is ignored without losing assembly metadata.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void EmbeddedSourceLib_OversizedEmbeddedPdb_ReportsInvalidProvenance()
    {
        byte[] image = EmbeddedPortablePdbTestImage.WithDeclaredSize(
            Samples.EmbeddedSourceLibDll,
            (256 * 1024 * 1024) + 1);

        using AssemblyAnalyzer analyzer = new(image, "EmbeddedSourceLib.dll");

        Assert.IsTrue(analyzer.HasMetadata);
        Assert.IsNotEmpty(analyzer.MethodDefs);
        Assert.IsFalse(analyzer.HasPortablePdb);
        Assert.AreEqual(PdbProvenanceKind.InvalidEmbeddedPdb, analyzer.PdbProvenance.Kind);
        Assert.Contains("256 MiB", analyzer.PdbProvenance.Details!);
        DebugDirectoryInfo embeddedEntry = Assert.ContainsSingle(
            static entry => entry.Type == DebugDirectoryEntryType.EmbeddedPortablePdb,
            analyzer.DebugDirectory);
        Assert.Contains("unreadable:", embeddedEntry.Payload);
        Assert.Contains("256 MiB", embeddedEntry.Payload);
    }

    /// <summary>
    /// Oversized embedded-source declarations fail closed without invalidating the enclosing PDB.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow((16 * 1024 * 1024) + 1)]
    [DataRow(int.MaxValue)]
    public void EmbeddedSourceLib_OversizedEmbeddedSource_ReturnsNull(int declaredSize)
    {
        byte[] image = EmbeddedPortablePdbTestImage.WithEmbeddedSourceDeclaredSize(
            Samples.EmbeddedSourceLibDll,
            "EmbeddedSourceFixture.cs",
            declaredSize);
        using AssemblyAnalyzer analyzer = new(image, "EmbeddedSourceLib.dll");
        MethodDefInfo method = FindMethod(
            analyzer,
            "EmbeddedSourceLib.EmbeddedSourceFixture",
            "Compute");

        EmbeddedSourceInfo? source = analyzer.GetEmbeddedSource(method);

        Assert.IsTrue(analyzer.HasMetadata);
        Assert.IsTrue(analyzer.HasPortablePdb);
        Assert.AreEqual(PdbProvenanceKind.Embedded, analyzer.PdbProvenance.Kind);
        Assert.IsNull(source);
    }

    /// <summary>
    /// Embedded-source deflate output must match the compiler-recorded length exactly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(-1)]
    [DataRow(1)]
    public void EmbeddedSourceLib_EmbeddedSourceLengthMismatch_ReturnsNull(int adjustment)
    {
        const string documentFileName = "EmbeddedSourceFixture.cs";
        int declaredSize = EmbeddedPortablePdbTestImage.ReadEmbeddedSourceDeclaredSize(
            Samples.EmbeddedSourceLibDll,
            documentFileName);
        byte[] image = EmbeddedPortablePdbTestImage.WithEmbeddedSourceDeclaredSize(
            Samples.EmbeddedSourceLibDll,
            documentFileName,
            checked(declaredSize + adjustment));
        using AssemblyAnalyzer analyzer = new(image, "EmbeddedSourceLib.dll");
        MethodDefInfo method = FindMethod(
            analyzer,
            "EmbeddedSourceLib.EmbeddedSourceFixture",
            "Compute");

        EmbeddedSourceInfo? source = analyzer.GetEmbeddedSource(method);

        Assert.IsTrue(analyzer.HasPortablePdb);
        Assert.AreEqual(PdbProvenanceKind.Embedded, analyzer.PdbProvenance.Kind);
        Assert.IsNull(source);
    }

    /// <summary>
    /// A valid matching sidecar remains usable when the embedded copy is malformed.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void EmbeddedSourceLib_InvalidEmbeddedPdb_UsesMatchingSidecar()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"dotsider-embedded-pdb-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string assemblyPath = Path.Combine(directory, "EmbeddedSourceLib.dll");
        string pdbPath = Path.Combine(directory, "EmbeddedSourceLib.pdb");

        try
        {
            File.WriteAllBytes(
                assemblyPath,
                EmbeddedPortablePdbTestImage.WithDeclaredSize(
                    Samples.EmbeddedSourceLibDll,
                    int.MaxValue));
            File.WriteAllBytes(
                pdbPath,
                EmbeddedPortablePdbTestImage.ExtractPortablePdb(Samples.EmbeddedSourceLibDll));
            using AssemblyAnalyzer analyzer = new(assemblyPath);
            MethodDefInfo method = FindMethod(
                analyzer,
                "EmbeddedSourceLib.EmbeddedSourceFixture",
                "Compute");

            EmbeddedSourceInfo? source = analyzer.GetEmbeddedSource(method);

            Assert.IsTrue(analyzer.HasPortablePdb);
            Assert.AreEqual(PdbProvenanceKind.Sidecar, analyzer.PdbProvenance.Kind);
            Assert.AreEqual(pdbPath, analyzer.PdbProvenance.Path);
            Assert.IsNotNull(source);
            Assert.Contains("return doubled + 1;", source.Text);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // --- RichLibraryV2 (same AssemblyName as V1) ---

    /// <summary>
    /// Verifies rich library v2 has same assembly name.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibraryV2_HasSameAssemblyName()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        Assert.AreEqual("RichLibrary", a.AssemblyName);
    }

    /// <summary>
    /// Verifies rich library v2 has version3.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibraryV2_HasVersion3()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        Assert.IsNotNull(a.AssemblyVersion);
        Assert.Contains("3.0.0", a.AssemblyVersion);
    }

    /// <summary>
    /// Verifies rich library v2 has new types.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibraryV2_HasNewTypes()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        Assert.Contains(t => t.Name == "Order", a.TypeDefs);
        Assert.Contains(t => t.Name == "OrderService", a.TypeDefs);
    }

    // --- Cross-assembly metadata checks ---

    /// <summary>
    /// Verifies all samples have custom attributes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AllSamples_HaveCustomAttributes()
    {
        string[] paths = [Samples.HelloWorldDll, Samples.RichLibraryDll, Samples.ComplexAppDll,
            Samples.MinimalApiDll, Samples.NativeLibDll];
        foreach (var path in paths)
        {
            using var a = new AssemblyAnalyzer(path);
            Assert.IsNotEmpty(a.CustomAttributes);
        }
    }

    /// <summary>
    /// Verifies all samples have positive file size.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AllSamples_HavePositiveFileSize()
    {
        string[] paths = [Samples.HelloWorldDll, Samples.RichLibraryDll, Samples.ComplexAppDll,
            Samples.MinimalApiDll, Samples.NativeLibDll, Samples.EmptyLibDll];
        foreach (var path in paths)
        {
            using var a = new AssemblyAnalyzer(path);
            Assert.IsGreaterThan(0, a.FileSize);
        }
    }

    /// <summary>
    /// Verifies all samples have clr header.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AllSamples_HaveClrHeader()
    {
        string[] paths = [Samples.HelloWorldDll, Samples.RichLibraryDll, Samples.ComplexAppDll,
            Samples.MinimalApiDll, Samples.NativeLibDll, Samples.EmptyLibDll];
        foreach (var path in paths)
        {
            using var a = new AssemblyAnalyzer(path);
            Assert.IsNotNull(a.ClrHeader);
            Assert.IsGreaterThan(0, a.ClrHeader!.MetadataSize);
        }
    }

    // --- Edge cases ---

    /// <summary>
    /// Verifies get method body returns non null for method with il.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetMethodBody_ReturnsNonNull_ForMethodWithIl()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        var method = a.MethodDefs.First(m => m.Rva != 0);
        var body = a.GetMethodBody(method);
        Assert.IsNotNull(body);
    }

    /// <summary>
    /// Verifies Dispose can be called multiple times without side effects.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Dispose_IsIdempotent()
    {
        var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        a.Dispose();
        a.Dispose(); // should not throw
    }

    /// <summary>
    /// Verifies invalid file path throws file not found.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void InvalidFilePath_ThrowsFileNotFound()
    {
        var path = Path.Combine(Path.GetTempPath(), "nonexistent-dotsider-test-" + Guid.NewGuid() + ".dll");
        Assert.ThrowsExactly<FileNotFoundException>(() => new AssemblyAnalyzer(path));
    }

    /// <summary>
    /// Verifies non dot net binary throws bad image format.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NonDotNetBinary_ThrowsBadImageFormat()
    {
        Assert.Throws<BadImageFormatException>(() => new AssemblyAnalyzer(Samples.NonDotNetBinaryPath));
    }

    /// <summary>
    /// Verifies native apphost file constructor tolerates non pe binary.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeApphost_FileConstructor_ToleratesNonPeBinary()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldExe);

        Assert.IsFalse(a.HasMetadata);
        Assert.IsGreaterThan(0, a.FileSize);
        Assert.IsFalse(a.RawBytes.IsEmpty);
    }

    /// <summary>
    /// Verifies the original four-parameter byte-array constructor remains binary compatible.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ByteArrayConstructor_PreservesLegacyMetadataSignature()
    {
        var constructor = typeof(AssemblyAnalyzer).GetConstructor(
            [typeof(byte[]), typeof(string), typeof(string), typeof(string)]);

        Assert.IsNotNull(constructor);
        var parameters = constructor.GetParameters();
        Assert.IsTrue(parameters[2].IsOptional);
        Assert.IsTrue(parameters[3].IsOptional);
    }

    /// <summary>
    /// Verifies native apphost byte array constructor tolerates non pe binary.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeApphost_ByteArrayConstructor_ToleratesNonPeBinary()
    {
        var bytes = File.ReadAllBytes(Samples.HelloWorldExe);
        using var a = new AssemblyAnalyzer(bytes, Samples.HelloWorldExe);

        Assert.IsFalse(a.HasMetadata);
        Assert.AreEqual(bytes.Length, a.FileSize);
        Assert.IsFalse(a.RawBytes.IsEmpty);
    }

    /// <summary>
    /// Verifies native aot byte array constructor tolerates non pe binary.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAot_ByteArrayConstructor_ToleratesNonPeBinary()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        var bytes = File.ReadAllBytes(Samples.NativeAotConsoleExe!);
        using var a = new AssemblyAnalyzer(bytes, Samples.NativeAotConsoleExe!);

        Assert.IsFalse(a.HasMetadata);
        Assert.AreEqual(bytes.Length, a.FileSize);
    }

    /// <summary>
    /// Verifies resolve token returns string.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ResolveToken_ReturnsString()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var method = a.MethodDefs[0];
        var result = a.ResolveToken(method.Token);
        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result);
    }

    /// <summary>
    /// Verifies architecture is not unknown.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Architecture_IsNotUnknown()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        Assert.AreNotEqual("Unknown", a.Architecture);
    }

    /// <summary>
    /// Verifies file properties are populated.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FileProperties_ArePopulated()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        Assert.AreNotEqual(default, a.LastModified);
        Assert.AreNotEqual(default, a.CreatedTime);
    }

    // --- Additional coverage tests ---

    /// <summary>
    /// Verifies file path is absolute.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FilePath_IsAbsolute()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        Assert.AreEqual(Samples.HelloWorldDll, a.FilePath);
        Assert.IsTrue(Path.IsPathRooted(a.FilePath));
    }

    /// <summary>
    /// Verifies rich library type refs non empty.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_TypeRefs_NonEmpty()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        Assert.IsNotEmpty(a.TypeRefs);
    }

    /// <summary>
    /// Verifies rich library member refs non empty.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_MemberRefs_NonEmpty()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        Assert.IsNotEmpty(a.MemberRefs);
        var first = a.MemberRefs[0];
        Assert.IsNotNull(first.Name);
        Assert.IsNotNull(first.DeclaringType);
    }

    /// <summary>
    /// Verifies complex app resources have offsets.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ComplexApp_Resources_HaveOffsets()
    {
        using var a = new AssemblyAnalyzer(Samples.ComplexAppDll);
        foreach (var r in a.Resources)
        {
            Assert.IsNotNull(r.Name);
            Assert.IsGreaterThanOrEqualTo(0, r.Size);
        }
    }

    /// <summary>
    /// Verifies rich library culture is neutral.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_Culture_IsNeutral()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        Assert.AreEqual("neutral", a.Culture);
    }

    /// <summary>
    /// Verifies hello world is read only is false.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HelloWorld_IsReadOnly_IsFalse()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        Assert.IsFalse(a.IsReadOnly);
    }

    /// <summary>
    /// Verifies rich library type defs have properties.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_TypeDefs_HaveProperties()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var userService = a.TypeDefs.First(t => t.Name == "UserService");
        Assert.IsNotNull(userService.FullName);
        Assert.IsNotNull(userService.Namespace);
        Assert.IsGreaterThan(0, userService.Token);
    }

    /// <summary>
    /// Verifies rich library method defs have signatures.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_MethodDefs_HaveSignatures()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var methods = a.MethodDefs.Where(m => m.Rva != 0).Take(5);
        foreach (var m in methods)
        {
            Assert.IsNotNull(m.Name);
            Assert.IsNotNull(m.Signature);
            Assert.IsGreaterThan(0, m.Token);
        }
    }

    /// <summary>
    /// Verifies rich library type refs have properties.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_TypeRefs_HaveProperties()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        foreach (var tr in a.TypeRefs.Take(5))
        {
            Assert.IsNotNull(tr.Name);
            Assert.IsNotNull(tr.Namespace);
        }
    }

    /// <summary>
    /// Verifies resolve token type def.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ResolveToken_TypeDef()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var typeDef = a.TypeDefs.First(t => t.Name != "<Module>");
        var resolved = a.ResolveToken(typeDef.Token);
        Assert.Contains(typeDef.Name, resolved);
    }

    /// <summary>
    /// Verifies resolve token type ref.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ResolveToken_TypeRef()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var typeRef = a.TypeRefs[0];
        var resolved = a.ResolveToken(typeRef.Token);
        Assert.IsNotEmpty(resolved);
    }

    /// <summary>
    /// Verifies resolve token member ref.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ResolveToken_MemberRef()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var memberRef = a.MemberRefs[0];
        var resolved = a.ResolveToken(memberRef.Token);
        Assert.IsNotEmpty(resolved);
    }

    /// <summary>
    /// Verifies MethodSpecs backed by ordinary MemberRefs expose their constructed LINQ names.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ResolveToken_MethodSpecMemberRef_FormatsConstructedMethod()
    {
        using var analyzer = new AssemblyAnalyzer(typeof(MethodSpecReproFixture).Assembly.Location);
        var tokens = GetMethodSpecificationTokens(
            analyzer,
            MethodSpecReproFixture.MethodName,
            expectedCount: MethodSpecReproFixture.ExpectedDisplays.Count);
        var reader = analyzer.GetMetadataReader()!;

        TestAssert.All(tokens, token => Assert.AreEqual(
            HandleKind.MemberReference,
            reader.GetMethodSpecification((MethodSpecificationHandle)MetadataTokens.EntityHandle(token))
                .Method.Kind));
        Assert.AreSequenceEqual(
            MethodSpecReproFixture.ExpectedDisplays,
            tokens.Select(analyzer.ResolveToken));
    }

    /// <summary>
    /// Verifies a MethodSpec may target a MethodDef and may itself contain a constructed generic
    /// type argument.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ResolveToken_MethodSpecMethodDef_FormatsConstructedTypeArgument()
    {
        using var analyzer = new AssemblyAnalyzer(typeof(MethodSpecReproFixture).Assembly.Location);
        var token = Assert.ContainsSingle(GetMethodSpecificationTokens(
            analyzer,
            MethodSpecReproFixture.MethodDefCallerName,
            expectedCount: 1));
        var specification = analyzer.GetMetadataReader()!.GetMethodSpecification(
            (MethodSpecificationHandle)MetadataTokens.EntityHandle(token));

        Assert.AreEqual(HandleKind.MethodDefinition, specification.Method.Kind);
        Assert.AreEqual(MethodSpecReproFixture.MethodDefExpectedDisplay, analyzer.ResolveToken(token));
    }

    /// <summary>
    /// Verifies a MethodSpec MemberRef can be owned by a TypeSpec and retains the constructed
    /// declaring type in its display.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ResolveToken_MethodSpecMemberRefWithTypeSpecParent_FormatsConstructedOwner()
    {
        using var analyzer = new AssemblyAnalyzer(typeof(MethodSpecReproFixture).Assembly.Location);
        var token = Assert.ContainsSingle(GetMethodSpecificationTokens(
            analyzer,
            MethodSpecReproFixture.TypeSpecParentCallerName,
            expectedCount: 1));
        var reader = analyzer.GetMetadataReader()!;
        var specification = reader.GetMethodSpecification(
            (MethodSpecificationHandle)MetadataTokens.EntityHandle(token));
        var memberReference = reader.GetMemberReference((MemberReferenceHandle)specification.Method);

        Assert.AreEqual(HandleKind.MemberReference, specification.Method.Kind);
        Assert.AreEqual(HandleKind.TypeSpecification, memberReference.Parent.Kind);
        Assert.AreEqual(MethodSpecReproFixture.TypeSpecParentExpectedDisplay, analyzer.ResolveToken(token));
    }

    /// <summary>
    /// Verifies invalid MethodSpec metadata fails closed to the complete original token.
    /// </summary>
    /// <param name="name">The malformed metadata shape.</param>
    /// <param name="memberReferenceMethod">The underlying MemberRef signature.</param>
    /// <param name="methodSpecification">The MethodSpec signature.</param>
    /// <param name="targetsField">Whether the MethodSpec targets the field MemberRef.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DynamicData(nameof(InvalidMethodSpecificationCases))]
    public void ResolveToken_InvalidMethodSpec_ReturnsExactRawToken(
        string name,
        byte[] memberReferenceMethod,
        byte[] methodSpecification,
        bool targetsField)
    {
        _ = name;
        using var scope = FacadeSignatureMetadataScope.Create(
            memberReferenceMethod: memberReferenceMethod,
            methodSpecification: methodSpecification,
            methodSpecificationTargetsField: targetsField);
        using var analyzer = new AssemblyAnalyzer(scope.Image, "InvalidMethodSpec.dll");
        var token = MetadataTokens.GetToken(scope.MethodSpecification);

        Assert.AreEqual($"0x{token:X8}", analyzer.ResolveToken(token));
    }

    /// <summary>Verifies an out-of-range MethodSpec row retains the exact original token.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ResolveToken_OutOfRangeMethodSpec_ReturnsExactRawToken()
    {
        using var analyzer = new AssemblyAnalyzer(typeof(MethodSpecReproFixture).Assembly.Location);
        const int token = 0x2BFFFFFF;

        Assert.AreEqual("0x2BFFFFFF", analyzer.ResolveToken(token));
    }

    /// <summary>
    /// Verifies resolve token invalid token returns hex string.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ResolveToken_InvalidToken_ReturnsHexString()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var result = a.ResolveToken(0x7F000001);
        Assert.Contains("0x", result);
    }

    /// <summary>Supplies malformed or inconsistent MethodSpec metadata.</summary>
    /// <returns>The case name, underlying method signature, MethodSpec signature, and field flag.</returns>
    public static IEnumerable<object[]> InvalidMethodSpecificationCases()
    {
        // Generic method with one type parameter, no parameters, and an int return type.
        byte[] genericArityOne = [0x10, 0x01, 0x00, 0x08];
        byte[] validIntInstantiation = [0x0A, 0x01, 0x08];

        yield return [
            "truncated instantiation signature",
            genericArityOne,
            new byte[] { 0x0A, 0x01 },
            false
        ];
        yield return [
            "non-generic underlying method",
            new byte[] { 0x00, 0x00, 0x08 },
            validIntInstantiation,
            false
        ];
        yield return [
            "generic arity mismatch",
            new byte[] { 0x10, 0x02, 0x00, 0x08 },
            validIntInstantiation,
            false
        ];
        yield return [
            "field MemberRef",
            genericArityOne,
            validIntInstantiation,
            true
        ];
    }

    private static int[] GetMethodSpecificationTokens(
        AssemblyAnalyzer analyzer,
        string methodName,
        int expectedCount)
    {
        var method = Assert.ContainsSingle(analyzer.MethodDefs.Where(candidate =>
            candidate.DeclaringType == MethodSpecReproFixture.TypeName
            && candidate.Name == methodName));
        var tokens = new IlDisassembler(analyzer)
            .Disassemble(method)
            .Where(candidate => candidate.MetadataToken is { } token
                && MetadataTokens.EntityHandle(token).Kind == HandleKind.MethodSpecification)
            .Select(candidate => candidate.MetadataToken!.Value)
            .ToArray();

        Assert.HasCount(expectedCount, tokens);
        return tokens;
    }

    /// <summary>
    /// Verifies rich library assembly refs have version and token.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_AssemblyRefs_HaveVersionAndToken()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var newtonsoftRef = a.AssemblyRefs.First(r => r.Name == "Newtonsoft.Json");
        Assert.IsNotNull(newtonsoftRef.Version);
        Assert.IsNotNull(newtonsoftRef.PublicKeyToken);
        Assert.IsNotNull(newtonsoftRef.Culture);
    }

    /// <summary>
    /// Verifies rich library custom attributes have properties.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_CustomAttributes_HaveProperties()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        foreach (var attr in a.CustomAttributes.Take(3))
        {
            Assert.IsNotNull(attr.Constructor);
            Assert.IsNotNull(attr.Parent);
        }
    }

    /// <summary>
    /// Verifies hello world get method body returns non null body.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HelloWorld_GetMethodBody_ReturnsNonNullBody()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        var method = a.MethodDefs.First(m => m.Rva != 0);
        var body = a.GetMethodBody(method);
        Assert.IsNotNull(body);
        Assert.IsGreaterThan(0, body!.GetILBytes()!.Length);
    }

    /// <summary>
    /// Verifies rich library sections have properties.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_Sections_HaveProperties()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var textSection = a.Sections.First(s => s.Name == ".text");
        Assert.IsGreaterThan(0, textSection.RawDataSize);
        Assert.IsGreaterThan(0, textSection.VirtualAddress);
    }

    /// <summary>
    /// Verifies rich library pe headers have valid fields.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_PeHeaders_HaveValidFields()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        Assert.IsNotNull(a.PeHeaders);
        Assert.AreNotEqual((Machine)0, a.PeHeaders!.Machine);
        Assert.AreNotEqual((Characteristics)0, a.PeHeaders.Characteristics);
    }

    /// <summary>
    /// Verifies access after dispose assembly refs throws instead of crashing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AccessAfterDispose_AssemblyRefs_ThrowsInsteadOfCrashing()
    {
        var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        Assert.IsTrue(a.HasMetadata);
        a.Dispose();

        // After Dispose, accessing AssemblyRefs must throw ObjectDisposedException
        // rather than crashing the process with AccessViolationException from
        // reading freed metadata memory.
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = a.AssemblyRefs);
    }

    /// <summary>
    /// Verifies access after dispose type defs throws instead of crashing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AccessAfterDispose_TypeDefs_ThrowsInsteadOfCrashing()
    {
        var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        a.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = a.TypeDefs);
    }

    /// <summary>
    /// Verifies access after dispose method defs throws instead of crashing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AccessAfterDispose_MethodDefs_ThrowsInsteadOfCrashing()
    {
        var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        a.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = a.MethodDefs);
    }

    /// <summary>
    /// Verifies access after dispose field defs throws instead of crashing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AccessAfterDispose_FieldDefs_ThrowsInsteadOfCrashing()
    {
        var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        a.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = a.FieldDefs);
    }

    /// <summary>
    /// Verifies access after dispose get method body throws instead of crashing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AccessAfterDispose_GetMethodBody_ThrowsInsteadOfCrashing()
    {
        var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var method = a.MethodDefs.First(m => m.Rva > 0);
        a.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => a.GetMethodBody(method));
    }

    /// <summary>
    /// Verifies access after dispose resolve token throws instead of crashing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AccessAfterDispose_ResolveToken_ThrowsInsteadOfCrashing()
    {
        var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var token = a.MethodDefs.First(m => m.Rva > 0).Token;
        a.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => a.ResolveToken(token));
    }

    /// <summary>
    /// Verifies the analyzer finds and parses the mstat and DGML sidecars published next to
    /// the Native AOT sample, preferring the codegen graph.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Sidecars_NativeAotExe_ProbeAndParse()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        using var a = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);

        Assert.AreEqual(Samples.NativeAotConsoleMstat, a.MstatPath);
        Assert.IsNotNull(a.Mstat);
        Assert.IsNotEmpty(a.Mstat.Methods);

        if (Samples.NativeAotConsoleDgml is not null)
        {
            Assert.AreEqual(Samples.NativeAotConsoleDgml, a.DgmlPath);
            Assert.IsNotNull(a.Dgml);
            Assert.IsNotEmpty(a.Dgml.Nodes);
        }
    }

    /// <summary>
    /// Verifies an AOT binary with no sidecars beside it probes to null without throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Sidecars_NativeAotExeWithoutSidecars_ReturnNull()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var dir = Directory.CreateTempSubdirectory("dotsider-nosidecar-");
        try
        {
            var exeCopy = Path.Combine(dir.FullName, Path.GetFileName(Samples.NativeAotConsoleExe!));
            File.Copy(Samples.NativeAotConsoleExe!, exeCopy);
            using var a = new AssemblyAnalyzer(exeCopy);

            Assert.IsNull(a.MstatPath);
            Assert.IsNull(a.Mstat);
            Assert.IsNull(a.DgmlPath);
            Assert.IsNull(a.Dgml);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a managed assembly never probes for sidecars, even when a stray mstat sits
    /// next to it — the binary-kind gate short-circuits first.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Sidecars_ManagedAssemblyWithStrayMstat_ReturnsNull()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var dir = Directory.CreateTempSubdirectory("dotsider-stray-");
        try
        {
            var dllCopy = Path.Combine(dir.FullName, "RichLibrary.dll");
            File.Copy(Samples.RichLibraryDll, dllCopy);
            File.Copy(Samples.NativeAotConsoleMstat!, Path.Combine(dir.FullName, "RichLibrary.mstat"));
            using var a = new AssemblyAnalyzer(dllCopy);

            Assert.IsNull(a.MstatPath);
            Assert.IsNull(a.Mstat);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a corrupt sidecar reads as null rather than throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Sidecars_CorruptMstat_ReturnsNull()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var dir = Directory.CreateTempSubdirectory("dotsider-corrupt-");
        try
        {
            var name = Path.GetFileName(Samples.NativeAotConsoleExe!);
            var exeCopy = Path.Combine(dir.FullName, name);
            File.Copy(Samples.NativeAotConsoleExe!, exeCopy);
            var stem = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
            File.WriteAllBytes(Path.Combine(dir.FullName, stem + ".mstat"), [0xDE, 0xAD]);
            using var a = new AssemblyAnalyzer(exeCopy);

            Assert.IsNotNull(a.MstatPath);
            Assert.IsNull(a.Mstat);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a Native AOT binary with its matching PDB beside it is marked NativePdb, carrying
    /// the PDB path, rather than the UnsupportedWindowsPdb it used to report.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Provenance_NativeAotExeWithMatchingPdb_IsNativePdb()
    {
        TestSkip.When(
            Samples.NativeAotConsoleSymbols is null
            || !Samples.NativeAotConsoleSymbols.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase),
            "native PDB not present on this platform");

        using var a = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);

        Assert.AreEqual(PdbProvenanceKind.NativePdb, a.PdbProvenance.Kind);
        Assert.AreEqual(Samples.NativeAotConsoleSymbols, a.PdbProvenance.Path);
    }

    /// <summary>
    /// Verifies a Native AOT binary copied away from its PDB falls back to UnsupportedWindowsPdb.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Provenance_NativeAotExeWithoutPdb_IsUnsupportedWindowsPdb()
    {
        TestSkip.When(
            Samples.NativeAotConsoleSymbols is null
            || !Samples.NativeAotConsoleSymbols.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase),
            "native PDB not present on this platform");

        var dir = Directory.CreateTempSubdirectory("dotsider-nopdb-");
        try
        {
            var exeCopy = Path.Combine(dir.FullName, Path.GetFileName(Samples.NativeAotConsoleExe!));
            File.Copy(Samples.NativeAotConsoleExe!, exeCopy);
            using var a = new AssemblyAnalyzer(exeCopy);

            Assert.AreEqual(PdbProvenanceKind.UnsupportedWindowsPdb, a.PdbProvenance.Kind);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a PDB whose GUID does not match the binary is rejected as UnsupportedWindowsPdb,
    /// not accepted as a native match.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Provenance_NativeAotExeWithWrongPdb_IsUnsupportedWindowsPdb()
    {
        TestSkip.When(
            Samples.NativeAotConsoleSymbols is null
            || !Samples.NativeAotConsoleSymbols.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase),
            "native PDB not present on this platform");

        var dir = Directory.CreateTempSubdirectory("dotsider-wrongpdb-");
        try
        {
            var name = Path.GetFileName(Samples.NativeAotConsoleExe!);
            var exeCopy = Path.Combine(dir.FullName, name);
            File.Copy(Samples.NativeAotConsoleExe!, exeCopy);
            var stem = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

            // A valid PDB whose GUID no longer matches the binary: flip the info-stream GUID bytes
            // in a copy of the real PDB, leaving the container otherwise intact.
            var pdb = File.ReadAllBytes(Samples.NativeAotConsoleSymbols!);
            MutatePdbGuid(pdb);
            File.WriteAllBytes(Path.Combine(dir.FullName, stem + ".pdb"), pdb);

            using var a = new AssemblyAnalyzer(exeCopy);

            Assert.AreEqual(PdbProvenanceKind.UnsupportedWindowsPdb, a.PdbProvenance.Kind);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    // Zeroes the PDB info-stream GUID so its id no longer matches the exe, while leaving the MSF
    // container valid. Walks the superblock → block map → directory to find stream 1's first block.
    private static void MutatePdbGuid(byte[] pdb)
    {
        var s = pdb.AsSpan();
        var blockSize = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(s[32..]);
        var numDirBytes = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(s[44..]);
        var blockMapAddr = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(s[52..]);
        var dirBlockCount = (numDirBytes + blockSize - 1) / blockSize;
        var dir = new byte[dirBlockCount * blockSize];
        for (var i = 0; i < dirBlockCount; i++)
        {
            var b = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(s[(blockMapAddr * blockSize + i * 4)..]);
            s.Slice(b * blockSize, blockSize).CopyTo(dir.AsSpan(i * blockSize));
        }

        var p = 0;
        var numStreams = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(dir.AsSpan(p));
        p += 4;
        var sizes = new int[numStreams];
        for (var i = 0; i < numStreams; i++) { sizes[i] = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(dir.AsSpan(p)); p += 4; }
        var stream0Blocks = (Math.Max(0, sizes[0]) + blockSize - 1) / blockSize;
        p += stream0Blocks * 4;
        var stream1FirstBlock = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(dir.AsSpan(p));
        // GUID is at offset 12 in the info stream's first block.
        for (var i = 0; i < 16; i++) pdb[stream1FirstBlock * blockSize + 12 + i] ^= 0xFF;
    }

    private static MethodDefInfo FindMethod(AssemblyAnalyzer analyzer, string typeName, string methodName)
    {
        var method = analyzer.MethodDefs.FirstOrDefault(m =>
            m.DeclaringType == typeName
            && m.Name == methodName);

        Assert.IsNotNull(method);
        return method;
    }
}
