using System.Reflection.PortableExecutable;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class AssemblyAnalyzerTests(SampleAssemblyFixture samples)
{
    // --- HelloWorld (Exe, minimal) ---

    [Fact(Timeout = 5_000)]
    public void HelloWorld_HasCorrectName()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.Equal("HelloWorld.dll", a.FileName);
        Assert.Equal("HelloWorld", a.AssemblyName);
    }

    [Fact(Timeout = 5_000)]
    public void HelloWorld_HasMetadata()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.True(a.HasMetadata);
        Assert.NotNull(a.GetMetadataReader());
    }

    [Fact(Timeout = 5_000)]
    public void HelloWorld_HasTargetFramework()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.NotNull(a.TargetFramework);
        Assert.Contains("10.0", a.TargetFramework);
    }

    [Fact(Timeout = 5_000)]
    public void HelloWorld_HasClrHeaderWithEntryPoint()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.NotNull(a.ClrHeader);
        Assert.True(a.ClrHeader!.EntryPointToken > 0);
    }

    [Fact(Timeout = 5_000)]
    public void HelloWorld_HasPeHeaders()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.NotNull(a.PeHeaders);
    }

    [Fact(Timeout = 5_000)]
    public void HelloWorld_HasTextSection()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.Contains(a.Sections, s => s.Name == ".text");
    }

    [Fact(Timeout = 5_000)]
    public void HelloWorld_RawBytesMatchFileSize()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.Equal(a.FileSize, a.RawBytes.Length);
    }

    [Fact(Timeout = 5_000)]
    public void HelloWorld_HasTypeDefs()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.NotEmpty(a.TypeDefs);
    }

    [Fact(Timeout = 5_000)]
    public void HelloWorld_HasMethodDefs()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.NotEmpty(a.MethodDefs);
    }

    // --- RichLibrary (Library, NuGet deps) ---

    [Fact(Timeout = 5_000)]
    public void RichLibrary_HasCorrectVersion()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.NotNull(a.AssemblyVersion);
        Assert.Contains("2.5.1", a.AssemblyVersion);
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_HasNewtonSoftRef()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.Contains(a.AssemblyRefs, r => r.Name == "Newtonsoft.Json");
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_HasSystemTextJsonRef()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.Contains(a.AssemblyRefs, r => r.Name == "System.Text.Json");
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_HasServiceTypes()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.Contains(a.TypeDefs, t => t.FullName == "RichLibrary.Services.UserService");
        Assert.Contains(a.TypeDefs, t => t.FullName == "RichLibrary.Services.ProductCatalog");
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_HasNoEntryPoint()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.NotNull(a.ClrHeader);
        Assert.Equal(0, a.ClrHeader!.EntryPointToken);
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_HasManyMethods()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.True(a.MethodDefs.Count > 10);
    }

    // --- ComplexApp (Exe, embedded resources) ---

    [Fact(Timeout = 5_000)]
    public void ComplexApp_HasEmbeddedResources()
    {
        using var a = new AssemblyAnalyzer(samples.ComplexAppDll);
        Assert.Contains(a.Resources, r => r.Name.Contains("config.json"));
        Assert.Contains(a.Resources, r => r.Name.Contains("banner.txt"));
    }

    [Fact(Timeout = 5_000)]
    public void ComplexApp_HasVersion()
    {
        using var a = new AssemblyAnalyzer(samples.ComplexAppDll);
        Assert.NotNull(a.AssemblyVersion);
        Assert.Contains("1.0.0", a.AssemblyVersion);
    }

    // --- MinimalApi (Web SDK) ---

    [Fact(Timeout = 5_000)]
    public void MinimalApi_HasAspNetRefs()
    {
        using var a = new AssemblyAnalyzer(samples.MinimalApiDll);
        // Web SDK assemblies reference ASP.NET Core packages
        Assert.True(a.AssemblyRefs.Count > 0);
    }

    [Fact(Timeout = 5_000)]
    public void MinimalApi_HasRecordTypes()
    {
        using var a = new AssemblyAnalyzer(samples.MinimalApiDll);
        Assert.Contains(a.TypeDefs, t => t.Name == "GreetingResponse");
        Assert.Contains(a.TypeDefs, t => t.Name == "EchoRequest");
    }

    // --- NativeLib (unsafe, P/Invoke) ---

    [Fact(Timeout = 5_000)]
    public void NativeLib_HasPInvokeMethods()
    {
        using var a = new AssemblyAnalyzer(samples.NativeLibDll);
        Assert.Contains(a.TypeDefs, t => t.FullName == "NativeLib.NativeInterop");
        Assert.Contains(a.TypeDefs, t => t.FullName == "NativeLib.UnsafeOperations");
    }

    [Fact(Timeout = 5_000)]
    public void NativeLib_HasFixedBufferStruct()
    {
        using var a = new AssemblyAnalyzer(samples.NativeLibDll);
        Assert.Contains(a.TypeDefs, t => t.Name == "FixedBuffer");
    }

    // --- EmptyLib (minimal) ---

    [Fact(Timeout = 5_000)]
    public void EmptyLib_HasMinimalTypeDefs()
    {
        using var a = new AssemblyAnalyzer(samples.EmptyLibDll);
        // <Module> + internal Module class
        Assert.True(a.TypeDefs.Count <= 3);
        Assert.True(a.TypeDefs.Count >= 1);
    }

    [Fact(Timeout = 5_000)]
    public void EmptyLib_HasMetadata()
    {
        using var a = new AssemblyAnalyzer(samples.EmptyLibDll);
        Assert.True(a.HasMetadata);
        Assert.NotNull(a.ClrHeader);
    }

    // --- RichLibraryV2 (same AssemblyName as V1) ---

    [Fact(Timeout = 5_000)]
    public void RichLibraryV2_HasSameAssemblyName()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        Assert.Equal("RichLibrary", a.AssemblyName);
    }

    [Fact(Timeout = 5_000)]
    public void RichLibraryV2_HasVersion3()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        Assert.NotNull(a.AssemblyVersion);
        Assert.Contains("3.0.0", a.AssemblyVersion);
    }

    [Fact(Timeout = 5_000)]
    public void RichLibraryV2_HasNewTypes()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        Assert.Contains(a.TypeDefs, t => t.Name == "Order");
        Assert.Contains(a.TypeDefs, t => t.Name == "OrderService");
    }

    // --- Cross-assembly metadata checks ---

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
    public void GetMethodBody_ReturnsNonNull_ForMethodWithIl()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var method = a.MethodDefs.First(m => m.Rva != 0);
        var body = a.GetMethodBody(method);
        Assert.NotNull(body);
    }

    [Fact(Timeout = 5_000)]
    public void Dispose_IsIdempotent()
    {
        var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        a.Dispose();
        a.Dispose(); // should not throw
    }

    [Fact(Timeout = 5_000)]
    public void InvalidFilePath_ThrowsFileNotFound()
    {
        var path = Path.Combine(Path.GetTempPath(), "nonexistent-dotsider-test-" + Guid.NewGuid() + ".dll");
        Assert.Throws<FileNotFoundException>(() => new AssemblyAnalyzer(path));
    }

    [Fact(Timeout = 5_000)]
    public void NonDotNetBinary_ThrowsBadImageFormat()
    {
        Assert.ThrowsAny<BadImageFormatException>(() => new AssemblyAnalyzer(samples.NonDotNetBinaryPath));
    }

    [Fact(Timeout = 5_000)]
    public void ResolveToken_ReturnsString()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var method = a.MethodDefs.First();
        var result = a.ResolveToken(method.Token);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact(Timeout = 5_000)]
    public void Architecture_IsNotUnknown()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.NotEqual("Unknown", a.Architecture);
    }

    [Fact(Timeout = 5_000)]
    public void FileProperties_ArePopulated()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.NotEqual(default, a.LastModified);
        Assert.NotEqual(default, a.CreatedTime);
    }

    // --- Additional coverage tests ---

    [Fact(Timeout = 5_000)]
    public void FilePath_IsAbsolute()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.Equal(samples.HelloWorldDll, a.FilePath);
        Assert.True(Path.IsPathRooted(a.FilePath));
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_TypeRefs_NonEmpty()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.NotEmpty(a.TypeRefs);
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_MemberRefs_NonEmpty()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.NotEmpty(a.MemberRefs);
        var first = a.MemberRefs[0];
        Assert.NotNull(first.Name);
        Assert.NotNull(first.DeclaringType);
    }

    [Fact(Timeout = 5_000)]
    public void ComplexApp_Resources_HaveOffsets()
    {
        using var a = new AssemblyAnalyzer(samples.ComplexAppDll);
        foreach (var r in a.Resources)
        {
            Assert.NotNull(r.Name);
            Assert.True(r.Size >= 0);
        }
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_Culture_IsNeutral()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.Equal("neutral", a.Culture);
    }

    [Fact(Timeout = 5_000)]
    public void HelloWorld_IsReadOnly_IsFalse()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.False(a.IsReadOnly);
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_TypeDefs_HaveProperties()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var userService = a.TypeDefs.First(t => t.Name == "UserService");
        Assert.NotNull(userService.FullName);
        Assert.NotNull(userService.Namespace);
        Assert.True(userService.Token > 0);
    }

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
    public void RichLibrary_TypeRefs_HaveProperties()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        foreach (var tr in a.TypeRefs.Take(5))
        {
            Assert.NotNull(tr.Name);
            Assert.NotNull(tr.Namespace);
        }
    }

    [Fact(Timeout = 5_000)]
    public void ResolveToken_TypeDef()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var typeDef = a.TypeDefs.First(t => t.Name != "<Module>");
        var resolved = a.ResolveToken(typeDef.Token);
        Assert.Contains(typeDef.Name, resolved);
    }

    [Fact(Timeout = 5_000)]
    public void ResolveToken_TypeRef()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var typeRef = a.TypeRefs.First();
        var resolved = a.ResolveToken(typeRef.Token);
        Assert.NotEmpty(resolved);
    }

    [Fact(Timeout = 5_000)]
    public void ResolveToken_MemberRef()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var memberRef = a.MemberRefs.First();
        var resolved = a.ResolveToken(memberRef.Token);
        Assert.NotEmpty(resolved);
    }

    [Fact(Timeout = 5_000)]
    public void ResolveToken_InvalidToken_ReturnsHexString()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var result = a.ResolveToken(0x7F000001);
        Assert.Contains("0x", result);
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_AssemblyRefs_HaveVersionAndToken()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var newtonsoftRef = a.AssemblyRefs.First(r => r.Name == "Newtonsoft.Json");
        Assert.NotNull(newtonsoftRef.Version);
        Assert.NotNull(newtonsoftRef.PublicKeyToken);
        Assert.NotNull(newtonsoftRef.Culture);
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_CustomAttributes_HaveProperties()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        foreach (var attr in a.CustomAttributes.Take(3))
        {
            Assert.NotNull(attr.Constructor);
            Assert.NotNull(attr.Parent);
        }
    }

    [Fact(Timeout = 5_000)]
    public void HelloWorld_GetMethodBody_ReturnsNonNullBody()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var method = a.MethodDefs.First(m => m.Rva != 0);
        var body = a.GetMethodBody(method);
        Assert.NotNull(body);
        Assert.True(body!.GetILBytes()!.Length > 0);
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_Sections_HaveProperties()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var textSection = a.Sections.First(s => s.Name == ".text");
        Assert.True(textSection.RawDataSize > 0);
        Assert.True(textSection.VirtualAddress > 0);
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_PeHeaders_HaveValidFields()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.NotNull(a.PeHeaders);
        Assert.NotEqual((Machine)0, a.PeHeaders!.Machine);
        Assert.NotEqual((Characteristics)0, a.PeHeaders.Characteristics);
    }
}
