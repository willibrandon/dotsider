using Dotsider.Views;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace Dotsider.Tests;

/// <summary>
/// Tests private embedded-source storage and inert filename generation.
/// </summary>
[TestClass]
public sealed partial class EmbeddedSourceTempFileStoreTests
{
    private const string AllowedNameCharacters =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-";

    /// <summary>
    /// Verifies every approved source extension survives in canonical lowercase form.
    /// </summary>
    /// <param name="extension">The source extension to validate.</param>
    /// <param name="expected">The expected canonical extension.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(".CS", ".cs")]
    [DataRow(".cshtml", ".cshtml")]
    [DataRow(".fs", ".fs")]
    [DataRow(".fsi", ".fsi")]
    [DataRow(".il", ".il")]
    [DataRow(".json", ".json")]
    [DataRow(".md", ".md")]
    [DataRow(".razor", ".razor")]
    [DataRow(".resx", ".resx")]
    [DataRow(".txt", ".txt")]
    [DataRow(".vb", ".vb")]
    [DataRow(".vbhtml", ".vbhtml")]
    [DataRow(".xaml", ".xaml")]
    [DataRow(".xml", ".xml")]
    public void SanitizeExtension_AllowedSourceExtension_ReturnsCanonicalValue(
        string extension,
        string expected)
    {
        var actual = EmbeddedSourceTempFileStore.SanitizeExtension($"source{extension}");

        Assert.AreEqual(expected, actual);
    }

    /// <summary>
    /// Verifies executable, script, and unknown source extensions become plain text.
    /// </summary>
    /// <param name="extension">The unsafe source extension.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(".appref-ms")]
    [DataRow(".application")]
    [DataRow(".bat")]
    [DataRow(".cmd")]
    [DataRow(".com")]
    [DataRow(".csx")]
    [DataRow(".exe")]
    [DataRow(".fsscript")]
    [DataRow(".fsx")]
    [DataRow(".hta")]
    [DataRow(".js")]
    [DataRow(".jse")]
    [DataRow(".lnk")]
    [DataRow(".msi")]
    [DataRow(".ps1")]
    [DataRow(".reg")]
    [DataRow(".scr")]
    [DataRow(".unknown")]
    [DataRow(".url")]
    [DataRow(".vbs")]
    [DataRow(".wsf")]
    [DataRow(".wsh")]
    [DataRow(".")]
    [DataRow(".c$")]
    [DataRow(".cs ")]
    public void SanitizeExtension_ExecutableOrScriptExtension_ReturnsTxt(string extension)
    {
        var actual = EmbeddedSourceTempFileStore.SanitizeExtension($"source{extension}");

        Assert.AreEqual(".txt", actual);
    }

    /// <summary>
    /// Verifies hostile document names always produce a bounded inert filename.
    /// </summary>
    /// <param name="documentPath">The hostile document path.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("evil\".bat")]
    [DataRow("a&b|c.cs")]
    [DataRow("%PATH%.cs")]
    [DataRow(@"..\..\traversal.cs")]
    [DataRow("foo;bar,baz=qux.cs")]
    [DataRow("x^y!z(w).cs")]
    [DataRow("foo bar.cs")]
    [DataRow("evil\u0001\u001b.cs")]
    [DataRow("café.cs")]
    [DataRow("名前.cs")]
    [DataRow("\uD800.cs")]
    [DataRow("foo.cs:stream")]
    [DataRow(@"\\server\share\x.cs")]
    public void BuildFileName_HostileDocumentPath_ProducesInertName(string documentPath)
    {
        var fileName = EmbeddedSourceTempFileStore.BuildFileName(
            "Method",
            documentPath,
            Guid.Empty);

        Assert.IsTrue(
            InertFileNameRegex().IsMatch(fileName),
            $"Generated filename was not inert: {fileName}");
    }

    /// <summary>
    /// Verifies document filename extraction is independent of the host operating system.
    /// </summary>
    /// <param name="documentPath">The cross-platform metadata document path.</param>
    /// <param name="expectedPrefix">The expected sanitized filename prefix.</param>
    /// <param name="expectedExtension">The expected extension.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(@"C:\dir.with.dot\Source.cs", "Source", ".cs")]
    [DataRow("/dir.with.dot/Source.cs", "Source", ".cs")]
    [DataRow(@"C:\mixed/path\Source.fs", "Source", ".fs")]
    [DataRow(@"\\server\share.with.dot\Source.vb", "Source", ".vb")]
    [DataRow("/parent/source", "source", ".txt")]
    public void BuildFileName_CrossHostDocumentPath_UsesFinalDocumentSegment(
        string documentPath,
        string expectedPrefix,
        string expectedExtension)
    {
        var fileName = EmbeddedSourceTempFileStore.BuildFileName(
            "Fallback",
            documentPath,
            Guid.Empty);

        Assert.StartsWith($"{expectedPrefix}-", fileName);
        Assert.EndsWith(expectedExtension, fileName);
    }

    /// <summary>
    /// Verifies empty and extension-only documents fall back to the sanitized method name.
    /// </summary>
    /// <param name="documentPath">The document path without a usable name.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(".cs")]
    [DataRow("/")]
    [DataRow(@"\")]
    public void BuildFileName_DocumentWithoutName_UsesSanitizedMethodName(string documentPath)
    {
        var fileName = EmbeddedSourceTempFileStore.BuildFileName(
            "<Main>$|x",
            documentPath,
            Guid.Empty);

        Assert.StartsWith("_Main___x-", fileName);
    }

    /// <summary>
    /// Verifies names without any allowlisted character use the documented fallback chain.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BuildFileName_AllInvalidNames_UsesMethodThenSourceFallback()
    {
        var methodFallback = EmbeddedSourceTempFileStore.BuildFileName(
            "Method",
            "!!!.cs",
            Guid.Empty);
        var sourceFallback = EmbeddedSourceTempFileStore.BuildFileName(
            "!!!",
            "!!!.cs",
            Guid.Empty);

        Assert.StartsWith("Method-", methodFallback);
        Assert.StartsWith("source-", sourceFallback);
    }

    /// <summary>
    /// Verifies very long document names produce only the bounded filename prefix.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BuildFileName_LongDocumentName_CapsNameLength()
    {
        var fileName = EmbeddedSourceTempFileStore.BuildFileName(
            "Method",
            $"{new string('a', 5_000)}.cs",
            Guid.Empty);
        var fallbackName = EmbeddedSourceTempFileStore.BuildFileName(
            "Method",
            $"{new string('!', 5_000)}a.cs",
            Guid.Empty);
        var prefixLength = fileName.IndexOf('-', StringComparison.Ordinal);

        Assert.AreEqual(64, prefixLength);
        Assert.StartsWith("Method-", fallbackName);
    }

    /// <summary>
    /// Verifies every UTF-16 code unit is reduced to the filename allowlist.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SanitizeNamePart_EveryUtf16CodeUnit_ProducesAllowedAscii()
    {
        for (var value = 0; value <= char.MaxValue; value++)
        {
            var actual = EmbeddedSourceTempFileStore.SanitizeNamePart(
                new string((char)value, 1));

            Assert.AreEqual(1, actual.Length);
            if (!AllowedNameCharacters.Contains(actual[0]))
            {
                Assert.Fail(
                    $"U+{value:X4} produced disallowed filename character U+{(int)actual[0]:X4}.");
            }
        }
    }

    /// <summary>
    /// Verifies concurrent writes are unique, contained, and preserve exact source bytes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Write_ConcurrentCalls_CreateUniqueContainedFiles()
    {
        using var store = new EmbeddedSourceTempFileStore();
        var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var paths = new string[64];

        Parallel.For(
            0,
            paths.Length,
            index => paths[index] = store.Write("Method", @"C:\src\file.cs", bytes));

        Assert.HasCount(paths.Length, paths.Distinct());
        Assert.IsNotNull(store.SessionDirectory);
        foreach (var path in paths)
        {
            Assert.IsTrue(string.Equals(
                store.SessionDirectory,
                Path.GetDirectoryName(path),
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal));
            Assert.AreSequenceEqual(bytes, File.ReadAllBytes(path));
        }
    }

    /// <summary>
    /// Verifies association preparation changes an allowlisted source extension to text.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PrepareAssociationPath_AllowedSource_MovesFileToTxt()
    {
        using var store = new EmbeddedSourceTempFileStore();
        var sourcePath = store.Write("Method", "Source.cs", [0x2A]);

        var associationPath = store.PrepareAssociationPath(sourcePath);

        Assert.EndsWith(".txt", associationPath);
        Assert.IsFalse(File.Exists(sourcePath));
        Assert.IsTrue(File.Exists(associationPath));
    }

    /// <summary>
    /// Verifies association preparation never overwrites an existing text file.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PrepareAssociationPath_TextTargetExists_ThrowsWithoutOverwriting()
    {
        using var store = new EmbeddedSourceTempFileStore();
        var sourcePath = store.Write("Method", "Source.cs", [0x2A]);
        var associationPath = Path.ChangeExtension(sourcePath, ".txt");
        File.WriteAllBytes(associationPath, [0x11]);

        Assert.ThrowsExactly<IOException>(() => store.PrepareAssociationPath(sourcePath));
        Assert.AreSequenceEqual(new byte[] { 0x11 }, File.ReadAllBytes(associationPath));
        Assert.IsTrue(File.Exists(sourcePath));
    }

    /// <summary>
    /// Verifies association preparation rejects paths outside the owned session directory.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PrepareAssociationPath_ExternalPath_Throws()
    {
        using var store = new EmbeddedSourceTempFileStore();
        _ = store.Write("Method", "Source.cs", [0x2A]);
        var externalPath = Path.Combine(Path.GetTempPath(), "outside.cs");

        Assert.ThrowsExactly<IOException>(() => store.PrepareAssociationPath(externalPath));
    }

    /// <summary>
    /// Verifies Unix source storage uses private directory and file modes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public void Write_OnUnix_UsesPrivatePermissions()
    {
        using var store = new EmbeddedSourceTempFileStore();
        var path = store.Write("Method", "Source.cs", [0x2A]);
        Assert.IsNotNull(store.SessionDirectory);

        var directoryMode = File.GetUnixFileMode(store.SessionDirectory);
        var fileMode = File.GetUnixFileMode(path);

        Assert.AreEqual(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            directoryMode);
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, fileMode);
    }

    /// <summary>
    /// Verifies normal disposal removes only the store's unique session directory.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Dispose_AfterWrite_RemovesSessionDirectoryAndIsIdempotent()
    {
        var store = new EmbeddedSourceTempFileStore();
        _ = store.Write("Method", "Source.cs", [0x2A]);
        var directory = store.SessionDirectory;
        Assert.IsNotNull(directory);
        Assert.Contains(
            "dotsider-embedded-source-",
            Path.GetFileName(directory),
            StringComparison.Ordinal);

        store.Dispose();
        store.Dispose();

        Assert.IsFalse(Directory.Exists(directory));
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            store.Write("Method", "Source.cs", [0x2A]));
    }

    /// <summary>
    /// Verifies a predictable legacy temp path cannot affect session-directory selection.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Write_PredictableLegacyTempDirectoryExists_UsesUniquePrivateDirectory()
    {
        var predictablePath = Path.Combine(Path.GetTempPath(), "dotsider");
        var created = false;
        if (!Directory.Exists(predictablePath) && !File.Exists(predictablePath))
        {
            Directory.CreateDirectory(predictablePath);
            created = true;
        }

        try
        {
            using var store = new EmbeddedSourceTempFileStore();
            _ = store.Write("Method", "Source.cs", [0x2A]);
            Assert.IsNotNull(store.SessionDirectory);

            Assert.IsFalse(string.Equals(
                Path.GetFullPath(predictablePath),
                store.SessionDirectory,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal));
            Assert.StartsWith(
                "dotsider-embedded-source-",
                Path.GetFileName(store.SessionDirectory));

            if (created)
            {
                Directory.Delete(predictablePath);
                File.WriteAllText(predictablePath, "legacy path poison");
                using var filePoisonStore = new EmbeddedSourceTempFileStore();
                _ = filePoisonStore.Write("Method", "Source.cs", [0x2A]);
                Assert.IsNotNull(filePoisonStore.SessionDirectory);
                Assert.IsFalse(string.Equals(
                    Path.GetFullPath(predictablePath),
                    filePoisonStore.SessionDirectory,
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal));
            }
        }
        finally
        {
            if (created)
            {
                if (Directory.Exists(predictablePath))
                    Directory.Delete(predictablePath);
                else
                    File.Delete(predictablePath);
            }
        }
    }

    [GeneratedRegex(
        @"^[A-Za-z0-9_-]{1,64}-[0-9a-f]{32}\.[a-z]{2,7}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex InertFileNameRegex();
}
