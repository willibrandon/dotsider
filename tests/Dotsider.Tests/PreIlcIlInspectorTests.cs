using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Integration tests for the IL Inspector's side-by-side mode after a pre-ILC companion is
/// attached: the managed tree replaces the native tree, <c>t</c> toggles between them, and
/// selecting a correlated method populates the native pair pane.
/// </summary>
[TestClass]
public class PreIlcIlInspectorTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private const string DialogTitle = "Native AOT Sidecars Detected";

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;
    private CancellationTokenSource? _cts;
    private Task? _runTask;

    private (Hex1bTerminal terminal, Hex1bApp app, CancellationToken ct) CreateDotsiderApp(string path, int width, int height)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(width, height)
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

    private async Task<Hex1bTerminalAutomator> AttachAndOpenIlAsync(int width = 160, int height = 40)
    {
        var (terminal, app, ct) = CreateDotsiderApp(Samples.NativeAotConsoleExe!, width, height);
        _runTask = app.RunAsync(ct);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync(DialogTitle);
        await auto.EnterAsync(ct);
        await auto.WaitUntilAsync(_ => _state!.Analyzer.PreIlcCompanions is not null,
            description: "companions attached");
        await auto.KeyAsync(Hex1bKey.D3, ct: ct);
        await auto.WaitUntilAsync(_ => _state!.CurrentTab == TabId.IlInspector,
            description: "IL Inspector active");
        return auto;
    }

    /// <summary>Attached, the IL tree defaults to the managed companion tree and lists its types.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Attached_IlTab_DefaultsToManagedTree()
    {
        TestSkip.When(Samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        await AttachAndOpenIlAsync();

        Assert.IsFalse(Views.IlInspectorView.IsNativeTreeMode(_state!));
        // The managed type tree is visible (Program and Greeter are the sample's types).
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Greeter") || s.ContainsText("Program"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(_terminal!, _cts!.Token);

        _cts!.Cancel();
    }

    /// <summary>The <c>t</c> key toggles the tree between managed and native and back.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Attached_T_TogglesTreeMode()
    {
        TestSkip.When(Samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var auto = await AttachAndOpenIlAsync();

        Assert.IsFalse(_state!.IlAotTreeNativeView);
        await auto.KeyAsync(Hex1bKey.T, ct: _cts!.Token);
        await auto.WaitUntilAsync(_ => _state!.IlAotTreeNativeView, description: "toggled to native tree");
        Assert.IsTrue(Views.IlInspectorView.IsNativeTreeMode(_state));

        await auto.KeyAsync(Hex1bKey.T, ct: _cts!.Token);
        await auto.WaitUntilAsync(_ => !_state!.IlAotTreeNativeView, description: "toggled back to managed tree");
        Assert.IsFalse(Views.IlInspectorView.IsNativeTreeMode(_state));

        _cts!.Cancel();
    }

    /// <summary>Selecting a correlated method populates the native pair pane beside the IL.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Attached_SelectCorrelatedMethod_PopulatesPairNative()
    {
        TestSkip.When(Samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        // Wide terminal so the right pane splits (above the narrow-collapse threshold).
        var auto = await AttachAndOpenIlAsync(width: 200, height: 50);

        _state!.EnsureManagedNativeIndexAsync();
        await auto.WaitUntilAsync(_ => _state!.PreIlcIndex is not null, description: "index built");

        var correlated = _state.PreIlcIndex!.Methods.FirstOrDefault(m =>
            m.Status == MethodCorrelationStatus.CorrelatedExact
            && m.NativeSymbols.Count > 0
            && m.NativeSymbols[0].FileOffset is not null);
        TestSkip.When(correlated is null, "no exact correlation with a native symbol on this leg");

        var owner = _state.Analyzer.PreIlcCompanions!.FindByAssemblyName(correlated!.AssemblyName);
        var ownerArg = owner is not null && !ReferenceEquals(owner, _state.Analyzer.PreIlcCompanions!.Root)
            ? owner
            : null;
        _state.NavigateToPreIlcMethod(correlated.Method, ownerArg);

        await auto.WaitUntilAsync(_ => _state!.IlPairNativeEditorState is not null,
            description: "pair native pane populated");
        await auto.WaitUntilAsync(_ =>
                GetEditorNode(_state!.IlEditorState)?.Bounds.Width > 0
                && GetEditorNode(_state.IlPairNativeEditorState)?.Bounds.Width > 0,
            description: "pair editors arranged");

        Assert.IsNotNull(_state.IlPairNativeEditorState);
        var ilWidth = GetEditorNode(_state.IlEditorState)!.Bounds.Width;
        var nativeWidth = GetEditorNode(_state.IlPairNativeEditorState)!.Bounds.Width;
        Assert.IsInRange(0, 1, Math.Abs(ilWidth - nativeWidth));

        _cts!.Cancel();
    }

    /// <summary>Declined, the IL tree stays the native function tree.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Declined_IlTab_ShowsNativeTree()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var (terminal, app, ct) = CreateDotsiderApp(Samples.NativeAotConsoleExe!, 160, 40);
        _runTask = app.RunAsync(ct);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync(DialogTitle);
        await auto.EscapeAsync(ct);
        await auto.WaitUntilAsync(s => !s.ContainsText(DialogTitle), description: "offer declined");
        await auto.KeyAsync(Hex1bKey.D3, ct: ct);
        await auto.WaitUntilAsync(s => s.ContainsText("(functions)") || s.ContainsText("(runtime)"),
            description: "native function tree rendered");

        Assert.IsTrue(Views.IlInspectorView.IsNativeTreeMode(_state!));

        _cts!.Cancel();
    }

    /// <summary>Disposes test resources.</summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _cts?.Cancel();
        try { _runTask?.Wait(TimeSpan.FromSeconds(5)); }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static e => e is OperationCanceledException)) { }
        catch (OperationCanceledException) { }
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
    }

    private EditorNode? GetEditorNode(EditorState? editorState)
        => _state?.App.Focusables
            .OfType<EditorNode>()
            .FirstOrDefault(node => ReferenceEquals(node.State, editorState));
}
