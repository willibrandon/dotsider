using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;
using System.IO.Compression;
using System.Text;

namespace Dotsider.Tests;

/// <summary>
/// Verifies that NuGet mode reports unsafe package entries without leaving the package browser.
/// </summary>
/// <param name="testContext">The current test context.</param>
[TestClass]
public sealed class NuGetUnsafeEntryIntegrationTests(TestContext testContext)
{
    private const string ExtractionFailedError = "Cannot open DLL: extraction failed";
    private const string InvalidAssemblyError = "Cannot open DLL: invalid .NET assembly";
    private const string UnsafeEntryError = "Cannot open DLL: unsafe package entry path";

    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private readonly TestContext _testContext = testContext;

    /// <summary>
    /// Verifies that activating an unsafe DLL reports a sanitized error and a subsequent safe DLL opens.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task UnsafeDllActivation_RejectsEntryThenSafeDllOpens()
    {
        var cancellationToken = _testContext.CancellationToken;
        var uniqueName = "dotsider-nupkg-ui-" + Guid.NewGuid().ToString("N");
        var packageDirectory = Directory.CreateTempSubdirectory("dotsider-nupkg-ui-test-").FullName;
        var packagePath = Path.Combine(packageDirectory, "UnsafePackage.1.0.0.nupkg");
        var outsideDirectory = Path.Combine(Path.GetTempPath(), uniqueName);
        var sentinelPath = Path.Combine(outsideDirectory, "unsafe.dll");
        var sentinelText = "outside sentinel must remain unchanged";
        var unsafeEntryPath = $"../{uniqueName}/unsafe.dll";
        const string safeEntryPath = "lib/net10.0/RichLibrary.dll";

        try
        {
            Directory.CreateDirectory(outsideDirectory);
            File.WriteAllText(sentinelPath, sentinelText);

            var assemblyBytes = File.ReadAllBytes(Samples.RichLibraryDll);
            CreatePackage(
                packagePath,
                (unsafeEntryPath, assemblyBytes),
                (safeEntryPath, assemblyBytes));

            var workload = new Hex1bAppWorkloadAdapter();
            var terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(workload)
                .WithHeadless()
                .WithDimensions(120, 30)
                .Build();
            NuGetState? state = null;
            NuGetApp? nuGetApp = null;
            Hex1bApp? app = null;
            app = new Hex1bApp(
                context =>
                {
                    state ??= new NuGetState(app!, packagePath);
                    nuGetApp ??= new NuGetApp(state);
                    return Task.FromResult<Hex1bWidget>(nuGetApp.Build(context));
                },
                new Hex1bAppOptions
                {
                    EnableInputCoalescing = false,
                    WorkloadAdapter = workload
                });

            var runTask = app.RunAsync(cancellationToken);
            string? extractedPath = null;
            string? extractionRoot = null;
            string? observedOpenError = null;
            var observedUnsafeState = false;
            var unsafeRowRemainedVisible = false;
            var rawHostilePathRendered = false;

            try
            {
                await new Hex1bTerminalInputSequenceBuilder()
                    .WaitUntil(snapshot => snapshot.InAlternateScreen, TimeSpan.FromSeconds(10))
                    .WaitUntil(
                        _ => string.Equals(
                            state?.FileTreeFocusedKey as string,
                            unsafeEntryPath,
                            StringComparison.Ordinal),
                        TimeSpan.FromSeconds(10))
                    .Key(Hex1bKey.Enter)
                    .WaitUntil(snapshot =>
                    {
                        observedOpenError = state?.OpenError;
                        observedUnsafeState = state is
                        {
                            IsBrowsingPackage: true,
                            OpenError: UnsafeEntryError,
                            SelectedDllState: null
                        };
                        unsafeRowRemainedVisible = snapshot.ContainsText("unsafe.dll");
                        rawHostilePathRendered = snapshot.ContainsText(unsafeEntryPath);
                        return observedUnsafeState
                            && unsafeRowRemainedVisible
                            && snapshot.ContainsText(UnsafeEntryError);
                    }, TimeSpan.FromSeconds(10))
                    .Key(Hex1bKey.DownArrow)
                    .WaitUntil(
                        _ => string.Equals(
                            state?.FileTreeFocusedKey as string,
                            safeEntryPath,
                            StringComparison.Ordinal),
                        TimeSpan.FromSeconds(10))
                    .Key(Hex1bKey.Enter)
                    .WaitUntil(_ => state is
                    {
                        IsBrowsingPackage: false,
                        OpenError: null,
                        SelectedDllEntry.Name: "RichLibrary.dll",
                        SelectedDllState: not null
                    }, TimeSpan.FromSeconds(10))
                    .WaitUntil(snapshot => snapshot.ContainsText("DLL Inspector"), TimeSpan.FromSeconds(10))
                    .Build()
                    .ApplyAsync(terminal, cancellationToken);

                Assert.IsTrue(observedUnsafeState);
                Assert.IsTrue(unsafeRowRemainedVisible);
                Assert.AreEqual(UnsafeEntryError, observedOpenError);
                Assert.IsFalse(rawHostilePathRendered);
                Assert.IsNotNull(state);
                Assert.IsFalse(state.IsBrowsingPackage);
                Assert.IsNull(state.OpenError);
                Assert.IsNotNull(state.SelectedDllState);
                Assert.AreEqual("RichLibrary", state.SelectedDllState.Analyzer.AssemblyName);
                Assert.AreEqual(safeEntryPath, state.SelectedDllEntry!.FullPath);
                Assert.Contains(unsafeEntryPath, state.Package.DllFiles.Select(entry => entry.FullPath));

                extractedPath = state.SelectedDllState.Analyzer.FilePath;
                extractionRoot = GetExtractionRoot(extractedPath);
                Assert.IsTrue(File.Exists(extractedPath));
            }
            finally
            {
                app.RequestStop();
                try
                {
                    await runTask;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // The cooperative test timeout has already failed the test.
                }
                finally
                {
                    try
                    {
                        state?.Dispose();
                        state?.Dispose();
                    }
                    finally
                    {
                        app.Dispose();
                        terminal.Dispose();
                        workload.Dispose();
                    }
                }
            }

            Assert.IsNotNull(extractedPath);
            Assert.IsNotNull(extractionRoot);
            Assert.IsFalse(File.Exists(extractedPath));
            Assert.IsFalse(Directory.Exists(extractionRoot));
            Assert.AreEqual(sentinelText, File.ReadAllText(sentinelPath));
        }
        finally
        {
            try
            {
                DeleteDirectory(packageDirectory);
            }
            finally
            {
                DeleteDirectory(outsideDirectory);
            }
        }
    }

    /// <summary>
    /// Verifies terminal control sequences in an archive name are rendered visibly and cannot
    /// execute an OSC clipboard operation while the package is merely being browsed.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task MaliciousControlText_IsEscapedBeforeTerminalRendering()
    {
        const string RawAuthors = "dotsider\u009B31mtests";
        const string RawControlName =
            "\x1b]52;c;cHduZWQ=\x07-\x1b[31mX\x1b[0m-\x1b]8;;x\x07L\x1b]8;;\x07.dll";
        const string RawDescription = "security\u2028metadata";
        const string RawPackageId = "Unsafe\u202EPackage";
        const string RawPackageVersion = "1.0.0\u2066";
        const string VisibleAuthors = "dotsider\\u009B31mtests";
        const string VisibleControlPrefix = "␛]52;c;cHduZWQ=␇-␛[31mX␛[0m";
        const string VisibleDescription = "security\\u2028metadata";
        const string VisiblePackageId = "Unsafe\\u202EPackage";
        const string VisiblePackageVersion = "1.0.0\\u2066";
        var cancellationToken = _testContext.CancellationToken;
        var packageDirectory = Directory.CreateTempSubdirectory("dotsider-nupkg-control-test-").FullName;
        var rawPackageFileName = OperatingSystem.IsWindows()
            ? "Control\u202EPackage.1.0.0.nupkg"
            : "\x1b]52;c;cGFja2FnZQ==\x07-ControlPackage.1.0.0.nupkg";
        var visiblePackageFileName = OperatingSystem.IsWindows()
            ? "Control\\u202EPackage.1.0.0.nupkg"
            : "␛]52;c;cGFja2FnZQ==␇-ControlPackage.1.0.0.nupkg";
        var packagePath = Path.Combine(packageDirectory, rawPackageFileName);
        var entryPath = "lib/" + RawControlName;

        try
        {
            CreatePackageWithMetadata(
                packagePath,
                RawPackageId,
                RawPackageVersion,
                RawAuthors,
                RawDescription,
                (entryPath, []));

            var workload = new Hex1bAppWorkloadAdapter();
            var clipboard = new ClipboardCapturingWorkloadAdapter(workload);
            var terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(workload)
                .WithHeadless()
                .WithDimensions(120, 30)
                .Build();
            NuGetState? state = null;
            NuGetApp? nuGetApp = null;
            Hex1bApp? app = null;
            app = new Hex1bApp(
                context =>
                {
                    state ??= new NuGetState(app!, packagePath);
                    nuGetApp ??= new NuGetApp(state);
                    return Task.FromResult<Hex1bWidget>(nuGetApp.Build(context));
                },
                new Hex1bAppOptions
                {
                    EnableInputCoalescing = false,
                    WorkloadAdapter = clipboard
                });

            var runTask = app.RunAsync(cancellationToken);
            var rawMetadataRendered = false;
            try
            {
                await new Hex1bTerminalInputSequenceBuilder()
                    .WaitUntil(snapshot => snapshot.InAlternateScreen, TimeSpan.FromSeconds(10))
                    .WaitUntil(
                        snapshot =>
                        {
                            rawMetadataRendered = snapshot.ContainsText(RawPackageId)
                                || snapshot.ContainsText(RawPackageVersion)
                                || snapshot.ContainsText(RawAuthors)
                                || snapshot.ContainsText(RawDescription);
                            return snapshot.ContainsText(VisibleControlPrefix)
                                && snapshot.ContainsText(visiblePackageFileName)
                                && snapshot.ContainsText(VisiblePackageId)
                                && snapshot.ContainsText(VisiblePackageVersion)
                                && snapshot.ContainsText(VisibleAuthors)
                                && snapshot.ContainsText(VisibleDescription);
                        },
                        TimeSpan.FromSeconds(10))
                    .Build()
                    .ApplyAsync(terminal, cancellationToken);

                Assert.IsNotNull(state);
                Assert.AreEqual(rawPackageFileName, state.Package.FileName);
                Assert.AreEqual(RawPackageId, state.Package.PackageId);
                Assert.AreEqual(RawPackageVersion, state.Package.PackageVersion);
                Assert.AreEqual(RawAuthors, state.Package.Authors);
                Assert.AreEqual(RawDescription, state.Package.Description);
                var entry = Assert.ContainsSingle(state.Package.DllFiles);
                Assert.AreEqual(entryPath, entry.FullPath);
                Assert.AreEqual(RawControlName, entry.Name);
                Assert.IsFalse(rawMetadataRendered);
                Assert.IsEmpty(clipboard.ClipboardWrites);
                Assert.IsNull(state.Package.ExtractionDirectory);

                await new Hex1bTerminalInputSequenceBuilder()
                    .Type("y")
                    .WaitUntil(_ => clipboard.ClipboardWrites.Count == 1, TimeSpan.FromSeconds(10))
                    .WaitUntil(
                        snapshot => snapshot.ContainsText("Yanked: lib/␛]52;c;"),
                        TimeSpan.FromSeconds(10))
                    .Build()
                    .ApplyAsync(terminal, cancellationToken);

                Assert.IsTrue(clipboard.ClipboardWrites.TryDequeue(out var clipboardText));
                Assert.AreEqual(entryPath, clipboardText);
                Assert.IsEmpty(clipboard.ClipboardWrites);
                Assert.IsNotNull(state.YankNotification);
                Assert.DoesNotContain("\x1b", state.YankNotification);
            }
            finally
            {
                app.RequestStop();
                try
                {
                    await runTask;
                }
                finally
                {
                    try
                    {
                        state?.Dispose();
                    }
                    finally
                    {
                        app.Dispose();
                        terminal.Dispose();
                        clipboard.Dispose();
                    }
                }
            }
        }
        finally
        {
            DeleteDirectory(packageDirectory);
        }
    }

    /// <summary>
    /// Verifies opening one safe DLL extracts only that DLL and a pre-existing destination is
    /// never overwritten, while the selected analyzer remains active after the failed open.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryOpenDll_ExistingDestination_PreservesFileAndSelectedState()
    {
        const string FirstEntryPath = "lib/net10.0/First.dll";
        const string SecondEntryPath = "lib/net10.0/Second.dll";
        byte[] sentinel = [0x21, 0x09, 0x20, 0x99];
        var packageDirectory = Directory.CreateTempSubdirectory("dotsider-nupkg-existing-test-").FullName;
        var packagePath = Path.Combine(packageDirectory, "ExistingDestination.1.0.0.nupkg");

        try
        {
            var assemblyBytes = File.ReadAllBytes(Samples.RichLibraryDll);
            CreatePackage(
                packagePath,
                (FirstEntryPath, assemblyBytes),
                (SecondEntryPath, assemblyBytes));

            using var workload = new Hex1bAppWorkloadAdapter();
            using var terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(workload)
                .WithHeadless()
                .WithDimensions(80, 24)
                .Build();
            using var app = new Hex1bApp(
                _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
                new Hex1bAppOptions { WorkloadAdapter = workload });
            using var state = new NuGetState(app, packagePath);
            var firstEntry = state.Package.DllFiles.Single(static entry =>
                entry.FullPath == FirstEntryPath);
            var secondEntry = state.Package.DllFiles.Single(static entry =>
                entry.FullPath == SecondEntryPath);

            Assert.IsTrue(state.TryOpenDll(firstEntry));
            var selectedState = state.SelectedDllState;
            Assert.IsNotNull(selectedState);
            Assert.AreSame(firstEntry, state.SelectedDllEntry);
            var extractionDirectory = state.Package.ExtractionDirectory;
            Assert.IsNotNull(extractionDirectory);
            var secondDestination = Path.Combine(
                extractionDirectory,
                "lib",
                "net10.0",
                "Second.dll");
            Assert.IsFalse(File.Exists(secondDestination));

            File.WriteAllBytes(secondDestination, sentinel);

            Assert.IsFalse(state.TryOpenDll(secondEntry));
            Assert.AreEqual(ExtractionFailedError, state.OpenError);
            Assert.AreSame(selectedState, state.SelectedDllState);
            Assert.AreSame(firstEntry, state.SelectedDllEntry);
            Assert.IsFalse(state.IsBrowsingPackage);
            Assert.AreSequenceEqual(sentinel, File.ReadAllBytes(secondDestination));
        }
        finally
        {
            DeleteDirectory(packageDirectory);
        }
    }

    /// <summary>
    /// Verifies that a package-owned DLL containing invalid bytes produces a sanitized state error.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryOpenDll_InvalidAssembly_ShowsSanitizedError()
    {
        var packageDirectory = Directory.CreateTempSubdirectory("dotsider-nupkg-invalid-test-").FullName;
        var packagePath = Path.Combine(packageDirectory, "InvalidPackage.1.0.0.nupkg");
        const string invalidEntryPath = "lib/net10.0/raw-hostile-value.dll";

        try
        {
            CreatePackage(packagePath, (invalidEntryPath, [0x01, 0x02, 0x03, 0x04]));

            using var workload = new Hex1bAppWorkloadAdapter();
            using var terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(workload)
                .WithHeadless()
                .WithDimensions(80, 24)
                .Build();
            using var app = new Hex1bApp(
                _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
                new Hex1bAppOptions { WorkloadAdapter = workload });
            using var state = new NuGetState(app, packagePath);

            var entry = Assert.ContainsSingle(state.Package.DllFiles);
            var opened = state.TryOpenDll(entry);

            Assert.IsFalse(opened);
            Assert.IsTrue(state.IsBrowsingPackage);
            Assert.IsNull(state.SelectedDllState);
            Assert.AreEqual(InvalidAssemblyError, state.OpenError);
            Assert.DoesNotContain(invalidEntryPath, state.OpenError!);

            state.Dispose();
            state.Dispose();
        }
        finally
        {
            DeleteDirectory(packageDirectory);
        }
    }

    private static void CreatePackage(
        string packagePath,
        params (string EntryPath, byte[] Contents)[] files) =>
        CreatePackageWithMetadata(
            packagePath,
            "UnsafePackage",
            "1.0.0",
            "dotsider tests",
            "Security regression package",
            files);

    private static void CreatePackageWithMetadata(
        string packagePath,
        string packageId,
        string packageVersion,
        string authors,
        string description,
        params (string EntryPath, byte[] Contents)[] files)
    {
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        WriteEntry(
            archive,
            "UnsafePackage.nuspec",
            Encoding.UTF8.GetBytes(
                $"<package><metadata><id>{packageId}</id><version>{packageVersion}</version>" +
                $"<authors>{authors}</authors><description>{description}</description>" +
                "</metadata></package>"));

        foreach (var (entryPath, contents) in files)
            WriteEntry(archive, entryPath, contents);
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private static string GetExtractionRoot(string extractedPath)
    {
        var tempPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var relativePath = Path.GetRelativePath(tempPath, extractedPath);
        var firstSeparator = relativePath.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        Assert.IsGreaterThan(0, firstSeparator, $"Extracted path was not inside a private temp directory: {extractedPath}");
        return Path.Combine(tempPath, relativePath[..firstSeparator]);
    }

    private static void WriteEntry(ZipArchive archive, string entryPath, byte[] contents)
    {
        var entry = archive.CreateEntry(entryPath, CompressionLevel.NoCompression);
        using var stream = entry.Open();
        stream.Write(contents);
    }
}
