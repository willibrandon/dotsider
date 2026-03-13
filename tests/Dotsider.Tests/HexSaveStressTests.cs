using Dotsider.Core.Analysis;
using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class HexSaveStressTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;

    private (Hex1bTerminal terminal, Hex1bApp app) CreateDotsiderApp(string dllPath, [System.Runtime.CompilerServices.CallerMemberName] string? testName = null)
    {
        TestHelpers.Diag($"Creating app for {Path.GetFileName(dllPath)}", testName);
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(120, 30)
            .Build();
        DotsiderApp? dotsiderApp = null;
        var renderCount = 0;
        _hex1bApp = new Hex1bApp(
            ctx =>
            {
                renderCount++;
                if (renderCount <= 3)
                    TestHelpers.Diag($"Render #{renderCount}", testName);
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
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.RightArrow).Key(Hex1bKey.RightArrow)
            .Key(Hex1bKey.RightArrow).Key(Hex1bKey.RightArrow)
            .Key(Hex1bKey.F).Key(Hex1bKey.F);

    [Fact(Timeout = 20_000)]
    public async Task LockedFile_FallsBackToTmpPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempDll = Path.Combine(tempDir, "HelloWorld.dll");
        File.Copy(samples.HelloWorldDll, tempDll);
        FileStream? fileLock = null;

        try
        {
            var (terminal, app) = CreateDotsiderApp(tempDll);
            var ct = TestContext.Current.CancellationToken;
            var runTask = app.RunAsync(ct);
            await Task.Delay(100, ct);

            await EditSafeByte(new Hex1bTerminalInputSequenceBuilder())
                .WaitUntil(_ => _state!.HexIsDirty, TimeSpan.FromSeconds(5))
                .Key(Hex1bKey.Escape)
                .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(5))
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
                .WaitUntil(_ => _state!.HexNotification != null, TimeSpan.FromSeconds(5))
                .Ctrl().Key(Hex1bKey.C)
                .Build()
                .ApplyAsync(terminal, ct);

            Assert.Contains("could not overwrite original", _state!.HexNotification);
            Assert.True(File.Exists(tempDll + ".tmp"), ".tmp file should remain after fallback");
            Assert.False(_state.HexIsDirty);

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

    [Fact(Timeout = 20_000)]
    public async Task InvalidEdit_RejectsCorruptedPe()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempDll = Path.Combine(tempDir, "HelloWorld.dll");
        File.Copy(samples.HelloWorldDll, tempDll);

        try
        {
            var (terminal, app) = CreateDotsiderApp(tempDll);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var runTask = app.RunAsync(cts.Token);
            await Task.Delay(100, cts.Token);

            // Enter insert mode and corrupt the MZ header at offset 0
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
                .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(5))
                .Key(Hex1bKey.D5)
                .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(5))
                .Key(Hex1bKey.I)
                .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(5))
                // Overwrite byte 0 (0x4D 'M') with 0x00, breaking the MZ signature
                .Key(Hex1bKey.D0).Key(Hex1bKey.D0)
                .WaitUntil(_ => _state!.HexIsDirty, TimeSpan.FromSeconds(5))
                .Key(Hex1bKey.Escape)
                .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(5))
                .Ctrl().Key(Hex1bKey.S)
                .WaitUntil(_ => _state!.HexNotification != null, TimeSpan.FromSeconds(5))
                .Build()
                .ApplyAsync(terminal, cts.Token);

            await Task.Delay(100, cts.Token);
            Assert.Contains("invalid image", _state!.HexNotification);
            Assert.False(File.Exists(tempDll + ".tmp"), ".tmp should be cleaned up after validation failure");
            Assert.True(_state.HexIsDirty, "Document should still be dirty after failed save");
            // Analyzer should still be functional (never disposed during Phase 1 failure)
            Assert.Equal(tempDll, _state.Analyzer.FilePath);

            cts.Cancel();
            await runTask;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact(Timeout = 20_000)]
    public async Task DoubleSave_NoDirtyStateAfterFirst()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempDll = Path.Combine(tempDir, "HelloWorld.dll");
        File.Copy(samples.HelloWorldDll, tempDll);

        try
        {
            var (terminal, app) = CreateDotsiderApp(tempDll);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var runTask = app.RunAsync(cts.Token);
            await Task.Delay(100, cts.Token);

            // Edit, save, verify clean state
            await EditSafeByte(new Hex1bTerminalInputSequenceBuilder())
                .WaitUntil(_ => _state!.HexIsDirty, TimeSpan.FromSeconds(5))
                .Key(Hex1bKey.Escape)
                .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(5))
                .Ctrl().Key(Hex1bKey.S)
                .WaitUntil(_ => _state!.HexNotification != null, TimeSpan.FromSeconds(5))
                .Build()
                .ApplyAsync(terminal, cts.Token);

            await Task.Delay(100, cts.Token);
            Assert.False(_state!.HexIsDirty, "Should not be dirty after save");
            Assert.Equal(HexEditMode.Normal, _state.HexMode);
            Assert.Contains("written", _state.HexNotification);

            // Send another Ctrl+S — the binding guard (HexIsDirty) prevents it.
            // Use E (toggle endianness) as a sentinel: if the app processes E,
            // it must have already processed the preceding Ctrl+S.
            var endiannessBefore = _state.HexEndianness;
            _state.HexNotification = null;

            await new Hex1bTerminalInputSequenceBuilder()
                .Ctrl().Key(Hex1bKey.S)
                .Key(Hex1bKey.E)
                .WaitUntil(_ => _state!.HexEndianness != endiannessBefore, TimeSpan.FromSeconds(5))
                .Build()
                .ApplyAsync(terminal, cts.Token);

            await Task.Delay(100, cts.Token);
            Assert.Null(_state.HexNotification);
            Assert.False(File.Exists(tempDll + ".tmp"), "No .tmp should be created on non-dirty save");

            cts.Cancel();
            await runTask;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void InMemoryAnalyzer_FunctionsWithoutDisk()
    {
        // Unit test for the in-memory fallback path that SaveHexChanges uses
        // when all disk candidates are exhausted
        var originalBytes = File.ReadAllBytes(samples.HelloWorldDll);
        var fakePath = "/nonexistent/path/HelloWorld.dll";

        using var analyzer = new AssemblyAnalyzer(originalBytes, fakePath);

        Assert.Equal(fakePath, analyzer.FilePath);
        Assert.Equal("HelloWorld.dll", analyzer.FileName);
        Assert.Equal(originalBytes.Length, analyzer.FileSize);
        Assert.True(analyzer.HasMetadata);
        Assert.NotNull(analyzer.AssemblyName);
        Assert.True(analyzer.RawBytes.Length > 0);
    }

    public void Dispose()
    {
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        GC.SuppressFinalize(this);
    }
}
