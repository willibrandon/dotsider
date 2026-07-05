using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Integration tests for the pre-ILC sidecar offer dialog: it appears only for an attachable
/// Native AOT binary, <c>Enter</c> attaches alongside (never replaces), <c>Esc</c> keeps native
/// only, and the General tab's <c>a</c>/<c>d</c> keys re-offer and detach.
/// </summary>
[Collection("SampleAssemblies")]
public class PreIlcOfferTests(SampleAssemblyFixture samples) : IDisposable
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

    /// <summary>The offer dialog appears (and only it, never the apphost dialog) for an attachable Native AOT binary.</summary>
    [Fact(Timeout = 60_000)]
    public async Task NativeAot_Attachable_ShowsOfferDialog()
    {
        Assert.SkipWhen(samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var (terminal, app, ct) = CreateDotsiderApp(samples.NativeAotConsoleExe!);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText(DialogTitle), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.True(_state!.PreIlcDialogOpen);
        Assert.False(_state.ApphostDialogOpen, "the AOT and apphost offers are mutually exclusive");
        Assert.Null(_state.Analyzer.PreIlcCompanions);

        _cts!.Cancel();
    }

    /// <summary>An apphost exe opens the apphost dialog, never the pre-ILC sidecar dialog.</summary>
    [Fact(Timeout = 60_000)]
    public async Task Apphost_ShowsApphostDialog_NotPreIlcDialog()
    {
        var (terminal, app, ct) = CreateDotsiderApp(samples.HelloWorldExe);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Apphost Detected"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.True(_state!.ApphostDialogOpen);
        Assert.False(_state.PreIlcDialogOpen);

        _cts!.Cancel();
    }

    /// <summary>Enter attaches the companion set alongside the native analyzer, never replacing it.</summary>
    [Fact(Timeout = 60_000)]
    public async Task Offer_Enter_AttachesAlongside()
    {
        Assert.SkipWhen(samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var (terminal, app, ct) = CreateDotsiderApp(samples.NativeAotConsoleExe!);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText(DialogTitle), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => !s.ContainsText(DialogTitle), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.False(_state!.PreIlcDialogOpen);
        Assert.NotNull(_state.Analyzer.PreIlcCompanions);
        Assert.True(_state.IsNativeAot, "attaching must not replace the native analyzer");
        Assert.Empty(_state.NavigationStack);

        _cts!.Cancel();
    }

    /// <summary>Escape declines the offer, keeping the binary native-only.</summary>
    [Fact(Timeout = 60_000)]
    public async Task Offer_Escape_KeepsNativeOnly()
    {
        Assert.SkipWhen(samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var (terminal, app, ct) = CreateDotsiderApp(samples.NativeAotConsoleExe!);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText(DialogTitle), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => !s.ContainsText(DialogTitle), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.False(_state!.PreIlcDialogOpen);
        Assert.Null(_state.Analyzer.PreIlcCompanions);
        Assert.True(_state.IsNativeAot);

        _cts!.Cancel();
    }

    /// <summary>After declining, the General tab's <c>a</c> re-opens the offer and <c>d</c> detaches.</summary>
    [Fact(Timeout = 60_000)]
    public async Task General_A_ReoffersAnd_D_Detaches()
    {
        Assert.SkipWhen(samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var (terminal, app, ct) = CreateDotsiderApp(samples.NativeAotConsoleExe!);
        var runTask = app.RunAsync(ct);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        // Decline the initial offer.
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText(DialogTitle), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => !s.ContainsText(DialogTitle), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // General tab is tab 1; press 'a' to re-open the offer, Enter to attach.
        await auto.KeyAsync(Hex1bKey.A, ct: ct);
        await auto.WaitUntilTextAsync(DialogTitle);
        await auto.KeyAsync(Hex1bKey.Enter, ct: ct);
        await auto.WaitUntilAsync(_ => _state!.Analyzer.PreIlcCompanions is not null,
            description: "companions attached after re-offer");

        // 'd' detaches.
        await auto.KeyAsync(Hex1bKey.D, ct: ct);
        await auto.WaitUntilAsync(_ => _state!.Analyzer.PreIlcCompanions is null,
            description: "companions detached");

        Assert.Null(_state!.Analyzer.PreIlcCompanions);

        _cts!.Cancel();
    }

    /// <summary>
    /// An mstat-only discovery — a recognized build tree whose obj holds the mstat but no managed
    /// assembly — is found by the probe yet never opens the dialog (nothing attachable).
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task MstatOnly_NoAttachableCompanion_NoDialog()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        // Build a classic publish tree with the mstat in obj\...\native but NO managed dll —
        // the probe recognizes the tree and finds mstat-only, so HasAttachableCompanion is false.
        var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
        var tempDir = Path.Combine(Path.GetTempPath(), "dotsider-preilc-mstatonly-" + Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(tempDir, "NativeAotConsole");
        var publishDir = Path.Combine(projectDir, "bin", "Release", "net10.0", rid, "publish");
        var objNativeDir = Path.Combine(projectDir, "obj", "Release", "net10.0", rid, "native");
        Directory.CreateDirectory(publishDir);
        Directory.CreateDirectory(objNativeDir);
        try
        {
            var exeCopy = Path.Combine(publishDir, Path.GetFileName(samples.NativeAotConsoleExe!));
            File.Copy(samples.NativeAotConsoleExe!, exeCopy);
            File.Copy(samples.NativeAotConsoleMstat!, Path.Combine(objNativeDir, "NativeAotConsole.mstat"));

            var (terminal, app, ct) = CreateDotsiderApp(exeCopy);
            var runTask = app.RunAsync(ct);

            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
                .WaitUntil(s => s.ContainsText("Native AOT (.NET)"), TimeSpan.FromSeconds(10))
                .Build()
                .ApplyAsync(terminal, ct);

            Assert.False(_state!.PreIlcDialogOpen);
            Assert.False(_state.Analyzer.PreIlcSidecars?.HasAttachableCompanion ?? false);
            Assert.NotNull(_state.Analyzer.PreIlcSidecars?.MstatPath);

            _cts!.Cancel();
        }
        finally
        {
            _state?.Dispose();
            _state = null;
            try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
        }
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
