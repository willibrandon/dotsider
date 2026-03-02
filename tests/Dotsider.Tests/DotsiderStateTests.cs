using Dotsider.Analysis;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class DotsiderStateTests(SampleAssemblyFixture samples) : IDisposable
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
    public void ConstructFromHelloWorld_HasCorrectFileName()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.Equal("HelloWorld.dll", state.Analyzer.FileName);
    }

    [Fact(Timeout = 5_000)]
    public void HasEntryPoint_TrueForExe()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.True(state.HasEntryPoint);
    }

    [Fact(Timeout = 5_000)]
    public void HasEntryPoint_FalseForLibrary()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        Assert.False(state.HasEntryPoint);
    }

    [Fact(Timeout = 5_000)]
    public void HasEntryPoint_TrueForComplexApp()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.ComplexAppDll);
        Assert.True(state.HasEntryPoint);
    }

    [Fact(Timeout = 5_000)]
    public void HasEntryPoint_FalseForEmptyLib()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.EmptyLibDll);
        Assert.False(state.HasEntryPoint);
    }

    [Fact(Timeout = 5_000)]
    public void HasEntryPoint_FalseForNativeLib()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.NativeLibDll);
        Assert.False(state.HasEntryPoint);
    }

    [Fact(Timeout = 5_000)]
    public void IsNativeAot_FalseForAllManagedSamples()
    {
        var app = CreateApp();
        string[] paths = [samples.HelloWorldDll, samples.RichLibraryDll, samples.ComplexAppDll,
            samples.MinimalApiDll, samples.NativeLibDll, samples.EmptyLibDll];
        foreach (var path in paths)
        {
            using var state = new DotsiderState(app, path);
            Assert.False(state.IsNativeAot, $"IsNativeAot should be false for {Path.GetFileName(path)}");
        }
    }

    [Fact(Timeout = 5_000)]
    public void PushAssembly_ChangesAnalyzer()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.Equal("HelloWorld.dll", state.Analyzer.FileName);
        state.PushAssembly(samples.RichLibraryDll);
        Assert.Equal("RichLibrary.dll", state.Analyzer.FileName);
        Assert.Single(state.NavigationStack);
    }

    [Fact(Timeout = 5_000)]
    public void PopAssembly_RestoresPrevious()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        state.PushAssembly(samples.RichLibraryDll);
        Assert.True(state.PopAssembly());
        Assert.Equal("HelloWorld.dll", state.Analyzer.FileName);
        Assert.Empty(state.NavigationStack);
    }

    [Fact(Timeout = 5_000)]
    public void PopAssembly_EmptyStack_ReturnsFalse()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        Assert.False(state.PopAssembly());
    }

    [Fact(Timeout = 5_000)]
    public void GetActiveStrings_ReturnsNonEmpty()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.StringsSourceTab = 1; // Metadata strings
        var strings = state.GetActiveStrings();
        Assert.NotEmpty(strings);
    }

    [Fact(Timeout = 5_000)]
    public void FormatSize_Zero()
    {
        Assert.Equal("0 B", DotsiderState.FormatSize(0));
    }

    [Fact(Timeout = 5_000)]
    public void FormatSize_KB()
    {
        Assert.Equal("1.0 KB", DotsiderState.FormatSize(1024));
    }

    [Fact(Timeout = 5_000)]
    public void FormatSize_MB()
    {
        Assert.Equal("1.0 MB", DotsiderState.FormatSize(1048576));
    }

    [Fact(Timeout = 5_000)]
    public void FormatSize_Bytes()
    {
        Assert.Equal("500 B", DotsiderState.FormatSize(500));
    }

    [Fact(Timeout = 5_000)]
    public void ConstructFromAnalyzer_Works()
    {
        var app = CreateApp();
        var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var state = new DotsiderState(app, analyzer);
        Assert.Equal("RichLibrary.dll", state.Analyzer.FileName);
        Assert.NotNull(state.IlDisassembler);
        Assert.NotNull(state.StringExtractor);
    }

    [Fact(Timeout = 5_000)]
    public void AllProjectTypes_ConstructWithoutError()
    {
        var app = CreateApp();
        string[] paths = [samples.HelloWorldDll, samples.RichLibraryDll, samples.ComplexAppDll,
            samples.MinimalApiDll, samples.NativeLibDll, samples.EmptyLibDll, samples.RichLibraryV2Dll];
        foreach (var path in paths)
        {
            using var state = new DotsiderState(app, path);
            Assert.NotNull(state.Analyzer);
            Assert.NotNull(state.IlDisassembler);
            Assert.NotNull(state.StringExtractor);
        }
    }

    public void Dispose()
    {
        _app?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
    }
}
