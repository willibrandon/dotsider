using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the pre-ILC sidecar detector: origin precedence, tree recognition,
/// response-file parsing, reference categorization, and PDB status.
/// </summary>
[TestClass]
public class PreIlcSidecarDetectorTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private readonly List<string> _tempFiles = [];

    private string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"dotsider-preilc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempFiles.Add(dir);
        return dir;
    }

    private static void WriteDummyBinary(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [0x4D, 0x5A, 0x00, 0x00]);
    }

    private static void CopyInto(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination);
    }

    /// <summary>Creates a classic <c>bin\cfg\tfm\rid[\publish]</c> tree with a dummy binary and returns its path.</summary>
    private static string CreateClassicTree(string root, string stem, out string objDir, bool publish = true)
    {
        var projDir = Path.Combine(root, stem);
        var ridDir = Path.Combine(projDir, "bin", "Release", "net10.0", "win-x64");
        var exeDir = publish ? Path.Combine(ridDir, "publish") : ridDir;
        objDir = Path.Combine(projDir, "obj", "Release", "net10.0", "win-x64");
        Directory.CreateDirectory(objDir);
        var exePath = Path.Combine(exeDir, stem + ".exe");
        WriteDummyBinary(exePath);
        return exePath;
    }

    /// <summary>Verifies the classic publish tree yields the intermediate managed input.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_ClassicPublishTree_FindsBuildTreeLayoutDll()
    {
        var root = NewTempDir();
        var exe = CreateClassicTree(root, "HelloWorld", out var objDir);
        CopyInto(Samples.HelloWorldDll, Path.Combine(objDir, "HelloWorld.dll"));

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.HasAttachableCompanion);
        Assert.AreEqual(PreIlcAssemblyOrigin.BuildTreeLayout, result.Origin);
        Assert.AreEqual(Path.Combine(objDir, "HelloWorld.dll"), result.ManagedAssemblyPath);
    }

    /// <summary>Verifies the classic non-publish bin directory is recognized too.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_ClassicBinTree_FindsBuildTreeLayoutDll()
    {
        var root = NewTempDir();
        var exe = CreateClassicTree(root, "HelloWorld", out var objDir, publish: false);
        CopyInto(Samples.HelloWorldDll, Path.Combine(objDir, "HelloWorld.dll"));

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.AreEqual(PreIlcAssemblyOrigin.BuildTreeLayout, result!.Origin);
    }

    /// <summary>Verifies the artifacts layout maps publish\proj\pivot to obj\proj\pivot by segment substitution.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_ArtifactsLayout_FindsBuildTreeLayoutDll()
    {
        var root = NewTempDir();
        var exe = Path.Combine(root, "artifacts", "publish", "HelloWorld", "release_win-x64", "HelloWorld.exe");
        WriteDummyBinary(exe);
        var objDir = Path.Combine(root, "artifacts", "obj", "HelloWorld", "release_win-x64");
        CopyInto(Samples.HelloWorldDll, Path.Combine(objDir, "HelloWorld.dll"));

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.AreEqual(PreIlcAssemblyOrigin.BuildTreeLayout, result!.Origin);
        Assert.AreEqual(Path.Combine(objDir, "HelloWorld.dll"), result.ManagedAssemblyPath);
    }

    /// <summary>Verifies the response file outranks the conventional obj location.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_RspAndObjBothPresent_RspWins()
    {
        var root = NewTempDir();
        var exe = CreateClassicTree(root, "HelloWorld", out var objDir);
        CopyInto(Samples.HelloWorldDll, Path.Combine(objDir, "HelloWorld.dll"));
        var altDir = Path.Combine(objDir, "alt");
        CopyInto(Samples.HelloWorldDll, Path.Combine(altDir, "HelloWorld.dll"));
        var nativeDir = Path.Combine(objDir, "native");
        Directory.CreateDirectory(nativeDir);
        File.WriteAllLines(Path.Combine(nativeDir, "HelloWorld.ilc.rsp"),
        [
            Path.Combine("obj", "Release", "net10.0", "win-x64", "alt", "HelloWorld.dll"),
            "-o:obj\\Release\\net10.0\\win-x64\\native\\HelloWorld.obj",
        ]);

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.AreEqual(PreIlcAssemblyOrigin.IlcResponseFile, result!.Origin);
        Assert.AreEqual(Path.Combine(altDir, "HelloWorld.dll"), result.ManagedAssemblyPath);
        Assert.AreEqual(Path.Combine(nativeDir, "HelloWorld.ilc.rsp"), result.IlcResponseFilePath);
    }

    /// <summary>Verifies quoted and absolute root-input tokens both resolve.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_RspRootQuotedAndAbsolute_Resolves()
    {
        var root = NewTempDir();
        var exe = CreateClassicTree(root, "HelloWorld", out var objDir);
        var dllPath = Path.Combine(objDir, "HelloWorld.dll");
        CopyInto(Samples.HelloWorldDll, dllPath);
        var nativeDir = Path.Combine(objDir, "native");
        Directory.CreateDirectory(nativeDir);
        File.WriteAllLines(Path.Combine(nativeDir, "HelloWorld.ilc.rsp"), [$"\"{dllPath}\""]);

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.AreEqual(PreIlcAssemblyOrigin.IlcResponseFile, result!.Origin);
        Assert.AreEqual(dllPath, result.ManagedAssemblyPath);
    }

    /// <summary>Verifies the separated -r value form and inline forms are all collected.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_RspSeparatedReferenceForm_CollectsReference()
    {
        var root = NewTempDir();
        var exe = CreateClassicTree(root, "HelloWorld", out var objDir);
        var dllPath = Path.Combine(objDir, "HelloWorld.dll");
        CopyInto(Samples.HelloWorldDll, dllPath);
        var libPath = Path.Combine(root, "libproj", "bin", "Release", "net10.0", "RichLibrary.dll");
        CopyInto(Samples.RichLibraryDll, libPath);
        var nativeDir = Path.Combine(objDir, "native");
        Directory.CreateDirectory(nativeDir);
        File.WriteAllLines(Path.Combine(nativeDir, "HelloWorld.ilc.rsp"), [dllPath, "-r", libPath]);

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.Contains(libPath, result!.LocalReferencePaths);
    }

    /// <summary>Verifies @-inclusion expands nested response files and a cycle is survived with a note.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_RspIncludesAndCycle_ExpandsAndSurvives()
    {
        var root = NewTempDir();
        var exe = CreateClassicTree(root, "HelloWorld", out var objDir);
        var dllPath = Path.Combine(objDir, "HelloWorld.dll");
        CopyInto(Samples.HelloWorldDll, dllPath);
        var nativeDir = Path.Combine(objDir, "native");
        Directory.CreateDirectory(nativeDir);
        var rsp = Path.Combine(nativeDir, "HelloWorld.ilc.rsp");
        var inner = Path.Combine(nativeDir, "inner.rsp");
        File.WriteAllLines(rsp, ["@inner.rsp"]);
        File.WriteAllLines(inner, [dllPath, "@HelloWorld.ilc.rsp"]);

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.AreEqual(PreIlcAssemblyOrigin.IlcResponseFile, result!.Origin);
        Assert.AreEqual(dllPath, result.ManagedAssemblyPath);
        Assert.Contains("cycle", result.Details!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies references are categorized: package store summarized, local evidence listed,
    /// unclassifiable counted, missing recorded as unresolved.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_ReferenceCategorization_SeparatesPackageLocalOtherUnresolved()
    {
        var root = NewTempDir();
        var exe = CreateClassicTree(root, "HelloWorld", out var objDir);
        var dllPath = Path.Combine(objDir, "HelloWorld.dll");
        CopyInto(Samples.HelloWorldDll, dllPath);

        var packageRef = Path.Combine(root, "custom-cache",
            "microsoft.netcore.app.runtime.nativeaot.win-x64", "10.0.9", "lib", "System.Runtime.dll");
        WriteDummyBinary(packageRef);
        var otherPackageRef = Path.Combine(root, "custom-cache", "somelib", "1.0.0", "lib", "SomeLib.dll");
        WriteDummyBinary(otherPackageRef);

        var localRef = Path.Combine(root, "libproj", "bin", "Release", "net10.0", "RichLibrary.dll");
        CopyInto(Samples.RichLibraryDll, localRef);

        var unclassifiable = Path.Combine(root, "misc", "Elsewhere.dll");
        CopyInto(Samples.RichLibraryDll, unclassifiable);

        var missing = Path.Combine(root, "gone", "Missing.dll");

        var nativeDir = Path.Combine(objDir, "native");
        Directory.CreateDirectory(nativeDir);
        File.WriteAllLines(Path.Combine(nativeDir, "HelloWorld.ilc.rsp"),
        [
            dllPath,
            $"-r:{packageRef}",
            $"-r:{otherPackageRef}",
            $"--reference:{localRef}",
            $"-r:{unclassifiable}",
            $"-r:{missing}",
        ]);

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result!.PackageReferenceCount);
        Assert.AreSequenceEqual([localRef], result.LocalReferencePaths);
        Assert.AreEqual(1, result.OtherReferenceCount);
        Assert.Contains("Elsewhere.dll", result.Details!, StringComparison.Ordinal);
        Assert.AreSequenceEqual([missing], result.UnresolvedReferencePaths);
    }

    /// <summary>Verifies a missing response-file root falls through to the obj layout with a note.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_RspRootMissing_FallsBackToObjLayout()
    {
        var root = NewTempDir();
        var exe = CreateClassicTree(root, "HelloWorld", out var objDir);
        CopyInto(Samples.HelloWorldDll, Path.Combine(objDir, "HelloWorld.dll"));
        var nativeDir = Path.Combine(objDir, "native");
        Directory.CreateDirectory(nativeDir);
        File.WriteAllLines(Path.Combine(nativeDir, "HelloWorld.ilc.rsp"),
            [Path.Combine("obj", "does-not-exist", "HelloWorld.dll")]);

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.AreEqual(PreIlcAssemblyOrigin.BuildTreeLayout, result!.Origin);
        Assert.Contains("fell back", result.Details!, StringComparison.Ordinal);
    }

    /// <summary>Verifies a lone sibling dll is offered with sibling provenance.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_SiblingOnly_FindsSiblingAssembly()
    {
        var dir = NewTempDir();
        var exe = Path.Combine(dir, "HelloWorld.exe");
        WriteDummyBinary(exe);
        CopyInto(Samples.HelloWorldDll, Path.Combine(dir, "HelloWorld.dll"));

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.AreEqual(PreIlcAssemblyOrigin.SiblingAssembly, result!.Origin);
    }

    /// <summary>Verifies extensionless (Linux) binaries keep their full name as the stem.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_ExtensionlessBinary_UsesFullNameStem()
    {
        var dir = NewTempDir();
        var exe = Path.Combine(dir, "HelloWorld");
        WriteDummyBinary(exe);
        CopyInto(Samples.HelloWorldDll, Path.Combine(dir, "HelloWorld.dll"));

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.HasAttachableCompanion);
    }

    /// <summary>Verifies native AOT library extensions (.so, .dylib) are stripped for the stem.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_LibraryExtensions_StripForStem()
    {
        foreach (var ext in new[] { ".so", ".dylib" })
        {
            var dir = NewTempDir();
            var binary = Path.Combine(dir, "RichLibrary" + ext);
            WriteDummyBinary(binary);
            CopyInto(Samples.RichLibraryDll, Path.Combine(dir, "RichLibrary.dll"));

            var result = PreIlcSidecarDetector.Find(binary);

            Assert.IsNotNull(result);
            Assert.AreEqual(PreIlcAssemblyOrigin.SiblingAssembly, result!.Origin);
        }
    }

    /// <summary>Verifies a Windows native AOT library never offers itself (sibling == binary path).</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_NativeLibrarySelfCollision_ReturnsNull()
    {
        var dir = NewTempDir();
        var binary = Path.Combine(dir, "HelloWorld.dll");
        WriteDummyBinary(binary);

        var result = PreIlcSidecarDetector.Find(binary);

        Assert.IsNull(result);
    }

    /// <summary>Verifies uppercase segments and forward slashes are recognized.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_UppercaseSegmentsAndForwardSlashes_Recognized()
    {
        var root = NewTempDir();
        var exe = CreateClassicTree(root, "HelloWorld", out var objDir);
        CopyInto(Samples.HelloWorldDll, Path.Combine(objDir, "HelloWorld.dll"));

        var mangled = exe.Replace('\\', '/')
            .Replace("/bin/", "/BIN/", StringComparison.Ordinal)
            .Replace("/publish/", "/PUBLISH/", StringComparison.Ordinal);
        var result = PreIlcSidecarDetector.Find(mangled);

        Assert.IsNotNull(result);
        Assert.AreEqual(PreIlcAssemblyOrigin.BuildTreeLayout, result!.Origin);
    }

    /// <summary>Verifies a sibling whose assembly name does not match the stem is rejected.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_SiblingNameMismatch_ReturnsNull()
    {
        var dir = NewTempDir();
        var exe = Path.Combine(dir, "HelloWorld.exe");
        WriteDummyBinary(exe);
        CopyInto(Samples.RichLibraryDll, Path.Combine(dir, "HelloWorld.dll"));

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNull(result);
    }

    /// <summary>Verifies a metadata-less sibling file is rejected.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_SiblingWithoutMetadata_ReturnsNull()
    {
        var dir = NewTempDir();
        var exe = Path.Combine(dir, "HelloWorld.exe");
        WriteDummyBinary(exe);
        WriteDummyBinary(Path.Combine(dir, "HelloWorld.dll"));

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNull(result);
    }

    /// <summary>Verifies a matching sidecar PDB reports Matched.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_MatchingPdb_ReportsMatched()
    {
        var sourcePdb = Path.ChangeExtension(Samples.HelloWorldDll, ".pdb");
        TestSkip.When(!File.Exists(sourcePdb), "HelloWorld.pdb was not produced");

        var dir = NewTempDir();
        var exe = Path.Combine(dir, "HelloWorld.exe");
        WriteDummyBinary(exe);
        CopyInto(Samples.HelloWorldDll, Path.Combine(dir, "HelloWorld.dll"));
        CopyInto(sourcePdb, Path.Combine(dir, "HelloWorld.pdb"));

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.AreEqual(PreIlcPdbStatus.Matched, result!.PdbStatus);
        Assert.AreEqual(Path.Combine(dir, "HelloWorld.pdb"), result.ManagedPdbPath);
    }

    /// <summary>Verifies a foreign PDB reports Mismatched but the dll is still offered.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_MismatchedPdb_OffersDllWithoutPdb()
    {
        var foreignPdb = Path.ChangeExtension(Samples.RichLibraryDll, ".pdb");
        TestSkip.When(!File.Exists(foreignPdb), "RichLibrary.pdb was not produced");

        var dir = NewTempDir();
        var exe = Path.Combine(dir, "HelloWorld.exe");
        WriteDummyBinary(exe);
        CopyInto(Samples.HelloWorldDll, Path.Combine(dir, "HelloWorld.dll"));
        CopyInto(foreignPdb, Path.Combine(dir, "HelloWorld.pdb"));

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.HasAttachableCompanion);
        Assert.AreEqual(PreIlcPdbStatus.Mismatched, result.PdbStatus);
    }

    /// <summary>Verifies an assembly with an embedded portable PDB reports Embedded, not Missing.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_EmbeddedPdb_ReportsEmbedded()
    {
        var dir = NewTempDir();
        var exe = Path.Combine(dir, "EmbeddedSourceLib.exe");
        WriteDummyBinary(exe);
        CopyInto(Samples.EmbeddedSourceLibDll, Path.Combine(dir, "EmbeddedSourceLib.dll"));

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.AreEqual(PreIlcPdbStatus.Embedded, result!.PdbStatus);
    }

    /// <summary>Verifies an absent PDB reports Missing.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_AbsentPdb_ReportsMissing()
    {
        var dir = NewTempDir();
        var exe = Path.Combine(dir, "HelloWorld.exe");
        WriteDummyBinary(exe);
        CopyInto(Samples.HelloWorldDll, Path.Combine(dir, "HelloWorld.dll"));

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.AreEqual(PreIlcPdbStatus.Missing, result!.PdbStatus);
    }

    /// <summary>Verifies a binary outside any recognizable tree with no siblings yields null.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_OutsideAnyTree_ReturnsNull()
    {
        var dir = NewTempDir();
        var exe = Path.Combine(dir, "Standalone.exe");
        WriteDummyBinary(exe);

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNull(result);
    }

    /// <summary>Verifies mstat/DGML-only discoveries produce a result that is not attachable.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_MstatOnlyInObjNative_NotAttachable()
    {
        var root = NewTempDir();
        var exe = CreateClassicTree(root, "HelloWorld", out var objDir);
        var nativeDir = Path.Combine(objDir, "native");
        Directory.CreateDirectory(nativeDir);
        WriteDummyBinary(Path.Combine(nativeDir, "HelloWorld.mstat"));
        WriteDummyBinary(Path.Combine(nativeDir, "HelloWorld.codegen.dgml.xml"));

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.IsFalse(result!.HasAttachableCompanion);
        Assert.AreEqual(PreIlcAssemblyOrigin.None, result.Origin);
        Assert.AreEqual(Path.Combine(nativeDir, "HelloWorld.mstat"), result.MstatPath);
        Assert.AreEqual(Path.Combine(nativeDir, "HelloWorld.codegen.dgml.xml"), result.CodegenDgmlPath);
        Assert.IsNull(result.ScanDgmlPath);
    }

    /// <summary>Verifies a managed input newer than the binary is noted but never blocking.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_StaleManagedInput_NotesStaleness()
    {
        var dir = NewTempDir();
        var exe = Path.Combine(dir, "HelloWorld.exe");
        WriteDummyBinary(exe);
        File.SetLastWriteTimeUtc(exe, DateTime.UtcNow.AddHours(-2));
        var dll = Path.Combine(dir, "HelloWorld.dll");
        CopyInto(Samples.HelloWorldDll, dll);
        File.SetLastWriteTimeUtc(dll, DateTime.UtcNow);

        var result = PreIlcSidecarDetector.Find(exe);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.HasAttachableCompanion);
        Assert.Contains("newer", result.Details!, StringComparison.Ordinal);
    }

    /// <summary>Verifies the real fixture publish tree: rsp origin, matched PDB, and obj mstat/DGML.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_FixturePublishTree_FindsRspOriginWithMatchedPdb()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var result = PreIlcSidecarDetector.Find(Samples.NativeAotConsoleExe!);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.HasAttachableCompanion);
        Assert.AreEqual(PreIlcAssemblyOrigin.IlcResponseFile, result.Origin);
        Assert.EndsWith("NativeAotConsole.dll", result.ManagedAssemblyPath!, StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(File.Exists(result.ManagedAssemblyPath));
        Assert.AreEqual(PreIlcPdbStatus.Matched, result.PdbStatus);
        Assert.IsNotNull(result.MstatPath);
        Assert.IsNotNull(result.CodegenDgmlPath);
        Assert.IsNotNull(result.IlcResponseFilePath);
        Assert.IsGreaterThan(0, result.PackageReferenceCount);
        Assert.IsEmpty(result.LocalReferencePaths);
        Assert.IsEmpty(result.UnresolvedReferencePaths);
    }

    /// <summary>
    /// Verifies the real artifacts-layout publish maps publish to obj with obj-only sidecars.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_ArtifactsFixture_FindsObjSidecarsWithNoSiblings()
    {
        TestSkip.When(Samples.NativeAotArtifactsExe is null, "artifacts NativeAOT sample was not built");

        var result = PreIlcSidecarDetector.Find(Samples.NativeAotArtifactsExe!);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.HasAttachableCompanion);
        var artifactsObj = TestProcessEnvironment.IsDevelopmentContainer
            ? Path.Combine("artifacts", "devcontainer", "obj")
            : Path.Combine("artifacts", "obj");
        Assert.Contains(artifactsObj, result.ManagedAssemblyPath!, StringComparison.OrdinalIgnoreCase);
        Assert.IsNotNull(result.MstatPath);
        Assert.IsNotNull(result.CodegenDgmlPath);

        var exeDir = Path.GetDirectoryName(Samples.NativeAotArtifactsExe!)!;
        Assert.IsFalse(File.Exists(Path.Combine(exeDir, "NativeAotArtifactsConsole.mstat")));
    }

    /// <summary>Verifies the real Native AOT library publish finds its companion via tree, not sibling.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Find_LibraryFixture_FindsCompanionViaTreeNotSibling()
    {
        TestSkip.When(Samples.NativeAotLibraryBinary is null, "NativeAOT library sample was not built");

        var result = PreIlcSidecarDetector.Find(Samples.NativeAotLibraryBinary!);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.HasAttachableCompanion);
        Assert.AreNotEqual(PreIlcAssemblyOrigin.SiblingAssembly, result.Origin);
        Assert.AreNotEqual(
            Path.GetFullPath(Samples.NativeAotLibraryBinary!),
            Path.GetFullPath(result.ManagedAssemblyPath!));
    }

    /// <summary>Disposes test resources created during the run.</summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var path in _tempFiles)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                else if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch { /* best effort */ }
        }
    }
}
