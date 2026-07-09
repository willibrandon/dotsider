using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Reproduces #90: opening a detail popup in PE/Metadata resets the table's
/// scroll position to the top. The viewport content should stay put.
/// </summary>
[TestClass]
public class TableScrollPopupTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;

    private (Hex1bTerminal terminal, Hex1bApp app) CreateDotsiderApp(int startTab, int subTab)
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
                _state ??= new DotsiderState(_hex1bApp!, Samples.RichLibraryDll)
                {
                    CurrentTab = startTab,
                    PeSubTab = subTab
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
    /// Verifies pe metadata scroll position preserved when popup opens.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task PeMetadata_ScrollPositionPreservedWhenPopupOpens()
    {
        // MethodDef sub-tab: RichLibrary has many methods, enough to scroll
        var (terminal, app) = CreateDotsiderApp(TabId.PeMetadata, PeSubTabId.MethodDef);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        var runTask = app.RunAsync(cts.Token);

        // We use the second row's token as a scroll marker. When we scroll
        // far enough down, this token leaves the viewport. If the popup
        // resets scroll to the top, this token reappears.
        string? secondRowToken = null;

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("MethodDef"), TimeSpan.FromSeconds(10))
            // Find the second data row's token (MethodDef tokens start with 0x06)
            .WaitUntil(s =>
            {
                var matches = s.FindPattern(@"0x06[0-9A-F]{6}");
                if (matches.Count >= 2)
                {
                    secondRowToken = matches[1].Text;
                    return true;
                }
                return false;
            }, TimeSpan.FromSeconds(10))
            // Scroll to the bottom of the table so early rows leave the viewport
            .Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow).Key(Hex1bKey.DownArrow)
            .WaitUntil(s => !s.ContainsText(secondRowToken!), TimeSpan.FromSeconds(10))
            // Open the detail popup on the now-focused (last) row
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Detail"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // If scroll reset to top, the second row token would reappear
        var snapshot = terminal.CreateSnapshot();
        Assert.IsFalse(snapshot.ContainsText(secondRowToken!),
            $"Table scrolled back to top when popup opened — row {secondRowToken} reappeared");

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
    }
}
