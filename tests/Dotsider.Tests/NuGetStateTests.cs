using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class NuGetStateTests(SampleAssemblyFixture samples) : IDisposable
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
    public void Construct_PackageMetadataPopulated()
    {
        var app = CreateApp();
        using var state = new NuGetState(app, samples.RichLibraryNupkg);
        Assert.Equal("RichLibrary", state.Package.PackageId);
        Assert.Equal("2.5.1", state.Package.PackageVersion);
    }

    [Fact(Timeout = 5_000)]
    public void Construct_FileListPopulated()
    {
        var app = CreateApp();
        using var state = new NuGetState(app, samples.RichLibraryNupkg);
        Assert.NotEmpty(state.Package.Files);
        Assert.NotEmpty(state.Package.DllFiles);
    }

    [Fact(Timeout = 5_000)]
    public void IsBrowsingPackage_DefaultTrue()
    {
        var app = CreateApp();
        using var state = new NuGetState(app, samples.RichLibraryNupkg);
        Assert.True(state.IsBrowsingPackage);
    }

    [Fact(Timeout = 5_000)]
    public void DrillInto_DllCreatesInspectorState()
    {
        var app = CreateApp();
        using var state = new NuGetState(app, samples.RichLibraryNupkg);
        var dll = state.Package.DllFiles.First();
        var analyzer = state.Package.OpenDll(dll);
        state.SelectedDllState = new DotsiderState(app, analyzer);
        state.SelectedDllEntry = dll;
        state.IsBrowsingPackage = false;
        Assert.False(state.IsBrowsingPackage);
        Assert.NotNull(state.SelectedDllState);
    }

    [Fact(Timeout = 5_000)]
    public void Dispose_CleansUp()
    {
        var app = CreateApp();
        var state = new NuGetState(app, samples.RichLibraryNupkg);
        state.Dispose();
        state.Dispose(); // idempotent
    }

    public void Dispose()
    {
        _app?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        GC.SuppressFinalize(this);
    }
}
