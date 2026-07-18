using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.IO.Compression;
using System.Xml;

namespace Dotsider.Tests;

/// <summary>
/// Security tests for opening files from NuGet package archives.
/// </summary>
/// <param name="testContext">The current test context.</param>
[TestClass]
public sealed class NuGetPackageAnalyzerSecurityTests(TestContext testContext)
{
    private const string ValidManifest =
        "<package><metadata><id>SecurityTests</id><version>1.0.0</version>" +
        "<authors>Dotsider.Tests</authors><description>Security regression package</description>" +
        "</metadata></package>";

    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private readonly TestContext _testContext = testContext;

    /// <summary>
    /// Verifies traversal cannot overwrite an existing file or create a directory outside the
    /// private extraction root.
    /// </summary>
    [TestMethod]
    public void OpenDll_ParentTraversal_DoesNotWriteOutsideExtractionRoot()
    {
        var existingDirectoryName = "dotsider-nupkg-existing-" + Guid.NewGuid().ToString("N");
        var absentDirectoryName = "dotsider-nupkg-absent-" + Guid.NewGuid().ToString("N");
        var existingDirectory = Path.Combine(Path.GetTempPath(), existingDirectoryName);
        var absentDirectory = Path.Combine(Path.GetTempPath(), absentDirectoryName);
        var outsideFile = Path.Combine(existingDirectory, "RichLibrary.dll");
        byte[] sentinel = [0x21, 0x09, 0x20, 0x99];
        Directory.CreateDirectory(existingDirectory);
        File.WriteAllBytes(outsideFile, sentinel);

        var packagePath = CreatePackage(
            ($"../{existingDirectoryName}/RichLibrary.dll", ReadSampleAssembly()),
            ($"../{absentDirectoryName}/RichLibrary.dll", ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            Assert.HasCount(2, package.DllFiles);

            TestAssert.All(
                package.DllFiles,
                entry => Assert.ThrowsExactly<UnsafePackageEntryException>(() => package.OpenDll(entry)));

            Assert.AreSequenceEqual(sentinel, File.ReadAllBytes(outsideFile));
            Assert.IsFalse(Directory.Exists(absentDirectory));
        }
        finally
        {
            DeletePackage(packagePath);
            DeleteDirectory(existingDirectory);
            DeleteDirectory(absentDirectory);
        }
    }

    /// <summary>
    /// Verifies portable traversal separators are rejected on every operating system.
    /// </summary>
    /// <param name="entryName">The hostile archive entry name.</param>
    [TestMethod]
    [DataRow(@"..\outside\RichLibrary.dll")]
    [DataRow(@"lib\..\..//outside/RichLibrary.dll")]
    [DataRow(@"lib/../..\outside/RichLibrary.dll")]
    [DataRow("lib/../RichLibrary.dll")]
    public void OpenDll_PortableTraversal_ThrowsUnsafePackageEntryException(string entryName)
    {
        var packagePath = CreatePackage((entryName, ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            var entry = Assert.ContainsSingle(package.DllFiles);

            var exception = Assert.ThrowsExactly<UnsafePackageEntryException>(() =>
                package.OpenDll(entry));
            Assert.AreEqual(
                "The package entry cannot be extracted because its path is unsafe or ambiguous.",
                exception.Message);
            Assert.DoesNotContain(entry.FullPath, exception.Message);
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies rooted paths in the host operating system's syntax are rejected without writing
    /// to the requested destination.
    /// </summary>
    [TestMethod]
    public void OpenDll_HostRootedPath_DoesNotWriteToRequestedDestination()
    {
        var outsideDirectory = Path.Combine(
            Path.GetTempPath(),
            "dotsider-nupkg-rooted-" + Guid.NewGuid().ToString("N"));
        var outsideFile = Path.Combine(outsideDirectory, "RichLibrary.dll");
        var packagePath = CreatePackage((outsideFile, ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            var entry = Assert.ContainsSingle(package.DllFiles);

            Assert.ThrowsExactly<UnsafePackageEntryException>(() => package.OpenDll(entry));
            Assert.IsFalse(Directory.Exists(outsideDirectory));
            Assert.IsFalse(File.Exists(outsideFile));
        }
        finally
        {
            DeletePackage(packagePath);
            DeleteDirectory(outsideDirectory);
        }
    }

    /// <summary>
    /// Verifies Windows drive, UNC, and device path syntax is rejected without consulting the
    /// filesystem, including on non-Windows hosts.
    /// </summary>
    /// <param name="entryName">The hostile archive entry name.</param>
    [TestMethod]
    [DataRow("/RichLibrary.dll")]
    [DataRow(@"\RichLibrary.dll")]
    [DataRow(@"C:\outside\RichLibrary.dll")]
    [DataRow(@"C:outside\RichLibrary.dll")]
    [DataRow(@"\\server\share\RichLibrary.dll")]
    [DataRow(@"\\?\C:\outside\RichLibrary.dll")]
    [DataRow(@"\\.\C:\outside\RichLibrary.dll")]
    public void OpenDll_PortableRootedPath_ThrowsUnsafePackageEntryException(string entryName)
    {
        var packagePath = CreatePackage((entryName, ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            var entry = Assert.ContainsSingle(package.DllFiles);

            Assert.ThrowsExactly<UnsafePackageEntryException>(() => package.OpenDll(entry));
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies malformed portable file names are rejected before the extraction root is created.
    /// </summary>
    /// <param name="entryName">The malformed archive entry name.</param>
    [TestMethod]
    [DataRow("lib/base.dll:payload.dll")]
    [DataRow("lib/evil?.dll")]
    [DataRow("lib/evil*.dll")]
    [DataRow("lib/evil|.dll")]
    [DataRow("lib/evil<.dll")]
    [DataRow("lib/evil>.dll")]
    [DataRow("lib/evil\".dll")]
    [DataRow("lib/control\u0001.dll")]
    [DataRow("lib/trailing /RichLibrary.dll")]
    [DataRow("lib/trailing./RichLibrary.dll")]
    [DataRow("lib/NUL.dll")]
    [DataRow("lib/COM1.dll")]
    [DataRow("lib/COM¹.dll")]
    [DataRow("lib/LPT².dll")]
    [DataRow("lib/COM³.dll")]
    public void OpenDll_MalformedPortableFileName_ThrowsBeforeExtraction(string entryName)
    {
        var packagePath = CreatePackage((entryName, ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            var entry = Assert.ContainsSingle(package.DllFiles);
            Assert.IsNull(package.ExtractionDirectory);

            Assert.ThrowsExactly<UnsafePackageEntryException>(() => package.OpenDll(entry));

            Assert.IsNull(package.ExtractionDirectory);
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies an alternate-stream entry is rejected whether it is activated before or after its
    /// ordinary base-file entry.
    /// </summary>
    [TestMethod]
    public void OpenDll_AlternateStreamAndBaseEntry_HasOrderIndependentOutcome()
    {
        var packagePath = CreatePackage(
            ("lib/base.dll", ReadSampleAssembly()),
            ("lib/base.dll:payload.dll", ReadSampleAssembly()));

        try
        {
            using (var package = new NuGetPackageAnalyzer(packagePath))
            {
                var baseEntry = Assert.ContainsSingle(
                    package.DllFiles.Where(entry => entry.FullPath == "lib/base.dll"));
                var streamEntry = Assert.ContainsSingle(
                    package.DllFiles.Where(entry => entry.FullPath != "lib/base.dll"));

                Assert.ThrowsExactly<UnsafePackageEntryException>(
                    () => package.OpenDll(streamEntry));
                Assert.IsNull(package.ExtractionDirectory);
                using var analyzer = package.OpenDll(baseEntry);
                Assert.AreEqual("RichLibrary", analyzer.AssemblyName);
            }

            using (var package = new NuGetPackageAnalyzer(packagePath))
            {
                var baseEntry = Assert.ContainsSingle(
                    package.DllFiles.Where(entry => entry.FullPath == "lib/base.dll"));
                var streamEntry = Assert.ContainsSingle(
                    package.DllFiles.Where(entry => entry.FullPath != "lib/base.dll"));

                using var analyzer = package.OpenDll(baseEntry);
                Assert.AreEqual("RichLibrary", analyzer.AssemblyName);
                Assert.ThrowsExactly<UnsafePackageEntryException>(
                    () => package.OpenDll(streamEntry));
            }
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies the containment check does not confuse the extraction root with a sibling whose
    /// name begins with the root name, and rejects the root itself as a file destination.
    /// </summary>
    [TestMethod]
    public void ContainedPathResolver_RootAndPrefixSibling_ReturnsFalse()
    {
        var root = Directory.CreateTempSubdirectory("dotsider-containment-root-").FullName;
        var rootName = Path.GetFileName(root);
        var child = Path.Combine(root, "lib", "RichLibrary.dll");
        var parent = Path.GetDirectoryName(root);
        Assert.IsNotNull(parent);
        var sibling = Path.Combine(
            parent,
            rootName + "bell",
            "RichLibrary.dll");

        try
        {
            Assert.IsFalse(ContainedPathResolver.TryResolve(root, ".", out _));
            Assert.IsTrue(ContainedPathResolver.IsStrictDescendant(root, child));
            Assert.IsFalse(ContainedPathResolver.IsStrictDescendant(root, root));
            Assert.IsFalse(ContainedPathResolver.IsStrictDescendant(root, sibling));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    /// <summary>
    /// Verifies safe portable archive paths extract and open as real managed assemblies.
    /// </summary>
    /// <param name="entryName">The safe archive entry name.</param>
    [TestMethod]
    [DataRow("lib/net10.0/RichLibrary.dll")]
    [DataRow("lib/./net10.0/RichLibrary.dll")]
    [DataRow("lib//net10.0//RichLibrary.dll")]
    [DataRow(@"lib\net10.0\RichLibrary.dll")]
    [DataRow("lib/ünicode/RichLibrary.dll")]
    [DataRow("lib/..name/RichLibrary.dll")]
    [DataRow("lib/version.2.5.1/RichLibrary.dll")]
    public void OpenDll_SafePortablePath_OpensRealAssembly(string entryName)
    {
        var packagePath = CreatePackage((entryName, ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            var entry = Assert.ContainsSingle(package.DllFiles);
            using var analyzer = package.OpenDll(entry);

            Assert.IsTrue(analyzer.HasMetadata);
            Assert.AreEqual("RichLibrary", analyzer.AssemblyName);
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies a value-equal copy of a package entry is rejected because it is not the exact
    /// object returned by the analyzer.
    /// </summary>
    [TestMethod]
    public void OpenDll_ValueEqualEntryCopy_ThrowsArgumentException()
    {
        var packagePath = CreatePackage(("lib/RichLibrary.dll", ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            var entry = Assert.ContainsSingle(package.DllFiles);
            var copy = entry with { };
            Assert.AreEqual(entry, copy);

            Assert.ThrowsExactly<ArgumentException>(() => package.OpenDll(copy));
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies a caller-constructed entry is rejected before its hostile path can create a
    /// directory outside the extraction root.
    /// </summary>
    [TestMethod]
    public void OpenDll_ForgedEntry_ThrowsBeforeFilesystemAccess()
    {
        var outsideDirectoryName = "dotsider-nupkg-forged-" + Guid.NewGuid().ToString("N");
        var outsideDirectory = Path.Combine(Path.GetTempPath(), outsideDirectoryName);
        var packagePath = CreatePackage(("lib/RichLibrary.dll", ReadSampleAssembly()));
        var forged = new NuGetFileEntry(
            "RichLibrary.dll",
            $"../{outsideDirectoryName}/RichLibrary.dll",
            "..",
            1,
            1,
            true);

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            Assert.IsNull(package.ExtractionDirectory);

            Assert.ThrowsExactly<ArgumentException>(() => package.OpenDll(forged));
            Assert.IsFalse(Directory.Exists(outsideDirectory));
            Assert.IsNull(package.ExtractionDirectory);
        }
        finally
        {
            DeletePackage(packagePath);
            DeleteDirectory(outsideDirectory);
        }
    }

    /// <summary>
    /// Verifies an entry returned by a different analyzer instance is rejected.
    /// </summary>
    [TestMethod]
    public void OpenDll_EntryFromAnotherAnalyzer_ThrowsArgumentException()
    {
        var firstPackagePath = CreatePackage(("lib/RichLibrary.dll", ReadSampleAssembly()));
        var secondPackagePath = CreatePackage(("lib/RichLibrary.dll", ReadSampleAssembly()));

        try
        {
            using var firstPackage = new NuGetPackageAnalyzer(firstPackagePath);
            using var secondPackage = new NuGetPackageAnalyzer(secondPackagePath);
            var foreignEntry = Assert.ContainsSingle(secondPackage.DllFiles);

            Assert.ThrowsExactly<ArgumentException>(() => firstPackage.OpenDll(foreignEntry));
        }
        finally
        {
            DeletePackage(firstPackagePath);
            DeletePackage(secondPackagePath);
        }
    }

    /// <summary>
    /// Verifies an exact package-owned entry that is not a DLL cannot be passed to OpenDll.
    /// </summary>
    [TestMethod]
    public void OpenDll_PackageOwnedNonDllEntry_ThrowsArgumentException()
    {
        var packagePath = CreatePackage(("content/readme.txt", "content"u8.ToArray()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            var entry = Assert.ContainsSingle(package.Files.Where(entry => entry.Name == "readme.txt"));

            Assert.ThrowsExactly<ArgumentException>(() => package.OpenDll(entry));
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies a null entry is rejected with the public contract's specific exception.
    /// </summary>
    [TestMethod]
    public void OpenDll_NullEntry_ThrowsArgumentNullException()
    {
        var packagePath = CreatePackage(("lib/RichLibrary.dll", ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);

            Assert.ThrowsExactly<ArgumentNullException>(() => package.OpenDll(null!));
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies listing a package and rejecting an unsafe owned entry do not create an extraction
    /// directory.
    /// </summary>
    [TestMethod]
    public void PackageListingAndUnsafeEntry_DoNotCreateExtractionDirectory()
    {
        var packagePath = CreatePackage(
            ("../outside/RichLibrary.dll", ReadSampleAssembly()),
            ("lib/RichLibrary.dll", ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            Assert.HasCount(2, package.DllFiles);
            Assert.IsNull(package.ExtractionDirectory);
            var unsafeEntry = Assert.ContainsSingle(
                package.DllFiles.Where(entry => entry.FullPath.StartsWith("..", StringComparison.Ordinal)));

            Assert.ThrowsExactly<UnsafePackageEntryException>(() => package.OpenDll(unsafeEntry));

            Assert.IsNull(package.ExtractionDirectory);
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies duplicate archive names are all rejected rather than ambiguously selecting one
    /// payload.
    /// </summary>
    [TestMethod]
    public void OpenDll_DuplicateExactEntryNames_ThrowsForEveryEntry()
    {
        var packagePath = CreatePackage(
            ("lib/RichLibrary.dll", ReadSampleAssembly()),
            ("lib/RichLibrary.dll", ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            Assert.HasCount(2, package.DllFiles);

            TestAssert.All(
                package.DllFiles,
                entry => Assert.ThrowsExactly<UnsafePackageEntryException>(() => package.OpenDll(entry)));
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies different archive names that canonicalize to one destination are all rejected.
    /// </summary>
    [TestMethod]
    public void OpenDll_CanonicalDestinationAliases_ThrowsForEveryEntry()
    {
        var packagePath = CreatePackage(
            ("lib/RichLibrary.dll", ReadSampleAssembly()),
            ("lib/./RichLibrary.dll", ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            Assert.HasCount(2, package.DllFiles);

            TestAssert.All(
                package.DllFiles,
                entry => Assert.ThrowsExactly<UnsafePackageEntryException>(() => package.OpenDll(entry)));
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies a destination that is also another DLL's parent directory makes both entries
    /// unsafe, independent of activation order.
    /// </summary>
    [TestMethod]
    public void OpenDll_FileDirectoryTopologyConflict_ThrowsForEveryEntryInEitherOrder()
    {
        var packagePath = CreatePackage(
            ("lib/A.dll", ReadSampleAssembly()),
            ("lib/A.dll/B.dll", ReadSampleAssembly()));

        try
        {
            foreach (var reverse in new[] { false, true })
            {
                using var package = new NuGetPackageAnalyzer(packagePath);
                NuGetFileEntry[] entries = reverse
                    ? [.. package.DllFiles.Reverse()]
                    : [.. package.DllFiles];

                TestAssert.All(
                    entries,
                    entry => Assert.ThrowsExactly<UnsafePackageEntryException>(
                        () => package.OpenDll(entry)));

                Assert.IsNotNull(package.ExtractionDirectory);
                Assert.IsFalse(Directory.Exists(Path.Combine(package.ExtractionDirectory, "lib")));
            }
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies a non-descendant prefix sibling cannot hide a later file-versus-directory
    /// conflict during destination planning.
    /// </summary>
    [TestMethod]
    public void OpenDll_TopologyConflictSeparatedByPrefixSibling_RejectsOnlyConflictPair()
    {
        var packagePath = CreatePackage(
            ("lib/A.dll", ReadSampleAssembly()),
            ("lib/A.dll-other.dll", ReadSampleAssembly()),
            ("lib/A.dll/B.dll", ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            var parent = package.DllFiles.Single(static entry => entry.FullPath == "lib/A.dll");
            var sibling = package.DllFiles.Single(static entry =>
                entry.FullPath == "lib/A.dll-other.dll");
            var child = package.DllFiles.Single(static entry => entry.FullPath == "lib/A.dll/B.dll");

            Assert.ThrowsExactly<UnsafePackageEntryException>(() => package.OpenDll(parent));
            Assert.ThrowsExactly<UnsafePackageEntryException>(() => package.OpenDll(child));
            using var analyzer = package.OpenDll(sibling);
            Assert.AreEqual("RichLibrary", analyzer.AssemblyName);
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies planning a deeply segmented path uses allocation proportional to the archive
    /// path instead of repeatedly allocating every progressively shorter parent path.
    /// </summary>
    [TestMethod]
    public void OpenDll_DeepSegmentedPath_PlanningAllocationIsLinear()
    {
        const long AllocationLimit = 64L * 1024 * 1024;
        var deepPath = string.Concat(Enumerable.Repeat("a/", 8_000)) + "Deep.dll";
        var packagePath = CreatePackage(
            (deepPath, [0x01]),
            ("lib/RichLibrary.dll", ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            var entry = package.DllFiles.Single(static entry =>
                entry.FullPath == "lib/RichLibrary.dll");
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            using var analyzer = package.OpenDll(entry);

            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.AreEqual("RichLibrary", analyzer.AssemblyName);
            Assert.IsLessThan(AllocationLimit, allocated);
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies a large duplicate group is classified once per entry without repeatedly feeding
    /// the growing alias group through topology analysis.
    /// </summary>
    [TestMethod]
    public void OpenDll_ManyDuplicateDestinations_RejectsEveryEntryWithBoundedAllocation()
    {
        const int DuplicateCount = 4_096;
        const long AllocationLimit = 64L * 1024 * 1024;
        byte[] content = [0x01];
        var entries = Enumerable.Range(0, DuplicateCount)
            .Select(_ => (EntryName: "lib/Duplicate.dll", Content: content))
            .ToArray();
        var packagePath = CreatePackage(entries);

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            Assert.HasCount(DuplicateCount, package.DllFiles);
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            Assert.ThrowsExactly<UnsafePackageEntryException>(() =>
                package.OpenDll(package.DllFiles[0]));

            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.IsLessThan(AllocationLimit, allocated);
            Assert.AreEqual(1, package.TopologyDestinationCount);
            TestAssert.All(
                package.DllFiles,
                entry => Assert.ThrowsExactly<UnsafePackageEntryException>(() =>
                    package.OpenDll(entry)));
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies destination aliases that differ only by case are rejected on case-insensitive
    /// platforms.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows | OperatingSystems.OSX)]
    public void OpenDll_CaseInsensitivePlatformAliases_ThrowsForEveryEntry()
    {
        var packagePath = CreatePackage(
            ("lib/RichLibrary.dll", ReadSampleAssembly()),
            ("LIB/RICHLIBRARY.DLL", ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            Assert.HasCount(2, package.DllFiles);

            TestAssert.All(
                package.DllFiles,
                entry => Assert.ThrowsExactly<UnsafePackageEntryException>(() => package.OpenDll(entry)));
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies canonically equivalent Unicode names are rejected on macOS filesystems.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.OSX)]
    public void OpenDll_MacUnicodeNormalizationAliases_ThrowsForEveryEntry()
    {
        var packagePath = CreatePackage(
            ("lib/caf\u00E9.dll", ReadSampleAssembly()),
            ("lib/cafe\u0301.dll", ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            Assert.HasCount(2, package.DllFiles);

            TestAssert.All(
                package.DllFiles,
                entry => Assert.ThrowsExactly<UnsafePackageEntryException>(() => package.OpenDll(entry)));
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies case-distinct archive paths remain distinct on Linux filesystems.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void OpenDll_LinuxCaseDistinctEntries_OpenIndependently()
    {
        var packagePath = CreatePackage(
            ("lib/RichLibrary.dll", ReadSampleAssembly()),
            ("LIB/RICHLIBRARY.DLL", ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            using var first = package.OpenDll(package.DllFiles[0]);
            using var second = package.OpenDll(package.DllFiles[1]);

            Assert.AreNotEqual(first.FilePath, second.FilePath);
            Assert.AreEqual("RichLibrary", first.AssemblyName);
            Assert.AreEqual("RichLibrary", second.AssemblyName);
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies repeatedly opening one entry reuses the successful immutable extraction.
    /// </summary>
    [TestMethod]
    public void OpenDll_RepeatedOpen_ReusesExtractedFile()
    {
        var packagePath = CreatePackage(("lib/RichLibrary.dll", ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            var entry = Assert.ContainsSingle(package.DllFiles);
            string firstPath;
            using (var first = package.OpenDll(entry))
            {
                firstPath = first.FilePath;
            }

            using var second = package.OpenDll(entry);
            Assert.AreEqual(firstPath, second.FilePath);
            Assert.AreEqual("RichLibrary", second.AssemblyName);
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies a failed analyzer construction removes its extracted file so the same failure is
    /// reproduced on a second attempt instead of becoming a create-new collision.
    /// </summary>
    [TestMethod]
    public void OpenDll_InvalidAssemblyTwice_ThrowsBadImageFormatBothTimes()
    {
        var packagePath = CreatePackage(("lib/invalid.dll", [0x01, 0x02, 0x03, 0x04]));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            var entry = Assert.ContainsSingle(package.DllFiles);

            Assert.ThrowsExactly<BadImageFormatException>(() => package.OpenDll(entry));
            Assert.IsNotNull(package.ExtractionDirectory);
            var extractedPath = Path.Combine(package.ExtractionDirectory, "lib", "invalid.dll");
            Assert.IsFalse(File.Exists(extractedPath));

            Assert.ThrowsExactly<BadImageFormatException>(() => package.OpenDll(entry));
            Assert.IsFalse(File.Exists(extractedPath));
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies concurrent opens of one entry share the single successful contained extraction.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task OpenDll_ConcurrentSameEntry_AllOpenSuccessfully()
    {
        const int Concurrency = 8;
        var cancellationToken = _testContext.CancellationToken;
        var packagePath = CreatePackage(
            ("lib/RichLibrary.dll", CreatePaddedAssembly(1024 * 1024)));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            var entry = Assert.ContainsSingle(package.DllFiles);
            using var barrier = new Barrier(Concurrency);
            var tasks = Enumerable.Range(0, Concurrency)
                .Select(_ => Task.Factory.StartNew(
                    () =>
                    {
                        if (!barrier.SignalAndWait(TimeSpan.FromSeconds(10), cancellationToken))
                            throw new TimeoutException("Concurrent open barrier was not reached.");

                        using var analyzer = package.OpenDll(entry);
                        Assert.AreEqual("RichLibrary", analyzer.AssemblyName);
                        return analyzer.FilePath;
                    },
                    cancellationToken,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default))
                .ToArray();

            var paths = await Task.WhenAll(tasks).WaitAsync(cancellationToken);
            var extractedPaths = paths.Distinct(StringComparer.Ordinal).ToArray();
            Assert.HasCount(1, extractedPaths);
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies racing an open with disposal produces only a complete analyzer or the documented
    /// disposed outcome, never an archive or partial-copy failure.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task OpenDll_RacingDispose_ProducesOnlySerializedOutcomes()
    {
        var cancellationToken = _testContext.CancellationToken;
        var packagePath = CreatePackage(
            ("lib/RichLibrary.dll", CreatePaddedAssembly(4 * 1024 * 1024)));

        try
        {
            for (var iteration = 0; iteration < 12; iteration++)
            {
                var package = new NuGetPackageAnalyzer(packagePath);
                var entry = Assert.ContainsSingle(package.DllFiles);
                using var barrier = new Barrier(2);

                try
                {
                    var openTask = Task.Factory.StartNew(
                        () =>
                        {
                            barrier.SignalAndWait(cancellationToken);
                            try
                            {
                                using var analyzer = package.OpenDll(entry);
                                Assert.AreEqual("RichLibrary", analyzer.AssemblyName);
                            }
                            catch (ObjectDisposedException)
                            {
                            }
                        },
                        cancellationToken,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default);
                    var disposeTask = Task.Factory.StartNew(
                        () =>
                        {
                            barrier.SignalAndWait(cancellationToken);
                            package.Dispose();
                        },
                        cancellationToken,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default);

                    await Task.WhenAll(openTask, disposeTask).WaitAsync(cancellationToken);
                }
                finally
                {
                    package.Dispose();

                    if (package.ExtractionDirectory is { } extractionDirectory)
                        DeleteDirectory(extractionDirectory);
                }
            }
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies distinct safe archive paths retain distinct extraction destinations.
    /// </summary>
    [TestMethod]
    public void OpenDll_DistinctSafePaths_UseDistinctExtractedFiles()
    {
        var packagePath = CreatePackage(
            ("lib/net9.0/RichLibrary.dll", ReadSampleAssembly()),
            ("lib/net10.0/RichLibrary.dll", ReadSampleAssembly()));

        try
        {
            using var package = new NuGetPackageAnalyzer(packagePath);
            Assert.HasCount(2, package.DllFiles);
            using var first = package.OpenDll(package.DllFiles[0]);
            using var second = package.OpenDll(package.DllFiles[1]);

            Assert.AreNotEqual(first.FilePath, second.FilePath);
            Assert.AreEqual("RichLibrary", first.AssemblyName);
            Assert.AreEqual("RichLibrary", second.AssemblyName);
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies disposal removes the complete private extraction root, is idempotent, and prevents
    /// later extraction.
    /// </summary>
    [TestMethod]
    public void Dispose_AfterExtraction_RemovesRootAndPreventsOpen()
    {
        var packagePath = CreatePackage(("lib/RichLibrary.dll", ReadSampleAssembly()));

        try
        {
            var package = new NuGetPackageAnalyzer(packagePath);
            var entry = Assert.ContainsSingle(package.DllFiles);
            string extractedPath;
            using (var analyzer = package.OpenDll(entry))
            {
                extractedPath = analyzer.FilePath;
            }

            var extractedDirectory = Path.GetDirectoryName(extractedPath)!;
            var extractionRoot = Directory.GetParent(extractedDirectory)!.FullName;
            Assert.IsTrue(File.Exists(extractedPath));
            Assert.IsTrue(Directory.Exists(extractionRoot));

            package.Dispose();
            package.Dispose();

            Assert.IsFalse(Directory.Exists(extractionRoot));
            Assert.ThrowsExactly<ObjectDisposedException>(() => package.OpenDll(entry));
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    /// <summary>
    /// Verifies a malformed manifest does not leave the package archive locked when construction
    /// fails.
    /// </summary>
    [TestMethod]
    public void Constructor_MalformedNuspec_ReleasesArchiveHandle()
    {
        var packagePath = CreatePackageWithManifest(
            "<package><metadata><id>Unclosed",
            ("lib/RichLibrary.dll", ReadSampleAssembly()));

        try
        {
            Assert.ThrowsExactly<XmlException>(() => new NuGetPackageAnalyzer(packagePath));

            using (new FileStream(
                packagePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None))
            {
            }

            File.Delete(packagePath);
            Assert.IsFalse(File.Exists(packagePath));
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    private static string CreatePackage(params (string EntryName, byte[] Content)[] entries) =>
        CreatePackageWithManifest(ValidManifest, entries);

    private static string CreatePackageWithManifest(
        string manifest,
        params (string EntryName, byte[] Content)[] entries)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "dotsider-nupkg-security-" + Guid.NewGuid().ToString("N"));
        var packagePath = Path.Combine(directory, "SecurityTests.1.0.0.nupkg");
        Directory.CreateDirectory(directory);

        try
        {
            using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
            var manifestEntry = archive.CreateEntry("SecurityTests.nuspec", CompressionLevel.NoCompression);
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                writer.Write(manifest);
            }

            foreach (var (entryName, content) in entries)
            {
                var archiveEntry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
                using var destination = archiveEntry.Open();
                destination.Write(content);
            }

            return packagePath;
        }
        catch
        {
            DeleteDirectory(directory);
            throw;
        }
    }

    private static void DeletePackage(string packagePath) =>
        DeleteDirectory(Path.GetDirectoryName(packagePath)!);

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private static byte[] ReadSampleAssembly() => File.ReadAllBytes(Samples.RichLibraryDll);

    private static byte[] CreatePaddedAssembly(int minimumLength)
    {
        var assembly = ReadSampleAssembly();
        if (assembly.Length >= minimumLength)
            return assembly;

        Array.Resize(ref assembly, minimumLength);
        return assembly;
    }
}
