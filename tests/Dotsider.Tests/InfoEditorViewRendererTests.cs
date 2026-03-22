using Dotsider.Views;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests that <see cref="InfoEditorViewRenderer"/> blanks filler rows
/// instead of showing vim-style ~ markers.
/// </summary>
public class InfoEditorViewRendererTests : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;

    [Fact(Timeout = 30_000)]
    public async Task InfoRenderer_NeverShowsTilde_InSmallDocument()
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        // Create a tiny 3-line document in a tall (15 rows) editor
        var doc = new Hex1bDocument("Line 1\nLine 2\nLine 3");
        var editorState = new EditorState(doc) { IsReadOnly = true };

        _hex1bApp = new Hex1bApp(
            ctx =>
            {
                var widget = ctx.Border(
                    ctx.Editor(editorState)
                        .WithViewRenderer(InfoEditorViewRenderer.Instance)
                        .FillWidth().FillHeight()
                ).Title(" Info ").Fill();
                return Task.FromResult<Hex1bWidget>(widget);
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
            });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = _hex1bApp.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Line 1"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(_terminal, cts.Token);

        // Verify no tilde markers appear below document content.
        // The border title " Info " and content "Line 1/2/3" don't contain ~.
        // Scan only the editor area (inside the border, rows below the 3 content lines).
        var foundTilde = false;
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                // Check rows 5+ (after border top + 3 content lines) for stray ~
                for (var row = 5; row < 18; row++)
                {
                    var line = s.GetLine(row).TrimEnd();
                    // A tilde line from the default renderer starts with ~ after the border
                    if (line.Contains("~") && !line.Contains("Info"))
                    {
                        foundTilde = true;
                        return true;
                    }
                }
                return true; // always pass — we check foundTilde after
            }, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(_terminal, cts.Token);

        Assert.False(foundTilde, "InfoEditorViewRenderer should not show ~ filler lines");

        cts.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        GC.SuppressFinalize(this);
    }
}
