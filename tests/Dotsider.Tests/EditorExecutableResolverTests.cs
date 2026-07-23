using Dotsider.Views;
using System.Runtime.Versioning;

namespace Dotsider.Tests;

/// <summary>
/// Tests deterministic cross-platform editor executable resolution.
/// </summary>
[TestClass]
public sealed class EditorExecutableResolverTests
{
    private static readonly string[] ExpectedPathExtensions = [".CMD", ".EXE", ".BAT"];

    /// <summary>
    /// Verifies a bare Windows editor name resolves a command shim through PATHEXT.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(OperatingSystems.Windows)]
    public void TryResolveWindows_BareCodeName_ResolvesCmdFromPath()
    {
        var directory = Directory.CreateTempSubdirectory("dotsider-editor-resolver-");
        try
        {
            var expected = Path.Combine(directory.FullName, "code.cmd");
            File.WriteAllText(expected, "");

            var resolved = EditorExecutableResolver.TryResolveWindows(
                "code",
                [directory.FullName],
                [".EXE", ".CMD"],
                out var actual);

            Assert.IsTrue(resolved);
            Assert.IsTrue(string.Equals(
                Path.GetFullPath(expected),
                actual,
                StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies PATHEXT ordering selects the first eligible Windows editor target.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(OperatingSystems.Windows)]
    public void TryResolveWindows_MultipleExtensions_UsesPathExtOrder()
    {
        var directory = Directory.CreateTempSubdirectory("dotsider-editor-order-");
        try
        {
            var batch = Path.Combine(directory.FullName, "editor.cmd");
            var executable = Path.Combine(directory.FullName, "editor.exe");
            File.WriteAllText(batch, "");
            File.WriteAllText(executable, "");

            var resolved = EditorExecutableResolver.TryResolveWindows(
                "editor",
                [directory.FullName],
                [".CMD", ".EXE"],
                out var actual);

            Assert.IsTrue(resolved);
            Assert.IsTrue(string.Equals(
                Path.GetFullPath(batch),
                actual,
                StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies Windows PATH directory ordering selects the first matching target.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(OperatingSystems.Windows)]
    public void TryResolveWindows_MultipleSearchDirectories_UsesPathOrder()
    {
        var firstDirectory = Directory.CreateTempSubdirectory("dotsider-editor-first-");
        var secondDirectory = Directory.CreateTempSubdirectory("dotsider-editor-second-");
        try
        {
            var expected = Path.Combine(firstDirectory.FullName, "editor.exe");
            File.WriteAllText(expected, "");
            File.WriteAllText(Path.Combine(secondDirectory.FullName, "editor.exe"), "");

            var resolved = EditorExecutableResolver.TryResolveWindows(
                "editor",
                [firstDirectory.FullName, secondDirectory.FullName],
                [".EXE"],
                out var actual);

            Assert.IsTrue(resolved);
            Assert.IsTrue(string.Equals(
                Path.GetFullPath(expected),
                actual,
                StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            firstDirectory.Delete(recursive: true);
            secondDirectory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies every supported Windows editor target extension resolves.
    /// </summary>
    /// <param name="extension">The supported target extension.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(".bat")]
    [DataRow(".cmd")]
    [DataRow(".com")]
    [DataRow(".exe")]
    public void TryResolveWindows_SupportedExtension_ReturnsTarget(string extension)
    {
        var directory = Directory.CreateTempSubdirectory("dotsider-editor-target-");
        try
        {
            var expected = Path.Combine(directory.FullName, $"editor{extension}");
            File.WriteAllText(expected, "");

            var resolved = EditorExecutableResolver.TryResolveWindows(
                $"editor{extension}",
                [directory.FullName],
                [".EXE"],
                out var actual);

            Assert.IsTrue(resolved);
            Assert.IsTrue(string.Equals(
                Path.GetFullPath(expected),
                actual,
                StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies explicit Windows editor paths do not search alternative PATH directories.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(OperatingSystems.Windows)]
    public void TryResolveWindows_ExplicitPath_IgnoresSearchDirectories()
    {
        var explicitDirectory = Directory.CreateTempSubdirectory("dotsider-editor-explicit-");
        var searchDirectory = Directory.CreateTempSubdirectory("dotsider-editor-search-");
        try
        {
            var expected = Path.Combine(explicitDirectory.FullName, "editor.exe");
            File.WriteAllText(expected, "");
            File.WriteAllText(Path.Combine(searchDirectory.FullName, "editor.exe"), "");

            var resolved = EditorExecutableResolver.TryResolveWindows(
                expected,
                [searchDirectory.FullName],
                [".EXE"],
                out var actual);

            Assert.IsTrue(resolved);
            Assert.AreEqual(Path.GetFullPath(expected), actual);
        }
        finally
        {
            explicitDirectory.Delete(recursive: true);
            searchDirectory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies an explicitly configured relative Windows path resolves only that location.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(OperatingSystems.Windows)]
    public void TryResolveWindows_ExplicitRelativePath_ResolvesAgainstCurrentDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("dotsider-editor-relative-");
        try
        {
            var expected = Path.Combine(directory.FullName, "editor.exe");
            File.WriteAllText(expected, "");
            var relative = Path.GetRelativePath(Environment.CurrentDirectory, expected);
            Assert.IsTrue(relative.Contains(
                Path.DirectorySeparatorChar,
                StringComparison.Ordinal));

            var resolved = EditorExecutableResolver.TryResolveWindows(
                relative,
                [],
                [".EXE"],
                out var actual);

            Assert.IsTrue(resolved);
            Assert.IsTrue(string.Equals(
                Path.GetFullPath(expected),
                actual,
                StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies empty, relative, and unsupported Windows PATH candidates fail closed.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(OperatingSystems.Windows)]
    public void TryResolveWindows_UnsafeSearchEntriesAndExtension_ReturnsFalse()
    {
        var resolved = EditorExecutableResolver.TryResolveWindows(
            "editor",
            ["", ".", "relative"],
            [".PS1"],
            out var actual);

        Assert.IsFalse(resolved);
        Assert.AreEqual("", actual);
    }

    /// <summary>
    /// Verifies PATHEXT normalization retains only supported extensions without duplicates.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SplitPathExtensions_MixedValue_ReturnsSupportedCanonicalOrder()
    {
        var actual = EditorExecutableResolver.SplitPathExtensions(
            "cmd;.EXE;.ps1;.CMD;bat");

        Assert.AreSequenceEqual(
            ExpectedPathExtensions,
            actual);
    }

    /// <summary>
    /// Verifies a bare Unix editor resolves only from the supplied rooted PATH.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public void TryResolveUnix_BareExecutable_ResolvesFromRootedPath()
    {
        var directory = Directory.CreateTempSubdirectory("dotsider-editor-unix-");
        try
        {
            var expected = Path.Combine(directory.FullName, "editor");
            File.WriteAllText(expected, "#!/bin/sh\nexit 0\n");
            MakeExecutable(expected);

            var resolved = EditorExecutableResolver.TryResolveUnix(
                "editor",
                ["", ".", directory.FullName],
                out var actual);

            Assert.IsTrue(resolved);
            Assert.AreEqual(Path.GetFullPath(expected), actual);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies Unix directories and files without execute permission do not resolve.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void TryResolveUnix_NonExecutableTargets_ReturnFalse()
    {
        var directory = Directory.CreateTempSubdirectory("dotsider-editor-nonexec-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "editor"), "#!/bin/sh\n");
            Directory.CreateDirectory(Path.Combine(directory.FullName, "directory-editor"));

            Assert.IsFalse(EditorExecutableResolver.TryResolveUnix(
                "editor",
                [directory.FullName],
                out _));
            Assert.IsFalse(EditorExecutableResolver.TryResolveUnix(
                "directory-editor",
                [directory.FullName],
                out _));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies an explicitly configured Unix executable ignores other search directories.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public void TryResolveUnix_ExplicitPath_IgnoresSearchDirectories()
    {
        var explicitDirectory = Directory.CreateTempSubdirectory("dotsider-editor-unix-explicit-");
        var searchDirectory = Directory.CreateTempSubdirectory("dotsider-editor-unix-search-");
        try
        {
            var expected = Path.Combine(explicitDirectory.FullName, "editor");
            File.WriteAllText(expected, "#!/bin/sh\nexit 0\n");
            MakeExecutable(expected);
            var poison = Path.Combine(searchDirectory.FullName, "editor");
            File.WriteAllText(poison, "#!/bin/sh\nexit 1\n");
            MakeExecutable(poison);

            var resolved = EditorExecutableResolver.TryResolveUnix(
                expected,
                [searchDirectory.FullName],
                out var actual);

            Assert.IsTrue(resolved);
            Assert.AreEqual(Path.GetFullPath(expected), actual);
        }
        finally
        {
            explicitDirectory.Delete(recursive: true);
            searchDirectory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies an explicitly configured relative Unix path resolves against the current directory.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public void TryResolveUnix_ExplicitRelativePath_ResolvesAgainstCurrentDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("dotsider-editor-unix-relative-");
        try
        {
            var expected = Path.Combine(directory.FullName, "editor");
            File.WriteAllText(expected, "#!/bin/sh\nexit 0\n");
            MakeExecutable(expected);
            var relative = Path.GetRelativePath(Environment.CurrentDirectory, expected);
            Assert.Contains('/', relative);

            var resolved = EditorExecutableResolver.TryResolveUnix(
                relative,
                [],
                out var actual);

            Assert.IsTrue(resolved);
            Assert.AreEqual(Path.GetFullPath(expected), actual);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a Unix symlink whose target is executable resolves successfully.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public void TryResolveUnix_SymlinkToExecutable_ReturnsResolvedLink()
    {
        var directory = Directory.CreateTempSubdirectory("dotsider-editor-symlink-");
        try
        {
            var target = Path.Combine(directory.FullName, "target");
            var link = Path.Combine(directory.FullName, "editor");
            File.WriteAllText(target, "#!/bin/sh\nexit 0\n");
            MakeExecutable(target);
            File.CreateSymbolicLink(link, target);

            var resolved = EditorExecutableResolver.TryResolveUnix(
                "editor",
                [directory.FullName],
                out var actual);

            Assert.IsTrue(resolved);
            Assert.AreEqual(Path.GetFullPath(link), actual);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies Unix resolution never gives the current or process directory implicit precedence.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public void TryResolveUnix_PoisonedImplicitDirectories_SearchesOnlySuppliedPath()
    {
        var fileName = $"dotsider-editor-poison-{Guid.NewGuid():N}";
        var currentDirectoryPoison = Path.Combine(Environment.CurrentDirectory, fileName);
        var searchDirectory = Directory.CreateTempSubdirectory("dotsider-editor-clean-");
        try
        {
            File.WriteAllText(currentDirectoryPoison, "#!/bin/sh\nexit 1\n");
            MakeExecutable(currentDirectoryPoison);
            var expected = Path.Combine(searchDirectory.FullName, fileName);
            File.WriteAllText(expected, "#!/bin/sh\nexit 0\n");
            MakeExecutable(expected);

            var resolved = EditorExecutableResolver.TryResolveUnix(
                fileName,
                [searchDirectory.FullName],
                out var actual);

            Assert.IsTrue(resolved);
            Assert.AreEqual(Path.GetFullPath(expected), actual);

            var processName = Path.GetFileName(Environment.ProcessPath);
            Assert.IsNotNull(processName);
            Assert.IsFalse(EditorExecutableResolver.TryResolveUnix(
                processName,
                [],
                out _));
        }
        finally
        {
            File.Delete(currentDirectoryPoison);
            searchDirectory.Delete(recursive: true);
        }
    }

    [UnsupportedOSPlatform("windows")]
    private static void MakeExecutable(string path)
    {
        var mode = File.GetUnixFileMode(path);
        File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute);
    }
}
