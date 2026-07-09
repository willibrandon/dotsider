using Dotsider.Views;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the native IL-inspector tree: a non-managed binary buckets its executable symbols
/// namespace → type → function with the symbol carried on the method rows, so the same tree widget
/// drives native disassembly.
/// </summary>
[TestClass]
public sealed class NativeIlTreeTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bApp? _app;
    private Hex1bTerminal? _terminal;
    private Hex1bAppWorkloadAdapter? _workload;

    private Hex1bApp CreateApp()
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();
        _app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = _workload });
        return _app;
    }

    /// <summary>Verifies the native tree buckets symbols and carries the symbol on the leaf rows.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BuildNativeTreeRows_NativeAot_BucketsSymbols()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null || !File.Exists(Samples.NativeAotConsoleExe),
            "NativeAOT publish did not run on this leg.");

        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.NativeAotConsoleExe!);

        var rows = IlInspectorView.BuildNativeTreeRows(state);

        Assert.IsNotEmpty(rows);
        Assert.Contains(r => r.Kind == IlTreeRowKind.Namespace, rows);

        // Expand every namespace, then every type, so the leaf function rows appear (each level is
        // only emitted once its parent is expanded).
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var r in IlInspectorView.BuildNativeTreeRows(state).Where(r => r.CanExpand))
                state.IlTreeExpansionState[r.ExpansionKey] = true;
        }

        var expanded = IlInspectorView.BuildNativeTreeRows(state);
        Assert.Contains(r => r.Kind == IlTreeRowKind.Method && r.Symbol is not null, expanded);
    }

    /// <summary>Verifies a managed binary produces an empty native tree.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BuildNativeTreeRows_Managed_IsEmpty()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.HelloWorldDll);

        Assert.IsEmpty(IlInspectorView.BuildNativeTreeRows(state));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _app?.Dispose();
        _terminal?.Dispose();
    }
}
