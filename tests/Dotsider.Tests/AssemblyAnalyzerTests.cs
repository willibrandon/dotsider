using System.Reflection.PortableExecutable;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Assembly Analyzer.
/// </summary>
[Collection("SampleAssemblies")]
public class AssemblyAnalyzerTests(SampleAssemblyFixture samples)
{
    // --- HelloWorld (Exe, minimal) ---

    /// <summary>
    /// Verifies hello world has correct name.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_HasCorrectName()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.Equal("HelloWorld.dll", a.FileName);
        Assert.Equal("HelloWorld", a.AssemblyName);
    }

    /// <summary>
    /// Verifies hello world has metadata.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_HasMetadata()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.True(a.HasMetadata);
        Assert.NotNull(a.GetMetadataReader());
    }

    /// <summary>
    /// Verifies hello world has target framework.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_HasTargetFramework()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.NotNull(a.TargetFramework);
        Assert.Contains("10.0", a.TargetFramework);
    }

    /// <summary>
    /// Verifies hello world has clr header with entry point.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_HasClrHeaderWithEntryPoint()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.NotNull(a.ClrHeader);
        Assert.True(a.ClrHeader!.EntryPointToken > 0);
    }

    /// <summary>
    /// Verifies hello world has pe headers.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_HasPeHeaders()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.NotNull(a.PeHeaders);
    }

    /// <summary>
    /// Verifies hello world has text section.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_HasTextSection()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.Contains(a.Sections, s => s.Name == ".text");
    }

    /// <summary>
    /// Verifies hello world raw bytes match file size.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_RawBytesMatchFileSize()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.Equal(a.FileSize, a.RawBytes.Length);
    }

    /// <summary>
    /// Verifies hello world has type defs.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_HasTypeDefs()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.NotEmpty(a.TypeDefs);
    }

    /// <summary>
    /// Verifies hello world has method defs.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_HasMethodDefs()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.NotEmpty(a.MethodDefs);
    }

    // --- RichLibrary (Library, NuGet deps) ---

    /// <summary>
    /// Verifies rich library has correct version.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_HasCorrectVersion()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.NotNull(a.AssemblyVersion);
        Assert.Contains("2.5.1", a.AssemblyVersion);
    }

    /// <summary>
    /// Verifies rich library has newton soft ref.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_HasNewtonSoftRef()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.Contains(a.AssemblyRefs, r => r.Name == "Newtonsoft.Json");
    }

    /// <summary>
    /// Verifies rich library has system text json ref.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_HasSystemTextJsonRef()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.Contains(a.AssemblyRefs, r => r.Name == "System.Text.Json");
    }

    /// <summary>
    /// Verifies rich library has service types.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_HasServiceTypes()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.Contains(a.TypeDefs, t => t.FullName == "RichLibrary.Services.UserService");
        Assert.Contains(a.TypeDefs, t => t.FullName == "RichLibrary.Services.ProductCatalog");
    }

    /// <summary>
    /// Verifies rich library has no entry point.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_HasNoEntryPoint()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.NotNull(a.ClrHeader);
        Assert.Equal(0, a.ClrHeader!.EntryPointToken);
    }

    /// <summary>
    /// Verifies rich library has many methods.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_HasManyMethods()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.True(a.MethodDefs.Count > 10);
    }

    /// <summary>
    /// Verifies rich library opens its matching portable PDB sidecar.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_OpensPortablePdbSidecar()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);

        Assert.True(a.HasPortablePdb);
        Assert.Equal(PdbProvenanceKind.Sidecar, a.PdbProvenance.Kind);
        Assert.NotNull(a.PdbProvenance.Path);
        Assert.NotNull(a.GetPdbReader());
        Assert.Contains(a.DebugDirectory, entry => entry.Type == DebugDirectoryEntryType.CodeView);
    }

    /// <summary>
    /// Verifies rich library source link mappings resolve method documents.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_SourceLink_ResolvesMethodDocument()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var method = FindMethod(a, "RichLibrary.Services.UserService", "Add");
        var debugInfo = a.GetMethodDebugInfo(method);
        var document = debugInfo.SequencePoints
            .First(point => point.Document?.EndsWith("UserService.cs", StringComparison.OrdinalIgnoreCase) == true)
            .Document!;

        var url = a.ResolveSourceLinkUrl(document);

        Assert.True(a.SourceLink.IsPresent);
        Assert.NotEmpty(a.SourceLink.Mappings);
        Assert.NotNull(url);
        Assert.Contains("raw.githubusercontent.com", url);
        Assert.EndsWith("UserService.cs", url, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies method debug info exposes sequence points and PDB local names.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_MethodDebugInfo_IncludesSequencePointsAndLocals()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var method = FindMethod(a, "RichLibrary.Services.UserService", "Add");

        var debugInfo = a.GetMethodDebugInfo(method);

        Assert.Equal(PdbProvenanceKind.Sidecar, debugInfo.Pdb.Kind);
        Assert.Contains(debugInfo.SequencePoints,
            point => point.Document?.EndsWith("UserService.cs", StringComparison.OrdinalIgnoreCase) == true
                && point.SourceLinkUrl is not null);
        Assert.Contains(debugInfo.Locals, local => local.Name == "id");
        Assert.Contains(debugInfo.Locals, local => local.Name == "user");
    }

    // --- ComplexApp (Exe, embedded resources) ---

    /// <summary>
    /// Verifies complex app has embedded resources.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ComplexApp_HasEmbeddedResources()
    {
        using var a = new AssemblyAnalyzer(samples.ComplexAppDll);
        Assert.Contains(a.Resources, r => r.Name.Contains("config.json"));
        Assert.Contains(a.Resources, r => r.Name.Contains("banner.txt"));
    }

    /// <summary>
    /// Verifies complex app has version.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ComplexApp_HasVersion()
    {
        using var a = new AssemblyAnalyzer(samples.ComplexAppDll);
        Assert.NotNull(a.AssemblyVersion);
        Assert.Contains("1.0.0", a.AssemblyVersion);
    }

    // --- MinimalApi (Web SDK) ---

    /// <summary>
    /// Verifies minimal api has asp net refs.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void MinimalApi_HasAspNetRefs()
    {
        using var a = new AssemblyAnalyzer(samples.MinimalApiDll);
        // Web SDK assemblies reference ASP.NET Core packages
        Assert.True(a.AssemblyRefs.Count > 0);
    }

    /// <summary>
    /// Verifies minimal api has record types.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void MinimalApi_HasRecordTypes()
    {
        using var a = new AssemblyAnalyzer(samples.MinimalApiDll);
        Assert.Contains(a.TypeDefs, t => t.Name == "GreetingResponse");
        Assert.Contains(a.TypeDefs, t => t.Name == "EchoRequest");
    }

    // --- NativeLib (unsafe, P/Invoke) ---

    /// <summary>
    /// Verifies native lib has p invoke methods.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeLib_HasPInvokeMethods()
    {
        using var a = new AssemblyAnalyzer(samples.NativeLibDll);
        Assert.Contains(a.TypeDefs, t => t.FullName == "NativeLib.NativeInterop");
        Assert.Contains(a.TypeDefs, t => t.FullName == "NativeLib.UnsafeOperations");
    }

    /// <summary>
    /// Verifies native lib has fixed buffer struct.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeLib_HasFixedBufferStruct()
    {
        using var a = new AssemblyAnalyzer(samples.NativeLibDll);
        Assert.Contains(a.TypeDefs, t => t.Name == "FixedBuffer");
    }

    // --- EmptyLib (minimal) ---

    /// <summary>
    /// Verifies empty lib has minimal type defs.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EmptyLib_HasMinimalTypeDefs()
    {
        using var a = new AssemblyAnalyzer(samples.EmptyLibDll);
        // <Module> + internal Module class
        Assert.True(a.TypeDefs.Count <= 3);
        Assert.True(a.TypeDefs.Count >= 1);
    }

    /// <summary>
    /// Verifies empty lib has metadata.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EmptyLib_HasMetadata()
    {
        using var a = new AssemblyAnalyzer(samples.EmptyLibDll);
        Assert.True(a.HasMetadata);
        Assert.NotNull(a.ClrHeader);
    }

    /// <summary>
    /// Verifies embedded source can be decoded from an embedded portable PDB.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EmbeddedSourceLib_DecodesEmbeddedSource()
    {
        using var a = new AssemblyAnalyzer(samples.EmbeddedSourceLibDll);
        var method = FindMethod(a, "EmbeddedSourceLib.EmbeddedSourceFixture", "Compute");

        var debugInfo = a.GetMethodDebugInfo(method);
        var source = a.GetEmbeddedSource(method);

        Assert.True(a.HasPortablePdb);
        Assert.Equal(PdbProvenanceKind.Embedded, a.PdbProvenance.Kind);
        Assert.Contains(a.DebugDirectory, entry => entry.Type == DebugDirectoryEntryType.EmbeddedPortablePdb);
        Assert.Contains(debugInfo.SequencePoints, point => point.HasEmbeddedSource);
        Assert.NotNull(source);
        Assert.Contains("return doubled + 1;", source.Text);
        Assert.NotEmpty(source.Bytes);
    }

    // --- RichLibraryV2 (same AssemblyName as V1) ---

    /// <summary>
    /// Verifies rich library v2 has same assembly name.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibraryV2_HasSameAssemblyName()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        Assert.Equal("RichLibrary", a.AssemblyName);
    }

    /// <summary>
    /// Verifies rich library v2 has version3.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibraryV2_HasVersion3()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        Assert.NotNull(a.AssemblyVersion);
        Assert.Contains("3.0.0", a.AssemblyVersion);
    }

    /// <summary>
    /// Verifies rich library v2 has new types.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibraryV2_HasNewTypes()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        Assert.Contains(a.TypeDefs, t => t.Name == "Order");
        Assert.Contains(a.TypeDefs, t => t.Name == "OrderService");
    }

    // --- Cross-assembly metadata checks ---

    /// <summary>
    /// Verifies all samples have custom attributes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void AllSamples_HaveCustomAttributes()
    {
        string[] paths = [samples.HelloWorldDll, samples.RichLibraryDll, samples.ComplexAppDll,
            samples.MinimalApiDll, samples.NativeLibDll];
        foreach (var path in paths)
        {
            using var a = new AssemblyAnalyzer(path);
            Assert.NotEmpty(a.CustomAttributes);
        }
    }

    /// <summary>
    /// Verifies all samples have positive file size.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void AllSamples_HavePositiveFileSize()
    {
        string[] paths = [samples.HelloWorldDll, samples.RichLibraryDll, samples.ComplexAppDll,
            samples.MinimalApiDll, samples.NativeLibDll, samples.EmptyLibDll];
        foreach (var path in paths)
        {
            using var a = new AssemblyAnalyzer(path);
            Assert.True(a.FileSize > 0);
        }
    }

    /// <summary>
    /// Verifies all samples have clr header.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void AllSamples_HaveClrHeader()
    {
        string[] paths = [samples.HelloWorldDll, samples.RichLibraryDll, samples.ComplexAppDll,
            samples.MinimalApiDll, samples.NativeLibDll, samples.EmptyLibDll];
        foreach (var path in paths)
        {
            using var a = new AssemblyAnalyzer(path);
            Assert.NotNull(a.ClrHeader);
            Assert.True(a.ClrHeader!.MetadataSize > 0);
        }
    }

    // --- Edge cases ---

    /// <summary>
    /// Verifies get method body returns non null for method with il.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void GetMethodBody_ReturnsNonNull_ForMethodWithIl()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var method = a.MethodDefs.First(m => m.Rva != 0);
        var body = a.GetMethodBody(method);
        Assert.NotNull(body);
    }

    /// <summary>
    /// Verifies Dispose can be called multiple times without side effects.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Dispose_IsIdempotent()
    {
        var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        a.Dispose();
        a.Dispose(); // should not throw
    }

    /// <summary>
    /// Verifies invalid file path throws file not found.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void InvalidFilePath_ThrowsFileNotFound()
    {
        var path = Path.Combine(Path.GetTempPath(), "nonexistent-dotsider-test-" + Guid.NewGuid() + ".dll");
        Assert.Throws<FileNotFoundException>(() => new AssemblyAnalyzer(path));
    }

    /// <summary>
    /// Verifies non dot net binary throws bad image format.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NonDotNetBinary_ThrowsBadImageFormat()
    {
        Assert.ThrowsAny<BadImageFormatException>(() => new AssemblyAnalyzer(samples.NonDotNetBinaryPath));
    }

    /// <summary>
    /// Verifies native apphost file constructor tolerates non pe binary.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeApphost_FileConstructor_ToleratesNonPeBinary()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldExe);

        Assert.False(a.HasMetadata);
        Assert.True(a.FileSize > 0);
        Assert.False(a.RawBytes.IsEmpty);
    }

    /// <summary>
    /// Verifies native apphost byte array constructor tolerates non pe binary.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeApphost_ByteArrayConstructor_ToleratesNonPeBinary()
    {
        var bytes = File.ReadAllBytes(samples.HelloWorldExe);
        using var a = new AssemblyAnalyzer(bytes, samples.HelloWorldExe);

        Assert.False(a.HasMetadata);
        Assert.Equal(bytes.Length, a.FileSize);
        Assert.False(a.RawBytes.IsEmpty);
    }

    /// <summary>
    /// Verifies native aot byte array constructor tolerates non pe binary.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeAot_ByteArrayConstructor_ToleratesNonPeBinary()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        var bytes = File.ReadAllBytes(samples.NativeAotConsoleExe!);
        using var a = new AssemblyAnalyzer(bytes, samples.NativeAotConsoleExe!);

        Assert.False(a.HasMetadata);
        Assert.Equal(bytes.Length, a.FileSize);
    }

    /// <summary>
    /// Verifies resolve token returns string.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ResolveToken_ReturnsString()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var method = a.MethodDefs[0];
        var result = a.ResolveToken(method.Token);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    /// <summary>
    /// Verifies architecture is not unknown.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Architecture_IsNotUnknown()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.NotEqual("Unknown", a.Architecture);
    }

    /// <summary>
    /// Verifies file properties are populated.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FileProperties_ArePopulated()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.NotEqual(default, a.LastModified);
        Assert.NotEqual(default, a.CreatedTime);
    }

    // --- Additional coverage tests ---

    /// <summary>
    /// Verifies file path is absolute.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FilePath_IsAbsolute()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.Equal(samples.HelloWorldDll, a.FilePath);
        Assert.True(Path.IsPathRooted(a.FilePath));
    }

    /// <summary>
    /// Verifies rich library type refs non empty.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_TypeRefs_NonEmpty()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.NotEmpty(a.TypeRefs);
    }

    /// <summary>
    /// Verifies rich library member refs non empty.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_MemberRefs_NonEmpty()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.NotEmpty(a.MemberRefs);
        var first = a.MemberRefs[0];
        Assert.NotNull(first.Name);
        Assert.NotNull(first.DeclaringType);
    }

    /// <summary>
    /// Verifies complex app resources have offsets.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ComplexApp_Resources_HaveOffsets()
    {
        using var a = new AssemblyAnalyzer(samples.ComplexAppDll);
        foreach (var r in a.Resources)
        {
            Assert.NotNull(r.Name);
            Assert.True(r.Size >= 0);
        }
    }

    /// <summary>
    /// Verifies rich library culture is neutral.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_Culture_IsNeutral()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.Equal("neutral", a.Culture);
    }

    /// <summary>
    /// Verifies hello world is read only is false.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_IsReadOnly_IsFalse()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.False(a.IsReadOnly);
    }

    /// <summary>
    /// Verifies rich library type defs have properties.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_TypeDefs_HaveProperties()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var userService = a.TypeDefs.First(t => t.Name == "UserService");
        Assert.NotNull(userService.FullName);
        Assert.NotNull(userService.Namespace);
        Assert.True(userService.Token > 0);
    }

    /// <summary>
    /// Verifies rich library method defs have signatures.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_MethodDefs_HaveSignatures()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var methods = a.MethodDefs.Where(m => m.Rva != 0).Take(5);
        foreach (var m in methods)
        {
            Assert.NotNull(m.Name);
            Assert.NotNull(m.Signature);
            Assert.True(m.Token > 0);
        }
    }

    /// <summary>
    /// Verifies rich library type refs have properties.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_TypeRefs_HaveProperties()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        foreach (var tr in a.TypeRefs.Take(5))
        {
            Assert.NotNull(tr.Name);
            Assert.NotNull(tr.Namespace);
        }
    }

    /// <summary>
    /// Verifies resolve token type def.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ResolveToken_TypeDef()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var typeDef = a.TypeDefs.First(t => t.Name != "<Module>");
        var resolved = a.ResolveToken(typeDef.Token);
        Assert.Contains(typeDef.Name, resolved);
    }

    /// <summary>
    /// Verifies resolve token type ref.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ResolveToken_TypeRef()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var typeRef = a.TypeRefs[0];
        var resolved = a.ResolveToken(typeRef.Token);
        Assert.NotEmpty(resolved);
    }

    /// <summary>
    /// Verifies resolve token member ref.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ResolveToken_MemberRef()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var memberRef = a.MemberRefs[0];
        var resolved = a.ResolveToken(memberRef.Token);
        Assert.NotEmpty(resolved);
    }

    /// <summary>
    /// Verifies resolve token invalid token returns hex string.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ResolveToken_InvalidToken_ReturnsHexString()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var result = a.ResolveToken(0x7F000001);
        Assert.Contains("0x", result);
    }

    /// <summary>
    /// Verifies rich library assembly refs have version and token.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_AssemblyRefs_HaveVersionAndToken()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var newtonsoftRef = a.AssemblyRefs.First(r => r.Name == "Newtonsoft.Json");
        Assert.NotNull(newtonsoftRef.Version);
        Assert.NotNull(newtonsoftRef.PublicKeyToken);
        Assert.NotNull(newtonsoftRef.Culture);
    }

    /// <summary>
    /// Verifies rich library custom attributes have properties.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_CustomAttributes_HaveProperties()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        foreach (var attr in a.CustomAttributes.Take(3))
        {
            Assert.NotNull(attr.Constructor);
            Assert.NotNull(attr.Parent);
        }
    }

    /// <summary>
    /// Verifies hello world get method body returns non null body.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_GetMethodBody_ReturnsNonNullBody()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var method = a.MethodDefs.First(m => m.Rva != 0);
        var body = a.GetMethodBody(method);
        Assert.NotNull(body);
        Assert.True(body!.GetILBytes()!.Length > 0);
    }

    /// <summary>
    /// Verifies rich library sections have properties.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_Sections_HaveProperties()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var textSection = a.Sections.First(s => s.Name == ".text");
        Assert.True(textSection.RawDataSize > 0);
        Assert.True(textSection.VirtualAddress > 0);
    }

    /// <summary>
    /// Verifies rich library pe headers have valid fields.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_PeHeaders_HaveValidFields()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.NotNull(a.PeHeaders);
        Assert.NotEqual((Machine)0, a.PeHeaders!.Machine);
        Assert.NotEqual((Characteristics)0, a.PeHeaders.Characteristics);
    }

    /// <summary>
    /// Verifies access after dispose assembly refs throws instead of crashing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void AccessAfterDispose_AssemblyRefs_ThrowsInsteadOfCrashing()
    {
        var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.True(a.HasMetadata);
        a.Dispose();

        // After Dispose, accessing AssemblyRefs must throw ObjectDisposedException
        // rather than crashing the process with AccessViolationException from
        // reading freed metadata memory.
        Assert.Throws<ObjectDisposedException>(() => _ = a.AssemblyRefs);
    }

    /// <summary>
    /// Verifies access after dispose type defs throws instead of crashing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void AccessAfterDispose_TypeDefs_ThrowsInsteadOfCrashing()
    {
        var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        a.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _ = a.TypeDefs);
    }

    /// <summary>
    /// Verifies access after dispose method defs throws instead of crashing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void AccessAfterDispose_MethodDefs_ThrowsInsteadOfCrashing()
    {
        var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        a.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _ = a.MethodDefs);
    }

    /// <summary>
    /// Verifies access after dispose field defs throws instead of crashing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void AccessAfterDispose_FieldDefs_ThrowsInsteadOfCrashing()
    {
        var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        a.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _ = a.FieldDefs);
    }

    /// <summary>
    /// Verifies access after dispose get method body throws instead of crashing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void AccessAfterDispose_GetMethodBody_ThrowsInsteadOfCrashing()
    {
        var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var method = a.MethodDefs.First(m => m.Rva > 0);
        a.Dispose();
        Assert.Throws<ObjectDisposedException>(() => a.GetMethodBody(method));
    }

    /// <summary>
    /// Verifies access after dispose resolve token throws instead of crashing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void AccessAfterDispose_ResolveToken_ThrowsInsteadOfCrashing()
    {
        var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var token = a.MethodDefs.First(m => m.Rva > 0).Token;
        a.Dispose();
        Assert.Throws<ObjectDisposedException>(() => a.ResolveToken(token));
    }

    /// <summary>
    /// Verifies the analyzer finds and parses the mstat and DGML sidecars published next to
    /// the Native AOT sample, preferring the codegen graph.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Sidecars_NativeAotExe_ProbeAndParse()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        using var a = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);

        Assert.Equal(samples.NativeAotConsoleMstat, a.MstatPath);
        Assert.NotNull(a.Mstat);
        Assert.NotEmpty(a.Mstat.Methods);

        if (samples.NativeAotConsoleDgml is not null)
        {
            Assert.Equal(samples.NativeAotConsoleDgml, a.DgmlPath);
            Assert.NotNull(a.Dgml);
            Assert.NotEmpty(a.Dgml.Nodes);
        }
    }

    /// <summary>
    /// Verifies an AOT binary with no sidecars beside it probes to null without throwing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Sidecars_NativeAotExeWithoutSidecars_ReturnNull()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var dir = Directory.CreateTempSubdirectory("dotsider-nosidecar-");
        try
        {
            var exeCopy = Path.Combine(dir.FullName, Path.GetFileName(samples.NativeAotConsoleExe!));
            File.Copy(samples.NativeAotConsoleExe!, exeCopy);
            using var a = new AssemblyAnalyzer(exeCopy);

            Assert.Null(a.MstatPath);
            Assert.Null(a.Mstat);
            Assert.Null(a.DgmlPath);
            Assert.Null(a.Dgml);
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
    [Fact(Timeout = 30_000)]
    public void Sidecars_ManagedAssemblyWithStrayMstat_ReturnsNull()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var dir = Directory.CreateTempSubdirectory("dotsider-stray-");
        try
        {
            var dllCopy = Path.Combine(dir.FullName, "RichLibrary.dll");
            File.Copy(samples.RichLibraryDll, dllCopy);
            File.Copy(samples.NativeAotConsoleMstat!, Path.Combine(dir.FullName, "RichLibrary.mstat"));
            using var a = new AssemblyAnalyzer(dllCopy);

            Assert.Null(a.MstatPath);
            Assert.Null(a.Mstat);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a corrupt sidecar reads as null rather than throwing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Sidecars_CorruptMstat_ReturnsNull()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var dir = Directory.CreateTempSubdirectory("dotsider-corrupt-");
        try
        {
            var name = Path.GetFileName(samples.NativeAotConsoleExe!);
            var exeCopy = Path.Combine(dir.FullName, name);
            File.Copy(samples.NativeAotConsoleExe!, exeCopy);
            var stem = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
            File.WriteAllBytes(Path.Combine(dir.FullName, stem + ".mstat"), [0xDE, 0xAD]);
            using var a = new AssemblyAnalyzer(exeCopy);

            Assert.NotNull(a.MstatPath);
            Assert.Null(a.Mstat);
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
    [Fact(Timeout = 30_000)]
    public void Provenance_NativeAotExeWithMatchingPdb_IsNativePdb()
    {
        Assert.SkipWhen(
            samples.NativeAotConsoleSymbols is null
            || !samples.NativeAotConsoleSymbols.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase),
            "native PDB not present on this platform");

        using var a = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);

        Assert.Equal(PdbProvenanceKind.NativePdb, a.PdbProvenance.Kind);
        Assert.Equal(samples.NativeAotConsoleSymbols, a.PdbProvenance.Path);
    }

    /// <summary>
    /// Verifies a Native AOT binary copied away from its PDB falls back to UnsupportedWindowsPdb.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Provenance_NativeAotExeWithoutPdb_IsUnsupportedWindowsPdb()
    {
        Assert.SkipWhen(
            samples.NativeAotConsoleSymbols is null
            || !samples.NativeAotConsoleSymbols.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase),
            "native PDB not present on this platform");

        var dir = Directory.CreateTempSubdirectory("dotsider-nopdb-");
        try
        {
            var exeCopy = Path.Combine(dir.FullName, Path.GetFileName(samples.NativeAotConsoleExe!));
            File.Copy(samples.NativeAotConsoleExe!, exeCopy);
            using var a = new AssemblyAnalyzer(exeCopy);

            Assert.Equal(PdbProvenanceKind.UnsupportedWindowsPdb, a.PdbProvenance.Kind);
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
    [Fact(Timeout = 30_000)]
    public void Provenance_NativeAotExeWithWrongPdb_IsUnsupportedWindowsPdb()
    {
        Assert.SkipWhen(
            samples.NativeAotConsoleSymbols is null
            || !samples.NativeAotConsoleSymbols.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase),
            "native PDB not present on this platform");

        var dir = Directory.CreateTempSubdirectory("dotsider-wrongpdb-");
        try
        {
            var name = Path.GetFileName(samples.NativeAotConsoleExe!);
            var exeCopy = Path.Combine(dir.FullName, name);
            File.Copy(samples.NativeAotConsoleExe!, exeCopy);
            var stem = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

            // A valid PDB whose GUID no longer matches the binary: flip the info-stream GUID bytes
            // in a copy of the real PDB, leaving the container otherwise intact.
            var pdb = File.ReadAllBytes(samples.NativeAotConsoleSymbols!);
            MutatePdbGuid(pdb);
            File.WriteAllBytes(Path.Combine(dir.FullName, stem + ".pdb"), pdb);

            using var a = new AssemblyAnalyzer(exeCopy);

            Assert.Equal(PdbProvenanceKind.UnsupportedWindowsPdb, a.PdbProvenance.Kind);
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

        Assert.NotNull(method);
        return method;
    }
}
