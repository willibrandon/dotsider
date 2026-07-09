using Hex1b;
using Hex1b.Documents;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for native IL-inspector navigation: go-to-definition selects the target symbol and pushes
/// the previous view onto the native back stack, and Esc restores it.
/// </summary>
[TestClass]
public sealed class NativeNavigationTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

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
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigateToNativeSymbol_PushesBackStack_AndRestores()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null || !File.Exists(Samples.NativeAotConsoleExe),
            "NativeAOT publish did not run on this leg.");

        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.NativeAotConsoleExe!);

        var functions = state.Analyzer.NativeSymbols!.Symbols
            .Where(s => s.Kind == Core.Analysis.Models.NativeSymbolKind.Function && s.Size > 0)
            .Take(2).ToList();
        Assert.HasCount(2, functions);

        // Establish the real precondition a UI navigation has: an editor loaded for the current
        // symbol with the cursor somewhere in its listing. NavigateToNativeSymbol captures that
        // editor so Esc restores both the symbol and the exact cursor offset (mirrors managed mode).
        state.IlSelectedNativeSymbol = functions[0];
        state.IlEditorState = new EditorState(new Hex1bDocument("0x1000: 90     nop\n0x1001: c3     ret")) { IsReadOnly = true };
        state.IlEditorNativeSymbol = functions[0];
        state.IlEditorState.SetCursorPosition(new DocumentOffset(12));
        var recordedOffset = state.IlEditorState.Cursor.Position.Value;

        state.NavigateToNativeSymbol(functions[1]);

        Assert.AreEqual(functions[1].VirtualAddress, state.IlSelectedNativeSymbol!.VirtualAddress);
        Assert.ContainsSingle(state.IlNativeBackStack);

        // Move the cursor away, then restore: the offset must snap back to where the jump departed.
        state.IlEditorState.SetCursorPosition(new DocumentOffset(0));
        state.RestoreFromNativeBackEntry(state.IlNativeBackStack.Pop());
        Assert.AreEqual(functions[0].VirtualAddress, state.IlSelectedNativeSymbol!.VirtualAddress);
        Assert.AreEqual(recordedOffset, state.IlEditorState.Cursor.Position.Value);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _app?.Dispose();
        _terminal?.Dispose();
    }
}
