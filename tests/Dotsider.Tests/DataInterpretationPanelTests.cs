using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class DataInterpretationPanelTests(SampleAssemblyFixture samples) : IDisposable
{
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
                _state ??= new DotsiderState(_hex1bApp!, dllPath)
                {
                    CurrentTab = 4 // Hex Dump tab
                };
                dotsiderApp ??= new DotsiderApp(_state);
                return Task.FromResult(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
            });
        return (_terminal, _hex1bApp);
    }

    [Fact(Timeout = 30_000)]
    public async Task HexDumpTab_ShowsInterpretationLabels()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s =>
                s.ContainsText("Int8:") && s.ContainsText("Int32:") &&
                s.ContainsText("Float32:") && s.ContainsText("Offset:"),
                TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task HexDumpTab_ShowsEndianLabel()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Endian:") && s.ContainsText("LE"),
                TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task HexDumpTab_ShowsLengthMatchingFileSize()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Length:"), TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        // Verify the length in the panel matches the actual file size
        Assert.NotNull(_state);
        Assert.Equal(_state.Analyzer.RawBytes.Length, _state.HexEditorState.Document.ByteCount);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task HexDumpTab_ShowsHexAddresses()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            // First row address is always 00000000
            .WaitUntil(s => s.ContainsText("00000000"), TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task HexDumpTab_EndianToggleUpdatesValues()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Endian:") && s.ContainsText("LE"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Toggle endianness
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("e")
            .WaitUntil(s => s.ContainsText("BE"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(HexEndianness.Big, _state!.HexEndianness);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task HexDumpTab_CursorMoveUpdatesInterpretation()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Int8:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var textBefore = _state!.DataInterpEditorText;
        Assert.NotNull(textBefore);

        // Move cursor right — byte value changes
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("l")
            .Build()
            .ApplyAsync(terminal, ct);
        await Task.Delay(200, ct);

        var textAfter = _state!.DataInterpEditorText;
        Assert.NotEqual(textBefore, textAfter);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        await runTask.ContinueWith(_ => { }, ct);
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
