using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// Verifies that NuGet dependency resolution accepts real package layouts while containing every
/// path read from an untrusted <c>.deps.json</c> manifest.
/// </summary>
[TestClass]
public sealed class NuGetDepsJsonResolverTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies an SDK-generated manifest resolves a real package through the direct, simple-name,
    /// and identity-aware production facades.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryResolve_RealSdkManifest_ResolvesExpectedPackageThroughFacades()
    {
        var direct = Assert.IsExactInstanceOfType<ResolvedAssembly.FromFile>(
            NuGetDepsJsonResolver.TryResolve(Samples.RichLibraryDll, "Newtonsoft.Json"));
        using (var dependency = new AssemblyAnalyzer(direct.Path))
            Assert.AreEqual("Newtonsoft.Json", dependency.AssemblyName);

        var simpleName = Assert.IsExactInstanceOfType<ResolvedAssembly.FromFile>(
            AssemblyAnalyzer.ResolveAssembly(Samples.RichLibraryDll, "Newtonsoft.Json"));
        Assert.AreEqual(
            Path.GetFullPath(direct.Path),
            Path.GetFullPath(simpleName.Path),
            ignoreCase: OperatingSystem.IsWindows() || OperatingSystem.IsMacOS());

        using var source = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var identity = Assert.ContainsSingle(
            source.AssemblyRefs.Where(reference => reference.Name == "Newtonsoft.Json"));
        var identityResolution = AssemblyAnalyzer.ResolveAssemblyByIdentity(
            Samples.RichLibraryDll,
            identity);
        var identityFile = Assert.IsExactInstanceOfType<ResolvedAssembly.FromFile>(
            identityResolution.Resolved);

        Assert.AreEqual(AssemblyProvenance.NuGetPackageCache, identityResolution.Provenance);
        Assert.AreEqual(
            Path.GetFullPath(direct.Path),
            Path.GetFullPath(identityFile.Path),
            ignoreCase: OperatingSystem.IsWindows() || OperatingSystem.IsMacOS());
    }

    /// <summary>
    /// Verifies a traversal that would reach a real package assembly is rejected by every public
    /// resolution facade, not only by the internal root-injection seam.
    /// </summary>
    [TestMethod]
    public void ResolveAssembly_TraversingRealPackageAsset_FailsClosedThroughFacades()
    {
        using var source = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var identity = Assert.ContainsSingle(
            source.AssemblyRefs.Where(reference => reference.Name == "Newtonsoft.Json"));
        var sandbox = Directory.CreateTempSubdirectory("dotsider-deps-facades-").FullName;
        try
        {
            var referencePath = CreateReference(sandbox);
            WriteDepsJson(
                referencePath,
                "Newtonsoft.Json/13.0.4",
                "lib/net6.0/../net6.0/Newtonsoft.Json.dll",
                "newtonsoft.json/13.0.4");

            var direct = NuGetDepsJsonResolver.TryResolve(referencePath, "Newtonsoft.Json");
            var simpleName = AssemblyAnalyzer.ResolveAssembly(referencePath, "Newtonsoft.Json");
            var path = AssemblyAnalyzer.ResolveAssemblyPath(referencePath, "Newtonsoft.Json");
            var identityResolution = AssemblyAnalyzer.ResolveAssemblyByIdentity(
                referencePath,
                identity);

            Assert.IsNull(direct);
            Assert.IsNull(simpleName);
            Assert.IsNull(path);
            Assert.IsNull(identityResolution.Resolved);
            Assert.AreEqual(AssemblyProvenance.Unresolved, identityResolution.Provenance);
        }
        finally
        {
            DeleteDirectory(sandbox);
        }
    }

    /// <summary>
    /// Verifies safe portable separators and redundant current-directory segments resolve to the
    /// same contained package file on every operating system.
    /// </summary>
    /// <param name="packagePath">The manifest package path.</param>
    /// <param name="assetPath">The manifest runtime asset path.</param>
    [TestMethod]
    [DataRow("contoso.library/1.0.0", "lib/net10.0/RichLibrary.dll")]
    [DataRow(@"contoso.library\1.0.0", @"lib\net10.0\RichLibrary.dll")]
    [DataRow("contoso.library/./1.0.0", "lib//net10.0/./RichLibrary.dll")]
    public void TryResolve_SafePortablePaths_ResolvesContainedAssembly(
        string packagePath,
        string assetPath)
    {
        var sandbox = Directory.CreateTempSubdirectory("dotsider-deps-safe-").FullName;
        try
        {
            var packageRoot = Directory.CreateDirectory(Path.Combine(sandbox, "packages")).FullName;
            var expectedPath = CopyRichLibrary(
                packageRoot,
                "contoso.library",
                "1.0.0",
                "lib",
                "net10.0");
            var referencePath = CreateReference(sandbox);
            WriteDepsJson(
                referencePath,
                "Contoso.Library/1.0.0",
                assetPath,
                packagePath);

            var resolved = Assert.IsExactInstanceOfType<ResolvedAssembly.FromFile>(
                NuGetDepsJsonResolver.TryResolve(
                    referencePath,
                    "RichLibrary",
                    [packageRoot]));

            Assert.AreEqual(
                Path.GetFullPath(expectedPath),
                Path.GetFullPath(resolved.Path),
                ignoreCase: OperatingSystem.IsWindows() || OperatingSystem.IsMacOS());
        }
        finally
        {
            DeleteDirectory(sandbox);
        }
    }

    /// <summary>
    /// Verifies the normal library-key fallback still maps a package ID and version when the
    /// manifest omits its optional <c>path</c> property.
    /// </summary>
    [TestMethod]
    public void TryResolve_MissingLibraryPath_UsesNuGetLayoutFallback()
    {
        var sandbox = Directory.CreateTempSubdirectory("dotsider-deps-fallback-").FullName;
        try
        {
            var packageRoot = Directory.CreateDirectory(Path.Combine(sandbox, "packages")).FullName;
            var expectedPath = CopyRichLibrary(
                packageRoot,
                "contoso.library",
                "1.2.3",
                "lib",
                "net10.0");
            var referencePath = CreateReference(sandbox);
            WriteDepsJson(
                referencePath,
                "Contoso.Library/1.2.3",
                "lib/net10.0/RichLibrary.dll",
                packagePath: null);

            var resolved = Assert.IsExactInstanceOfType<ResolvedAssembly.FromFile>(
                NuGetDepsJsonResolver.TryResolve(
                    referencePath,
                    "RichLibrary",
                    [packageRoot]));

            Assert.AreEqual(
                Path.GetFullPath(expectedPath),
                Path.GetFullPath(resolved.Path),
                ignoreCase: OperatingSystem.IsWindows() || OperatingSystem.IsMacOS());
        }
        finally
        {
            DeleteDirectory(sandbox);
        }
    }

    /// <summary>
    /// Verifies a library path that climbs out of the global packages root cannot return a real
    /// managed assembly placed at the attacker-selected destination.
    /// </summary>
    [TestMethod]
    public void TryResolve_PackagePathTraversalToExistingAssembly_ReturnsNull()
    {
        var sandbox = Directory.CreateTempSubdirectory("dotsider-deps-package-traversal-").FullName;
        try
        {
            var packageRoot = Directory.CreateDirectory(
                Path.Combine(sandbox, "cache", "packages")).FullName;
            var outsidePackage = Directory.CreateDirectory(
                Path.Combine(sandbox, "outside-package")).FullName;
            File.Copy(
                Samples.RichLibraryDll,
                Path.Combine(outsidePackage, "RichLibrary.dll"));
            var referencePath = CreateReference(sandbox);
            var traversal = Path.GetRelativePath(packageRoot, outsidePackage)
                .Replace(Path.DirectorySeparatorChar, '/');
            WriteDepsJson(
                referencePath,
                "Hostile.Package/1.0.0",
                "RichLibrary.dll",
                traversal);

            var resolved = NuGetDepsJsonResolver.TryResolve(
                referencePath,
                "RichLibrary",
                [packageRoot]);

            Assert.IsNull(resolved);
        }
        finally
        {
            DeleteDirectory(sandbox);
        }
    }

    /// <summary>
    /// Verifies a runtime asset cannot traverse from its selected package into a sibling package,
    /// even though the resulting file remains inside the global packages root.
    /// </summary>
    [TestMethod]
    public void TryResolve_AssetTraversalToSiblingPackage_ReturnsNull()
    {
        var sandbox = Directory.CreateTempSubdirectory("dotsider-deps-asset-traversal-").FullName;
        try
        {
            var packageRoot = Directory.CreateDirectory(Path.Combine(sandbox, "packages")).FullName;
            var selectedPackage = Directory.CreateDirectory(
                Path.Combine(packageRoot, "selected", "1.0.0")).FullName;
            var siblingPackage = Directory.CreateDirectory(
                Path.Combine(packageRoot, "sibling", "1.0.0")).FullName;
            var outsidePath = Path.Combine(siblingPackage, "RichLibrary.dll");
            File.Copy(Samples.RichLibraryDll, outsidePath);
            var referencePath = CreateReference(sandbox);
            var traversal = Path.GetRelativePath(selectedPackage, outsidePath)
                .Replace(Path.DirectorySeparatorChar, '/');
            WriteDepsJson(
                referencePath,
                "Selected/1.0.0",
                traversal,
                "selected/1.0.0");

            var resolved = NuGetDepsJsonResolver.TryResolve(
                referencePath,
                "RichLibrary",
                [packageRoot]);

            Assert.IsNull(resolved);
        }
        finally
        {
            DeleteDirectory(sandbox);
        }
    }

    /// <summary>
    /// Verifies unsafe package path forms are rejected using portable path semantics.
    /// </summary>
    /// <param name="packagePath">The hostile package path.</param>
    [TestMethod]
    [DataRow("../outside")]
    [DataRow(@"..\outside")]
    [DataRow("safe/../../outside")]
    [DataRow("/outside")]
    [DataRow(@"\outside")]
    [DataRow(@"C:\outside")]
    [DataRow(@"C:outside")]
    [DataRow(@"\\server\share")]
    [DataRow(@"\\?\C:\outside")]
    [DataRow(@"\\.\C:\outside")]
    [DataRow(".")]
    [DataRow("NUL/1.0.0")]
    [DataRow("package./1.0.0")]
    public void TryResolve_UnsafePackagePath_ReturnsNull(string packagePath)
    {
        var sandbox = Directory.CreateTempSubdirectory("dotsider-deps-package-path-").FullName;
        try
        {
            var packageRoot = Directory.CreateDirectory(Path.Combine(sandbox, "packages")).FullName;
            var referencePath = CreateReference(sandbox);
            WriteDepsJson(
                referencePath,
                "Hostile.Package/1.0.0",
                "RichLibrary.dll",
                packagePath);

            var resolved = NuGetDepsJsonResolver.TryResolve(
                referencePath,
                "RichLibrary",
                [packageRoot]);

            Assert.IsNull(resolved);
        }
        finally
        {
            DeleteDirectory(sandbox);
        }
    }

    /// <summary>
    /// Verifies unsafe runtime asset path forms are rejected after portable filename matching.
    /// </summary>
    /// <param name="assetPath">The hostile runtime asset path.</param>
    [TestMethod]
    [DataRow("../outside/RichLibrary.dll")]
    [DataRow(@"..\outside\RichLibrary.dll")]
    [DataRow("lib/../../outside/RichLibrary.dll")]
    [DataRow("/outside/RichLibrary.dll")]
    [DataRow(@"\outside\RichLibrary.dll")]
    [DataRow(@"C:\outside\RichLibrary.dll")]
    [DataRow(@"C:outside\RichLibrary.dll")]
    [DataRow(@"\\server\share\RichLibrary.dll")]
    [DataRow(@"\\?\C:\outside\RichLibrary.dll")]
    [DataRow(@"\\.\C:\outside\RichLibrary.dll")]
    [DataRow("lib/RichLibrary.dll:stream")]
    [DataRow("lib/NUL/RichLibrary.dll")]
    [DataRow("lib/trailing./RichLibrary.dll")]
    public void TryResolve_UnsafeRuntimeAssetPath_ReturnsNull(string assetPath)
    {
        var sandbox = Directory.CreateTempSubdirectory("dotsider-deps-asset-path-").FullName;
        try
        {
            var packageRoot = Directory.CreateDirectory(Path.Combine(sandbox, "packages")).FullName;
            Directory.CreateDirectory(Path.Combine(packageRoot, "hostile.package", "1.0.0"));
            var referencePath = CreateReference(sandbox);
            WriteDepsJson(
                referencePath,
                "Hostile.Package/1.0.0",
                assetPath,
                "hostile.package/1.0.0");

            var resolved = NuGetDepsJsonResolver.TryResolve(
                referencePath,
                "RichLibrary",
                [packageRoot]);

            Assert.IsNull(resolved);
        }
        finally
        {
            DeleteDirectory(sandbox);
        }
    }

    /// <summary>
    /// Verifies a package-directory link cannot redirect resolution outside the global packages
    /// root.
    /// </summary>
    [TestMethod]
    public void TryResolve_PackageDirectoryLinkEscapesRoot_ReturnsNull()
    {
        var sandbox = Directory.CreateTempSubdirectory("dotsider-deps-package-link-").FullName;
        try
        {
            var packageRoot = Directory.CreateDirectory(Path.Combine(sandbox, "packages")).FullName;
            var outsidePackage = Directory.CreateDirectory(
                Path.Combine(sandbox, "outside-package")).FullName;
            File.Copy(
                Samples.RichLibraryDll,
                Path.Combine(outsidePackage, "RichLibrary.dll"));
            Directory.CreateSymbolicLink(
                Path.Combine(packageRoot, "linked-package"),
                outsidePackage);
            var referencePath = CreateReference(sandbox);
            WriteDepsJson(
                referencePath,
                "Linked.Package/1.0.0",
                "RichLibrary.dll",
                "linked-package");

            var resolved = NuGetDepsJsonResolver.TryResolve(
                referencePath,
                "RichLibrary",
                [packageRoot]);

            Assert.IsNull(resolved);
        }
        finally
        {
            DeleteDirectory(sandbox);
        }
    }

    /// <summary>
    /// Verifies a runtime asset link cannot redirect resolution outside its selected package.
    /// </summary>
    [TestMethod]
    public void TryResolve_RuntimeAssetLinkEscapesPackage_ReturnsNull()
    {
        var sandbox = Directory.CreateTempSubdirectory("dotsider-deps-asset-link-").FullName;
        try
        {
            var packageRoot = Directory.CreateDirectory(Path.Combine(sandbox, "packages")).FullName;
            var packageDirectory = Directory.CreateDirectory(
                Path.Combine(packageRoot, "linked.package", "1.0.0")).FullName;
            var outsideDirectory = Directory.CreateDirectory(
                Path.Combine(sandbox, "outside")).FullName;
            var outsidePath = Path.Combine(outsideDirectory, "RichLibrary.dll");
            File.Copy(Samples.RichLibraryDll, outsidePath);
            File.CreateSymbolicLink(
                Path.Combine(packageDirectory, "RichLibrary.dll"),
                outsidePath);
            var referencePath = CreateReference(sandbox);
            WriteDepsJson(
                referencePath,
                "Linked.Package/1.0.0",
                "RichLibrary.dll",
                "linked.package/1.0.0");

            var resolved = NuGetDepsJsonResolver.TryResolve(
                referencePath,
                "RichLibrary",
                [packageRoot]);

            Assert.IsNull(resolved);
        }
        finally
        {
            DeleteDirectory(sandbox);
        }
    }

    /// <summary>
    /// Verifies a trusted linked package root and a package-internal directory link resolve while
    /// their physical targets remain contained.
    /// </summary>
    [TestMethod]
    public void TryResolve_ContainedLinks_ResolvesAssembly()
    {
        var sandbox = Directory.CreateTempSubdirectory("dotsider-deps-contained-links-").FullName;
        try
        {
            var physicalRoot = Directory.CreateDirectory(
                Path.Combine(sandbox, "physical-packages")).FullName;
            var linkedRoot = Path.Combine(sandbox, "linked-packages");
            Directory.CreateSymbolicLink(linkedRoot, physicalRoot);
            var packageDirectory = Directory.CreateDirectory(
                Path.Combine(physicalRoot, "linked.package", "1.0.0")).FullName;
            var physicalLibrary = Directory.CreateDirectory(
                Path.Combine(packageDirectory, "physical-lib")).FullName;
            var expectedPath = Path.Combine(physicalLibrary, "RichLibrary.dll");
            File.Copy(Samples.RichLibraryDll, expectedPath);
            Directory.CreateSymbolicLink(
                Path.Combine(packageDirectory, "lib"),
                physicalLibrary);
            var referencePath = CreateReference(sandbox);
            WriteDepsJson(
                referencePath,
                "Linked.Package/1.0.0",
                "lib/RichLibrary.dll",
                "linked.package/1.0.0");

            var resolved = Assert.IsExactInstanceOfType<ResolvedAssembly.FromFile>(
                NuGetDepsJsonResolver.TryResolve(
                    referencePath,
                    "RichLibrary",
                    [linkedRoot]));

            Assert.AreEqual(
                Path.GetFullPath(Path.Combine(
                    linkedRoot,
                    "linked.package",
                    "1.0.0",
                    "lib",
                    "RichLibrary.dll")),
                Path.GetFullPath(resolved.Path),
                ignoreCase: OperatingSystem.IsWindows() || OperatingSystem.IsMacOS());
        }
        finally
        {
            DeleteDirectory(sandbox);
        }
    }

    /// <summary>
    /// Verifies a root-specific link escape is skipped so a later safe package root can satisfy
    /// the same manifest mapping.
    /// </summary>
    [TestMethod]
    public void TryResolve_FirstRootEscapesSecondRootContained_ResolvesSecondRoot()
    {
        var sandbox = Directory.CreateTempSubdirectory("dotsider-deps-root-order-").FullName;
        try
        {
            var firstRoot = Directory.CreateDirectory(Path.Combine(sandbox, "first")).FullName;
            var secondRoot = Directory.CreateDirectory(Path.Combine(sandbox, "second")).FullName;
            var outsidePackage = Directory.CreateDirectory(Path.Combine(sandbox, "outside")).FullName;
            Directory.CreateSymbolicLink(
                Path.Combine(firstRoot, "ordered.package"),
                outsidePackage);
            var expectedPath = CopyRichLibrary(
                secondRoot,
                "ordered.package",
                "1.0.0");
            var referencePath = CreateReference(sandbox);
            WriteDepsJson(
                referencePath,
                "Ordered.Package/1.0.0",
                "RichLibrary.dll",
                "ordered.package/1.0.0");

            var resolved = Assert.IsExactInstanceOfType<ResolvedAssembly.FromFile>(
                NuGetDepsJsonResolver.TryResolve(
                    referencePath,
                    "RichLibrary",
                    [firstRoot, secondRoot]));

            Assert.AreEqual(
                Path.GetFullPath(expectedPath),
                Path.GetFullPath(resolved.Path),
                ignoreCase: OperatingSystem.IsWindows() || OperatingSystem.IsMacOS());
        }
        finally
        {
            DeleteDirectory(sandbox);
        }
    }

    /// <summary>
    /// Verifies broken and cyclic file links fail closed without surfacing filesystem exceptions.
    /// </summary>
    [TestMethod]
    public void TryResolve_BrokenAndCyclicAssetLinks_ReturnNull()
    {
        var sandbox = Directory.CreateTempSubdirectory("dotsider-deps-broken-links-").FullName;
        try
        {
            var packageRoot = Directory.CreateDirectory(Path.Combine(sandbox, "packages")).FullName;
            var packageDirectory = Directory.CreateDirectory(
                Path.Combine(packageRoot, "broken.package", "1.0.0")).FullName;
            File.CreateSymbolicLink(
                Path.Combine(packageDirectory, "Broken.dll"),
                Path.Combine(packageDirectory, "missing.dll"));
            File.CreateSymbolicLink(
                Path.Combine(packageDirectory, "CycleA.dll"),
                Path.Combine(packageDirectory, "CycleB.dll"));
            File.CreateSymbolicLink(
                Path.Combine(packageDirectory, "CycleB.dll"),
                Path.Combine(packageDirectory, "CycleA.dll"));
            var referencePath = CreateReference(sandbox);

            WriteDepsJson(
                referencePath,
                "Broken.Package/1.0.0",
                "Broken.dll",
                "broken.package/1.0.0");
            var broken = NuGetDepsJsonResolver.TryResolve(
                referencePath,
                "Broken",
                [packageRoot]);

            WriteDepsJson(
                referencePath,
                "Broken.Package/1.0.0",
                "CycleA.dll",
                "broken.package/1.0.0");
            var cyclic = NuGetDepsJsonResolver.TryResolve(
                referencePath,
                "CycleA",
                [packageRoot]);

            Assert.IsNull(broken);
            Assert.IsNull(cyclic);
        }
        finally
        {
            DeleteDirectory(sandbox);
        }
    }

    /// <summary>
    /// Verifies malformed JSON and non-string library types degrade to an unresolved dependency.
    /// </summary>
    [TestMethod]
    public void TryResolve_MalformedManifest_ReturnsNull()
    {
        var sandbox = Directory.CreateTempSubdirectory("dotsider-deps-malformed-").FullName;
        try
        {
            var packageRoot = Directory.CreateDirectory(Path.Combine(sandbox, "packages")).FullName;
            var referencePath = CreateReference(sandbox);
            var depsJsonPath = Path.ChangeExtension(referencePath, ".deps.json");
            File.WriteAllText(depsJsonPath, "{");

            var invalidJson = NuGetDepsJsonResolver.TryResolve(
                referencePath,
                "RichLibrary",
                [packageRoot]);

            File.WriteAllText(
                depsJsonPath,
                """
                {
                  "targets": {
                    ".NETCoreApp,Version=v10.0": {
                      "Malformed.Package/1.0.0": {
                        "runtime": { "RichLibrary.dll": {} }
                      }
                    }
                  },
                  "libraries": {
                    "Malformed.Package/1.0.0": {
                      "type": {},
                      "path": "malformed.package/1.0.0"
                    }
                  }
                }
                """);
            var invalidType = NuGetDepsJsonResolver.TryResolve(
                referencePath,
                "RichLibrary",
                [packageRoot]);

            Assert.IsNull(invalidJson);
            Assert.IsNull(invalidType);
        }
        finally
        {
            DeleteDirectory(sandbox);
        }
    }

    private static string CopyRichLibrary(string packageRoot, params string[] segments)
    {
        var directory = Directory.CreateDirectory(
            Path.Combine([packageRoot, .. segments])).FullName;
        var targetPath = Path.Combine(directory, "RichLibrary.dll");
        File.Copy(Samples.RichLibraryDll, targetPath);
        return targetPath;
    }

    private static string CreateReference(string sandbox)
    {
        var directory = Directory.CreateDirectory(Path.Combine(sandbox, "app")).FullName;
        var referencePath = Path.Combine(directory, "Host.dll");
        File.WriteAllBytes(referencePath, []);
        return referencePath;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private static void WriteDepsJson(
        string referencePath,
        string libraryKey,
        string assetPath,
        string? packagePath)
    {
        var library = new Dictionary<string, object?>
        {
            ["type"] = "package"
        };
        if (packagePath is not null)
            library["path"] = packagePath;

        var document = new Dictionary<string, object?>
        {
            ["targets"] = new Dictionary<string, object?>
            {
                [".NETCoreApp,Version=v10.0"] = new Dictionary<string, object?>
                {
                    [libraryKey] = new Dictionary<string, object?>
                    {
                        ["runtime"] = new Dictionary<string, object?>
                        {
                            [assetPath] = new Dictionary<string, object?>()
                        }
                    }
                }
            },
            ["libraries"] = new Dictionary<string, object?>
            {
                [libraryKey] = library
            }
        };

        File.WriteAllText(
            Path.ChangeExtension(referencePath, ".deps.json"),
            JsonSerializer.Serialize(document));
    }
}
