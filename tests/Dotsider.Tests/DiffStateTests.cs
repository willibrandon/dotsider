using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class DiffStateTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _app;

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

    [Fact(Timeout = 5_000)]
    public void Construct_BothAnalyzersAccessible()
    {
        var app = CreateApp();
        using var state = new DiffState(app, samples.RichLibraryDll, samples.RichLibraryV2Dll);
        Assert.NotNull(state.Left);
        Assert.NotNull(state.Right);
        Assert.Equal("RichLibrary", state.Left.AssemblyName);
        Assert.Equal("RichLibrary", state.Right.AssemblyName);
    }

    [Fact(Timeout = 5_000)]
    public void Construct_DiffResultPopulated()
    {
        var app = CreateApp();
        using var state = new DiffState(app, samples.RichLibraryDll, samples.RichLibraryV2Dll);
        Assert.NotNull(state.DiffResult);
        Assert.NotEmpty(state.DiffResult.TypeDiffs);
    }

    [Fact(Timeout = 5_000)]
    public void DefaultFilterMode_IsAll()
    {
        var app = CreateApp();
        using var state = new DiffState(app, samples.RichLibraryDll, samples.RichLibraryV2Dll);
        Assert.Equal(DiffFilterMode.All, state.FilterMode);
    }

    [Fact(Timeout = 5_000)]
    public void TabSwitching_Works()
    {
        var app = CreateApp();
        using var state = new DiffState(app, samples.RichLibraryDll, samples.RichLibraryV2Dll);
        state.CurrentTab = 2;
        Assert.Equal(2, state.CurrentTab);
    }

    [Fact(Timeout = 5_000)]
    public void Dispose_CleansUp()
    {
        var app = CreateApp();
        var state = new DiffState(app, samples.RichLibraryDll, samples.RichLibraryV2Dll);
        state.Dispose();
        state.Dispose(); // idempotent — should not throw
    }

    public void Dispose()
    {
        _app?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        GC.SuppressFinalize(this);
    }
}
