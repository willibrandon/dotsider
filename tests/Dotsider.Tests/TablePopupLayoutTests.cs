using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Reproduces #88: opening a detail popup in the Strings or PE/Metadata tab causes
/// the underlying table to shrink its bottom border to just below the last data row.
/// </summary>
[Collection("SampleAssemblies")]
public class TablePopupLayoutTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;

    private (Hex1bTerminal terminal, Hex1bApp app) CreateDotsiderApp(int startTab)
    {
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
                _state ??= new DotsiderState(_hex1bApp!, samples.RichLibraryDll)
                {
                    CurrentTab = startTab
                };
                dotsiderApp ??= new DotsiderApp(_state);
                return Task.FromResult<Hex1bWidget>(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
            });
        return (_terminal, _hex1bApp);
    }

    /// <summary>
    /// Verifies strings table bottom border does not move when popup opens.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Strings_TableBottomBorderDoesNotMoveWhenPopupOpens()
    {
        var (terminal, app) = CreateDotsiderApp(TabId.Strings);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);

        int bottomBorderRowBefore = -1;
        int bottomBorderRowDuring = -1;

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("strings"), TimeSpan.FromSeconds(10))
            // Record the table bottom border row before the popup
            .WaitUntil(s =>
            {
                var positions = s.FindText("\u2514"); // └ bottom-left corner
                if (positions.Count > 0)
                {
                    bottomBorderRowBefore = positions[^1].Line;
                    return true;
                }
                return false;
            }, TimeSpan.FromSeconds(10))
            // Open the detail popup
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.StringsDetailContent is not null, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("String Detail"), TimeSpan.FromSeconds(10))
            // Record the table bottom border row with the popup open
            .WaitUntil(s =>
            {
                var positions = s.FindText("\u2514"); // └ bottom-left corner
                if (positions.Count > 0)
                {
                    // The table's └ is the last one on screen (popup's └ may appear too)
                    bottomBorderRowDuring = positions[^1].Line;
                    return true;
                }
                return false;
            }, TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.True(bottomBorderRowBefore > 0, "Could not find table bottom border before popup");
        Assert.Equal(bottomBorderRowBefore, bottomBorderRowDuring);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata table bottom border does not move when popup opens.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_TableBottomBorderDoesNotMoveWhenPopupOpens()
    {
        var (terminal, app) = CreateDotsiderApp(TabId.PeMetadata);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);

        int bottomBorderRowBefore = -1;
        int bottomBorderRowDuring = -1;

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            // Record the table bottom border row before the popup
            .WaitUntil(s =>
            {
                var positions = s.FindText("\u2514"); // └ bottom-left corner
                if (positions.Count > 0)
                {
                    bottomBorderRowBefore = positions[^1].Line;
                    return true;
                }
                return false;
            }, TimeSpan.FromSeconds(10))
            // Open the detail popup
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Detail"), TimeSpan.FromSeconds(10))
            // Record the table bottom border row with the popup open
            .WaitUntil(s =>
            {
                var positions = s.FindText("\u2514"); // └ bottom-left corner
                if (positions.Count > 0)
                {
                    bottomBorderRowDuring = positions[^1].Line;
                    return true;
                }
                return false;
            }, TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.True(bottomBorderRowBefore > 0, "Could not find table bottom border before popup");
        Assert.Equal(bottomBorderRowBefore, bottomBorderRowDuring);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        GC.SuppressFinalize(this);
    }
}
