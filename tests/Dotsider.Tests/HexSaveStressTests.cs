using Dotsider.Core.Analysis;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Hex Save Stress.
/// </summary>
[TestClass]
public class HexSaveStressTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;

    private (Hex1bTerminal terminal, Hex1bApp app) CreateDotsiderApp(string dllPath)
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(120, 30)
            .Build();
        DotsiderApp? dotsiderApp = null;
        _hex1bApp = new Hex1bApp(
            ctx =>
            {
                _state ??= new DotsiderState(_hex1bApp!, dllPath);
                dotsiderApp ??= new DotsiderApp(_state);
                return Task.FromResult<Hex1bWidget>(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
            });
        return (_terminal, _hex1bApp);
    }

    /// <summary>
    /// Builds an input sequence that navigates to the hex dump tab, enters insert mode,
    /// edits a safe byte in DOS stub padding (offset 2 → 0xFF), and returns to normal mode.
    /// </summary>
    private static Hex1bTerminalInputSequenceBuilder EditSafeByte(Hex1bTerminalInputSequenceBuilder builder)
        => builder
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow).Key(Hex1bKey.RightArrow)
            .Key(Hex1bKey.RightArrow).Key(Hex1bKey.RightArrow)
            .Key(Hex1bKey.F).Key(Hex1bKey.F);

    /// <summary>
    /// Verifies locked file falls back to tmp path.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LockedFile_FallsBackToTmpPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempDll = Path.Combine(tempDir, "HelloWorld.dll");
        File.Copy(Samples.HelloWorldDll, tempDll);
        FileStream? fileLock = null;

        try
        {
            var (terminal, app) = CreateDotsiderApp(tempDll);
            var ct = CancellationToken.None;
            var runTask = app.RunAsync(ct);
            await Task.Delay(100, ct);

            await EditSafeByte(new Hex1bTerminalInputSequenceBuilder())
                .WaitUntil(_ => _state!.HexIsDirty, TimeSpan.FromSeconds(10))
                .Key(Hex1bKey.Escape)
                .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
                .Build()
                .ApplyAsync(terminal, ct);

            // Block File.Move from overwriting the original.
            // Windows: hold a read handle without FileShare.Delete so
            //          MoveFileEx cannot replace the target.
            // Unix:    pre-create the .tmp file so Phase 1 can still write
            //          to it, then remove directory write permission so
            //          rename() fails with EACCES.
            if (OperatingSystem.IsWindows())
            {
                fileLock = new FileStream(tempDll, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            else
            {
                File.WriteAllBytes(tempDll + ".tmp", []);
                File.SetUnixFileMode(tempDir,
                    UnixFileMode.UserRead | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            await new Hex1bTerminalInputSequenceBuilder()
                .Ctrl().Key(Hex1bKey.S)
                .WaitUntil(_ => _state!.HexNotification != null, TimeSpan.FromSeconds(10))
                .Ctrl().Key(Hex1bKey.C)
                .Build()
                .ApplyAsync(terminal, ct);

            Assert.Contains("could not overwrite original", _state!.HexNotification!);
            Assert.IsTrue(File.Exists(tempDll + ".tmp"), ".tmp file should remain after fallback");
            Assert.IsFalse(_state.HexIsDirty);

            await runTask.ContinueWith(_ => { }, ct);
        }
        finally
        {
            fileLock?.Dispose();
            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    File.SetUnixFileMode(tempDir,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
                catch { }
            }
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// Verifies invalid edit rejects corrupted pe.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task InvalidEdit_RejectsCorruptedPe()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempDll = Path.Combine(tempDir, "HelloWorld.dll");
        File.Copy(Samples.HelloWorldDll, tempDll);

        try
        {
            var (terminal, app) = CreateDotsiderApp(tempDll);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
            var runTask = app.RunAsync(cts.Token);
            await Task.Delay(100, cts.Token);

            // Enter insert mode and corrupt the MZ header at offset 0
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
                .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
                .Key(Hex1bKey.D5)
                .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
                .Key(Hex1bKey.I)
                .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
                // Overwrite byte 0 (0x4D 'M') with 0x00, breaking the MZ signature
                .Key(Hex1bKey.D0).Key(Hex1bKey.D0)
                .WaitUntil(_ => _state!.HexIsDirty, TimeSpan.FromSeconds(10))
                .Key(Hex1bKey.Escape)
                .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
                .Ctrl().Key(Hex1bKey.S)
                .WaitUntil(_ => _state!.HexNotification != null, TimeSpan.FromSeconds(10))
                .Build()
                .ApplyAsync(terminal, cts.Token);

            await Task.Delay(100, cts.Token);
            Assert.Contains("invalid image", _state!.HexNotification!);
            Assert.IsFalse(File.Exists(tempDll + ".tmp"), ".tmp should be cleaned up after validation failure");
            Assert.IsTrue(_state.HexIsDirty, "Document should still be dirty after failed save");
            // Analyzer should still be functional (never disposed during Phase 1 failure)
            Assert.AreEqual(tempDll, _state.Analyzer.FilePath);

            cts.Cancel();
            await runTask;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// Verifies double save no dirty state after first.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task DoubleSave_NoDirtyStateAfterFirst()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempDll = Path.Combine(tempDir, "HelloWorld.dll");
        File.Copy(Samples.HelloWorldDll, tempDll);

        try
        {
            var (terminal, app) = CreateDotsiderApp(tempDll);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
            var runTask = app.RunAsync(cts.Token);
            await Task.Delay(100, cts.Token);

            // Edit, save, verify clean state
            await EditSafeByte(new Hex1bTerminalInputSequenceBuilder())
                .WaitUntil(_ => _state!.HexIsDirty, TimeSpan.FromSeconds(10))
                .Key(Hex1bKey.Escape)
                .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
                .Ctrl().Key(Hex1bKey.S)
                .WaitUntil(_ => _state!.HexNotification != null, TimeSpan.FromSeconds(10))
                .Build()
                .ApplyAsync(terminal, cts.Token);

            await Task.Delay(100, cts.Token);
            Assert.IsFalse(_state!.HexIsDirty, "Should not be dirty after save");
            Assert.AreEqual(HexEditMode.Normal, _state.HexMode);
            Assert.Contains("written", _state.HexNotification!);

            // Send another Ctrl+S — the binding guard (HexIsDirty) prevents it.
            // Use E (toggle endianness) as a sentinel: if the app processes E,
            // it must have already processed the preceding Ctrl+S.
            var endiannessBefore = _state.HexEndianness;
            _state.HexNotification = null;

            await new Hex1bTerminalInputSequenceBuilder()
                .Ctrl().Key(Hex1bKey.S)
                .Key(Hex1bKey.E)
                .WaitUntil(_ => _state!.HexEndianness != endiannessBefore, TimeSpan.FromSeconds(10))
                .Build()
                .ApplyAsync(terminal, cts.Token);

            await Task.Delay(100, cts.Token);
            Assert.IsNull(_state.HexNotification);
            Assert.IsFalse(File.Exists(tempDll + ".tmp"), "No .tmp should be created on non-dirty save");

            cts.Cancel();
            await runTask;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// Verifies native binary hex save succeeds.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task NativeBinary_HexSave_Succeeds()
    {
        // Copy ONLY the apphost (no companion DLL) so there's no apphost
        // dialog — the file opens as a plain native binary without metadata.
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "HelloWorld");
        File.Copy(Samples.HelloWorldExe, tempFile);

        try
        {
            var (terminal, app) = CreateDotsiderApp(tempFile);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
            var runTask = app.RunAsync(cts.Token);
            await Task.Delay(100, cts.Token);

            // Navigate to hex tab, edit a byte, save
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
                .Key(Hex1bKey.D5)
                .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
                .Key(Hex1bKey.I)
                .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
                .Key(Hex1bKey.RightArrow).Key(Hex1bKey.RightArrow)
                .Key(Hex1bKey.RightArrow).Key(Hex1bKey.RightArrow)
                .Key(Hex1bKey.F).Key(Hex1bKey.F)
                .WaitUntil(_ => _state!.HexIsDirty, TimeSpan.FromSeconds(10))
                .Key(Hex1bKey.Escape)
                .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
                .Ctrl().Key(Hex1bKey.S)
                .WaitUntil(_ => _state!.HexNotification != null, TimeSpan.FromSeconds(10))
                .Build()
                .ApplyAsync(terminal, cts.Token);

            Assert.Contains("written", _state!.HexNotification!);
            Assert.IsFalse(_state.HexIsDirty);

            cts.Cancel();
            await runTask;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// Verifies in memory analyzer functions without disk.
    /// </summary>
    [TestMethod]
    public void InMemoryAnalyzer_FunctionsWithoutDisk()
    {
        // Unit test for the in-memory fallback path that SaveHexChanges uses
        // when all disk candidates are exhausted
        var originalBytes = File.ReadAllBytes(Samples.HelloWorldDll);
        var fakePath = "/nonexistent/path/HelloWorld.dll";

        using var analyzer = new AssemblyAnalyzer(originalBytes, fakePath);

        Assert.AreEqual(fakePath, analyzer.FilePath);
        Assert.AreEqual("HelloWorld.dll", analyzer.FileName);
        Assert.AreEqual(originalBytes.Length, analyzer.FileSize);
        Assert.IsTrue(analyzer.HasMetadata);
        Assert.IsNotNull(analyzer.AssemblyName);
        Assert.IsGreaterThan(0, analyzer.RawBytes.Length);
    }

    /// <summary>
    /// Verifies in memory analyzer native binary save recovery path.
    /// </summary>
    [TestMethod]
    public void InMemoryAnalyzer_NativeBinary_SaveRecoveryPath()
    {
        // Simulates the byte-array fallback in SaveHexChanges: after a hex
        // edit on a native binary (apphost/NativeAOT), all disk candidates
        // are exhausted and the analyzer is reconstructed from memory.
        var originalBytes = File.ReadAllBytes(Samples.HelloWorldExe);

        // Simulate a hex edit: modify a byte in the native binary
        var editedBytes = originalBytes.ToArray();
        editedBytes[4] = 0xFF;

        using var analyzer = new AssemblyAnalyzer(editedBytes, Samples.HelloWorldExe);

        Assert.AreEqual(Samples.HelloWorldExe, analyzer.FilePath);
        Assert.AreEqual(editedBytes.Length, analyzer.FileSize);
        Assert.IsFalse(analyzer.HasMetadata);
        Assert.IsFalse(analyzer.RawBytes.IsEmpty);
        Assert.AreEqual(0xFF, analyzer.RawBytes.Span[4]);
    }

    /// <summary>
    /// Verifies reopen or fallback all candidates fail returns in memory analyzer.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReopenOrFallback_AllCandidatesFail_ReturnsInMemoryAnalyzer()
    {
        // Exercises the candidate loop + byte-array fallback with all
        // non-existent paths so the in-memory branch triggers.
        var originalBytes = File.ReadAllBytes(Samples.HelloWorldExe);
        var editedBytes = originalBytes.ToArray();
        editedBytes[4] = 0xFF;

        string[] bogusPath =
        [
            "/nonexistent/dir/HelloWorld",
            "/nonexistent/dir/HelloWorld.tmp",
            "/nonexistent/dir/HelloWorld.recovery"
        ];

        var (analyzer, resolvedPath) = DotsiderApp.ReopenOrFallback(
            bogusPath, editedBytes, Samples.HelloWorldExe);

        using (analyzer)
        {
            Assert.IsNull(resolvedPath);
            Assert.AreEqual(Samples.HelloWorldExe, analyzer.FilePath);
            Assert.IsFalse(analyzer.HasMetadata);
            Assert.AreEqual(editedBytes.Length, analyzer.FileSize);
            Assert.AreEqual(0xFF, analyzer.RawBytes.Span[4]);
        }
    }

    /// <summary>
    /// Verifies save hex changes native binary memory fallback sets notification.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SaveHexChanges_NativeBinary_MemoryFallbackSetsNotification()
    {
        // Drives SaveHexChanges through the resolvedPath == null branch by
        // injecting a reopener that always returns the in-memory fallback.
        // Verifies the caller correctly sets the "working from memory" notification.
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "HelloWorld");
        File.Copy(Samples.HelloWorldExe, tempFile);

        try
        {
            _workload = new Hex1bAppWorkloadAdapter();
            _terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(_workload)
                .WithHeadless()
                .WithDimensions(80, 24)
                .Build();
            _hex1bApp = new Hex1bApp(
                _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
                new Hex1bAppOptions { WorkloadAdapter = _workload });
            _state = new DotsiderState(_hex1bApp, tempFile);

            // Make the document dirty
            _state.HexEditorState.IsReadOnly = false;
            _state.HexEditorState.Document.ApplyBytes(
                new ByteReplaceOperation(4, 1, [0xFF]));
            _state.HexEditorState.IsReadOnly = true;
            Assert.IsTrue(_state.HexIsDirty);

            // Inject a reopener that simulates all disk candidates failing
            DotsiderApp.SaveHexChanges(_state,
                reopener: (_, bytes, path) => (new AssemblyAnalyzer(bytes, path), null));

            Assert.AreEqual("Saved (working from memory — file may be locked)", _state.HexNotification);
            Assert.IsFalse(_state.HexIsDirty);
            Assert.IsFalse(_state.Analyzer.HasMetadata);
            Assert.AreEqual(0xFF, _state.Analyzer.RawBytes.Span[4]);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
    }
}
