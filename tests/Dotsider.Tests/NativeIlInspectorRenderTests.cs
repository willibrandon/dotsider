using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Full-app render test for native IL-inspector mode: opening a Native AOT binary, entering the
/// IL Inspector, and selecting a function must render its disassembly without an unhandled
/// exception (which would crash the app and leave the terminal in a bad state).
/// </summary>
[Collection("SampleAssemblies")]
public sealed class NativeIlInspectorRenderTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bApp? _hex1bApp;
    private Hex1bTerminal? _terminal;
    private Hex1bAppWorkloadAdapter? _workload;
    private DotsiderState? _state;
    private CancellationTokenSource? _cts;

    /// <summary>Opens a Native AOT binary, enters the IL Inspector, selects a function, and asserts it renders without faulting.</summary>
    [Fact(Timeout = 60_000)]
    public async Task NativeAot_SelectFunction_RendersDisassembly()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null || !File.Exists(samples.NativeAotConsoleExe),
            "NativeAOT publish did not run on this leg.");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder().WithWorkload(_workload).WithHeadless().WithDimensions(120, 30).Build();
        DotsiderApp? app = null;
        _hex1bApp = new Hex1bApp(
            ctx =>
            {
                _state ??= new DotsiderState(_hex1bApp!, samples.NativeAotConsoleExe!);
                app ??= new DotsiderApp(_state);
                return Task.FromResult<Hex1bWidget>(app.Build(ctx));
            },
            new Hex1bAppOptions { WorkloadAdapter = _workload, EnableInputCoalescing = false });

        var runTask = _hex1bApp.RunAsync(_cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("(functions)") || s.ContainsText("(runtime)"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(_terminal, _cts.Token);

        // Select a function programmatically, then let it render.
        var fn = _state!.Analyzer.NativeSymbols!.Symbols
            .First(s => s.Kind == NativeSymbolKind.Function && s.FileOffset is not null && s.Size > 8);
        _state.IlSelectedNativeSymbol = fn;
        _state.App.Invalidate();

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("0x"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(_terminal, _cts.Token);

        Assert.False(runTask.IsFaulted, runTask.Exception?.ToString());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts?.Cancel();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _state?.Dispose();
        _cts?.Dispose();
    }
}
