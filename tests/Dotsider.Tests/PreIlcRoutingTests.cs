using Hex1b;
using Hex1b.Automation;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests that attaching a pre-ILC companion routes the metadata-driven surfaces to the managed
/// assembly while the binary stays native: <see cref="DotsiderState.MetadataAnalyzer"/> flips to
/// the companion root, its managed types become visible, the correlation index builds, and detach
/// restores native routing.
/// </summary>
[Collection("SampleAssemblies")]
public class PreIlcRoutingTests(SampleAssemblyFixture samples) : IDisposable
{
    private const string DialogTitle = "Native AOT Sidecars Detected";

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;
    private CancellationTokenSource? _cts;

    private (Hex1bTerminal terminal, Hex1bApp app, CancellationToken ct) CreateDotsiderApp(string path)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
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
                _state ??= new DotsiderState(_hex1bApp!, path);
                dotsiderApp ??= new DotsiderApp(_state);
                return Task.FromResult<Hex1bWidget>(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions { WorkloadAdapter = _workload, EnableInputCoalescing = false });
        return (_terminal, _hex1bApp, _cts.Token);
    }

    private async Task<Hex1bTerminalAutomator> AttachAsync()
    {
        var (terminal, app, ct) = CreateDotsiderApp(samples.NativeAotConsoleExe!);
        _ = app.RunAsync(ct);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync(DialogTitle);
        await auto.EnterAsync(ct);
        await auto.WaitUntilAsync(_ => _state!.Analyzer.PreIlcCompanions is not null,
            description: "companions attached");
        return auto;
    }

    /// <summary>Before attaching, the metadata analyzer is the native analyzer itself.</summary>
    [Fact(Timeout = 60_000)]
    public async Task Detached_MetadataAnalyzer_IsNativeAnalyzer()
    {
        Assert.SkipWhen(samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var (terminal, app, ct) = CreateDotsiderApp(samples.NativeAotConsoleExe!);
        _ = app.RunAsync(ct);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync(DialogTitle);
        await auto.EscapeAsync(ct);
        await auto.WaitUntilAsync(s => !s.ContainsText(DialogTitle), description: "offer declined");

        Assert.Same(_state!.Analyzer, _state.MetadataAnalyzer);

        _cts!.Cancel();
    }

    /// <summary>Attaching routes metadata to the managed companion, exposing its managed types.</summary>
    [Fact(Timeout = 60_000)]
    public async Task Attached_MetadataAnalyzer_IsCompanionRootWithManagedTypes()
    {
        Assert.SkipWhen(samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        await AttachAsync();

        Assert.NotSame(_state!.Analyzer, _state.MetadataAnalyzer);
        Assert.True(_state.MetadataAnalyzer.HasMetadata);
        Assert.Equal("NativeAotConsole", _state.MetadataAnalyzer.AssemblyName);
        Assert.Contains(_state.MetadataAnalyzer.TypeDefs, t => t.Name == "Greeter");
        // The binary stays native.
        Assert.True(_state.IsNativeAot);

        _cts!.Cancel();
    }

    /// <summary>The correlation index builds over the companion set after attach.</summary>
    [Fact(Timeout = 60_000)]
    public async Task Attached_CorrelationIndex_Builds()
    {
        Assert.SkipWhen(samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var auto = await AttachAsync();
        _state!.EnsureManagedNativeIndexAsync();
        await auto.WaitUntilAsync(_ => _state!.PreIlcIndex is not null,
            description: "correlation index built");

        Assert.NotNull(_state.PreIlcIndex);
        Assert.True(_state.PreIlcIndex!.Methods.Count > 0);

        _cts!.Cancel();
    }

    /// <summary>Detaching restores native metadata routing and clears the index.</summary>
    [Fact(Timeout = 60_000)]
    public async Task Detach_RestoresNativeRouting()
    {
        Assert.SkipWhen(samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var auto = await AttachAsync();
        _state!.DetachPreIlc();
        await auto.WaitUntilAsync(_ => _state!.Analyzer.PreIlcCompanions is null,
            description: "companions detached");

        Assert.Same(_state.Analyzer, _state.MetadataAnalyzer);
        Assert.Null(_state.PreIlcIndex);

        _cts!.Cancel();
    }

    /// <summary>Disposes test resources.</summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _cts?.Cancel();
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
    }
}
