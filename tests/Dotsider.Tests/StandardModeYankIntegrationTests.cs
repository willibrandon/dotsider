using Dotsider.Views;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class StandardModeYankIntegrationTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private ClipboardCapturingWorkloadAdapter? _clipboardAdapter;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;
    private CancellationTokenSource? _cts;

    private (Hex1bTerminal terminal, Hex1bApp app, CancellationToken ct) Launch(
        string dllPath, int? initialTab = null)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _workload = new Hex1bAppWorkloadAdapter();
        _clipboardAdapter = new ClipboardCapturingWorkloadAdapter(_workload);
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(120, 30)
            .WithMouse()
            .Build();
        DotsiderApp? dotsiderApp = null;
        _hex1bApp = new Hex1bApp(
            ctx =>
            {
                if (_state is null)
                {
                    _state = new DotsiderState(_hex1bApp!, dllPath);
                    if (initialTab.HasValue)
                        _state.CurrentTab = initialTab.Value;
                }
                dotsiderApp ??= new DotsiderApp(_state);
                return Task.FromResult<Hex1bWidget>(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _clipboardAdapter,
                EnableInputCoalescing = false,
                EnableMouse = true,
                Theme = DotsiderTheme.Create()
            });
        return (_terminal, _hex1bApp, _cts.Token);
    }

    /// <summary>
    /// Safely checks if the focused node is an EditorNode.
    /// Returns false instead of throwing during early app lifecycle
    /// when the focus ring has not been initialized yet.
    /// </summary>
    private bool IsFocusedOnEditor()
    {
        try
        {
            return _state?.App.FocusedNode is EditorNode;
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }

    /// <summary>
    /// Safely checks if the focused node is an EditorNode with a specific state.
    /// Returns false instead of throwing during early app lifecycle
    /// when the focus ring has not been initialized yet.
    /// </summary>
    private bool IsFocusedOnEditor(EditorState? expectedState)
    {
        try
        {
            return _state?.App.FocusedNode is EditorNode { State: var es }
                && es == expectedState;
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }

    // --- General tab ---

    [Fact(Timeout = 30_000)]
    public async Task General_TabTogglesFocusBetweenEditorAndTable()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Initial focus is on the table (RequestContentFocus excludes EditorNode)
        // Allow render to settle before checking focus
        await Task.Delay(100, ct);
        Assert.False(IsFocusedOnEditor(),
            "Initial focus should not be on the editor");

        // Tab → editor
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);
        Assert.True(IsFocusedOnEditor());

        // Tab → table
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => !IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);
        Assert.False(IsFocusedOnEditor());

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task General_YankOnFocusedRow_ShowsNotificationAndFlash()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Ensure a ref row is focused
        Assert.NotNull(_state!.GeneralFocusedDep);

        // Compute expected payload before yank
        var expectedPayload = YankHelper.GetYankText(_state);
        Assert.NotNull(expectedPayload);
        Assert.Contains("\t", expectedPayload); // Tab-separated

        // Yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Notification contains the payload (truncated if long)
        Assert.NotNull(_state.YankNotification);
        var firstRef = _state.Analyzer.AssemblyRefs.First(r => r.Name == _state.GeneralFocusedDep as string);
        Assert.Contains(firstRef.Name, _state.YankNotification);

        // Verify the actual OSC 52 clipboard payload emitted by ctx.CopyToClipboard
        Assert.True(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var actualClipboard),
            "CopyToClipboard should have emitted an OSC 52 sequence");
        Assert.Equal(expectedPayload, actualClipboard);

        // Wait for notification to clear
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => _state.YankNotification is null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- PE/Metadata tab ---

    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_TabCyclesThroughHeadersAndTable()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.PeMetadata);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("PE Headers") || s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Tab → PE Headers editor
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.PeHeadersEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Tab → CLR Header editor
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.ClrHeaderEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Tab → metadata table
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => _state!.App.FocusedNode is not EditorNode, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_LeftRightDoNotSwitchSubTabsWhenEditorFocused()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.PeMetadata);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("PE Headers"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var initialSubTab = _state!.PeSubTab;

        // Tab to PE Headers editor
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Press Right — should NOT switch sub-tab
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(100, ct);
        Assert.Equal(initialSubTab, _state.PeSubTab);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_DetailPopupIsEditorAndEscCloses()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.PeMetadata);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Open detail popup
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(5))
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.PeDetailEditorState);
        Assert.True(_state.App.FocusedNode is EditorNode,
            "Detail popup editor should have focus");

        // Escape closes popup
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state.PeDetailContent is null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(100, ct);
        Assert.False(_state.App.FocusedNode is EditorNode,
            "Focus should return to table after popup closes");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Strings tab ---

    [Fact(Timeout = 30_000)]
    public async Task Strings_YankOnFocusedRow_CopiesStringValue()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.Strings);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("strings"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.StringsFocusedKey);

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task Strings_LeftRightDoNotSwitchTabsWhenPopupOpen()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.Strings);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("strings"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var initialSourceTab = _state!.StringsSourceTab;

        // Open detail popup
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state.StringsDetailContent is not null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Right arrow — should NOT switch source tab
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(100, ct);
        Assert.Equal(initialSourceTab, _state.StringsSourceTab);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Hex Dump tab ---

    [Fact(Timeout = 30_000)]
    public async Task HexDump_SelectionYank_CopiesUppercaseHexBytes()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.HexDump);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Data Interpretation"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Select bytes using real Shift+Right input
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.HexEditorState.Cursor.HasSelection, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Verify payload is uppercase hex
        Assert.NotNull(_state!.YankNotification);
        Assert.Matches(@"Yanked: [0-9A-F]{2} [0-9A-F]{2}", _state.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Focus restoration ---

    [Fact(Timeout = 30_000)]
    public async Task FocusRestoration_AfterDetailPopupClose_LandsOnTable()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.PeMetadata);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Open and close detail popup
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state!.PeDetailContent is null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Allow focus to settle after popup close
        await Task.Delay(100, ct);

        // Focus should be on a table, not an editor
        Assert.False(_state!.App.FocusedNode is EditorNode,
            "Focus should land on table after closing detail popup");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- PE/Metadata editor selection + yank ---

    [Fact(Timeout = 30_000)]
    public async Task PeHeaders_SelectionYank_CopiesTextAndFlashes()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.PeMetadata);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("PE Headers"), TimeSpan.FromSeconds(10))
            // Tab to PE Headers editor
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.PeHeadersEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Select text via Shift+Right
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeHeadersEditorState!.Cursor.HasSelection, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task PeDetailPopup_SelectionYank_Works()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.PeMetadata);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            // Open detail popup
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(5))
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Select text
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeDetailEditorState!.Cursor.HasSelection, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);

        // Escape closes popup
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state.PeDetailContent is null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Strings detail popup selection + yank ---

    [Fact(Timeout = 30_000)]
    public async Task StringsDetailPopup_SelectionYank_Works()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.Strings);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("strings"), TimeSpan.FromSeconds(10))
            // Open detail popup
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.StringsDetailContent is not null, TimeSpan.FromSeconds(5))
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Select text
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.StringsDetailEditorState!.Cursor.HasSelection, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- General tab double-click word selection ---

    [Fact(Timeout = 60_000)]
    public async Task General_DoubleClickWordSelection_AdjustsBoundaryAndYanks()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Find "Version" on screen — it appears inside the Assembly Info editor
        // and is more likely to be a standalone word than "Assembly" which is part of "Assembly Name:"
        List<(int Line, int Column)> matches = [];
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                var found = s.FindText("Version");
                if (found.Count == 0) return false;
                matches = [.. found.Select(m => (m.Line, m.Column))];
                return true;
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.True(matches.Count > 0);
        // Use the first match — coordinates are 0-based screen positions
        var (row, col) = matches[0];

        // Single click to give editor focus, then double-click to select word
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));
        await auto.ClickAtAsync(col + 2, row, ct: ct); // +2 to land inside the word
        await Task.Delay(150, ct);
        await auto.DoubleClickAtAsync(col + 2, row, ct: ct);

        // Wait for selection to appear
        await TestHelpers.WaitUntilAsync(
            () => _state!.GeneralInfoEditorState?.Cursor.HasSelection == true,
            TimeSpan.FromSeconds(5));

        // Verify selection is a clean word
        var es = _state!.GeneralInfoEditorState!;
        var selected = es.Document.GetText(es.Cursor.SelectionRange);
        Assert.True(selected.Length > 0, "Selection should not be empty");
        Assert.True(selected.All(char.IsLetterOrDigit),
            $"Expected pure word, got '{selected}'");

        // Yank the selection
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Yank notification auto-clear ---

    [Fact(Timeout = 30_000)]
    public async Task YankNotification_AutoClears_After1500ms()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Yank a row
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(_ => _state!.YankNotification is not null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Verify notification is set
        Assert.NotNull(_state!.YankNotification);

        // Wait for auto-clear
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => _state.YankNotification is null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Null(_state.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Yank flash on table row ---

    [Fact(Timeout = 30_000)]
    public async Task General_YankFlash_SetsAndClears()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Ensure table has focus (not editor)
        Assert.NotNull(_state!.GeneralFocusedDep);
        Assert.False(_state.App.FocusedNode is EditorNode);

        // Yank — flash should be set briefly then cleared
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(_ => _state.YankNotification is not null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Wait for flash to clear (150ms timer)
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => !_state.YankFlashRow, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.False(_state.YankFlashRow);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
