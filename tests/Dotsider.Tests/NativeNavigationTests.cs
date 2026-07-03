using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for native IL-inspector navigation: go-to-definition selects the target symbol and pushes
/// the previous view onto the native back stack, and Esc restores it.
/// </summary>
[Collection("SampleAssemblies")]
public sealed class NativeNavigationTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bApp? _app;
    private Hex1bTerminal? _terminal;
    private Hex1bAppWorkloadAdapter? _workload;

    private Hex1bApp CreateApp()
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder().WithWorkload(_workload).WithHeadless().WithDimensions(80, 24).Build();
        _app = new Hex1bApp(_ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("t")),
            new Hex1bAppOptions { WorkloadAdapter = _workload });
        return _app;
    }

    /// <summary>Verifies go-to-symbol pushes the back stack and Esc-restore returns to the origin.</summary>
    [Fact(Timeout = 60_000)]
    public void NavigateToNativeSymbol_PushesBackStack_AndRestores()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null || !File.Exists(samples.NativeAotConsoleExe),
            "NativeAOT publish did not run on this leg.");

        var app = CreateApp();
        using var state = new DotsiderState(app, samples.NativeAotConsoleExe!);

        var functions = state.Analyzer.NativeSymbols!.Symbols
            .Where(s => s.Kind == Core.Analysis.Models.NativeSymbolKind.Function && s.Size > 0)
            .Take(2).ToList();
        Assert.Equal(2, functions.Count);

        state.IlSelectedNativeSymbol = functions[0];
        state.NavigateToNativeSymbol(functions[1]);

        Assert.Equal(functions[1].VirtualAddress, state.IlSelectedNativeSymbol!.VirtualAddress);
        Assert.Single(state.IlNativeBackStack);

        state.RestoreFromNativeBackEntry(state.IlNativeBackStack.Pop());
        Assert.Equal(functions[0].VirtualAddress, state.IlSelectedNativeSymbol!.VirtualAddress);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _app?.Dispose();
        _terminal?.Dispose();
    }
}
