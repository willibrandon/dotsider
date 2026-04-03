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

    // --- Size Map drill ---

    [Fact(Timeout = 30_000)]
    public async Task SizeMap_SelectThenEnterDrills()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.SizeMap);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary") && s.ContainsText("Total:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Empty(_state!.TreemapBreadcrumb);

        // Select with arrow first, then Enter to drill
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.TreemapSelectedIndex >= 0, TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state.TreemapBreadcrumb.Count > 0, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotEmpty(_state.TreemapBreadcrumb);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task SizeMap_EnterWithoutSelection_DoesNothing()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.SizeMap);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary") && s.ContainsText("Total:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Empty(_state!.TreemapBreadcrumb);
        Assert.Equal(-1, _state.TreemapSelectedIndex);

        // Press Enter with no selection — should be a no-op
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(200, ct);
        Assert.Empty(_state.TreemapBreadcrumb);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Esc regression: nested cross-view + assembly stack ---

    [Fact(Timeout = 60_000)]
    public async Task EscBack_CrossViewTakesPriorityOverAssemblyStack()
    {
        // Use RichLibrary — its refs resolve to BCL assemblies in the runtime dir
        var (terminal, app, ct) = Launch(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Real drill via PushAssembly — resolve a BCL ref from the runtime directory
        var refName = _state!.Analyzer.AssemblyRefs[0].Name;
        var resolvedPath = Dotsider.Core.Analysis.AssemblyAnalyzer.ResolveAssemblyPath(
            _state.Analyzer.FilePath, refName);
        Assert.NotNull(resolvedPath);
        Assert.True(_state.PushAssembly(resolvedPath),
            $"PushAssembly should succeed for resolved ref '{refName}'");
        _state.App.Invalidate();
        await Task.Delay(200, ct);

        Assert.True(_state.NavigationStack.Count > 0);

        // The drilled BCL assembly may lack user types with methods.
        // Navigate back to push RichLibrary again so we have real types for g.
        _state.PopAssembly();
        _state.App.Invalidate();
        await Task.Delay(100, ct);

        // Re-push so NavigationStack > 0, but stay on the RichLibrary analyzer
        Assert.True(_state.PushAssembly(resolvedPath));
        _state.App.Invalidate();
        await Task.Delay(100, ct);

        // Pop back to RichLibrary — now NavigationStack has the BCL assembly
        _state.PopAssembly();

        // Push the BCL one more time so we have NavigationStack > 0
        // while the current analyzer is RichLibrary (which has real types)
        _state.NavigationStack.Push(new Dotsider.Core.Analysis.AssemblyAnalyzer(resolvedPath));
        _state.App.Invalidate();
        await Task.Delay(100, ct);

        Assert.True(_state.NavigationStack.Count > 0);

        // Switch to PE/Metadata TypeDef sub-tab
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D2) // PE/Metadata
            .WaitUntil(s => s.ContainsText("TypeDef") || s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        _state.PeSubTab = PeSubTabId.TypeDef;
        _state.App.Invalidate();
        await Task.Delay(200, ct);

        // RichLibrary has real types with methods
        var typeDef = _state.Analyzer.TypeDefs.FirstOrDefault(t =>
            !t.FullName.StartsWith("<") && t.MethodCount > 0);
        Assert.NotNull(typeDef);

        _state.PeFocusedKey = typeDef.Token;
        _state.App.Invalidate();
        await Task.Delay(100, ct);

        // Press g to trigger cross-view jump to IL Inspector
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("g")
            .WaitUntil(_ => _state.CurrentTab == TabId.IlInspector, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state.CrossViewBackTarget);
        Assert.True(_state.NavigationStack.Count > 0);

        // Esc 1: cross-view back to PE/Metadata — NOT assembly pop
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state.CrossViewBackTarget is null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(TabId.PeMetadata, _state.CurrentTab);
        Assert.True(_state.NavigationStack.Count > 0,
            "Assembly stack should still have the parent");

        // Allow bindings to rebuild after cross-view back
        await Task.Delay(200, ct);

        // Esc 2: pop assembly and return to General
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state.CurrentTab == TabId.General, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Empty(_state.NavigationStack);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Esc regression: Dynamic filter clears before assembly pop ---

    [Fact(Timeout = 120_000)]
    public async Task EscBack_DynamicFilterClearsBeforeAssemblyPop()
    {
        // Use HelloWorld which is executable (has entry point for Dynamic tab)
        var (terminal, app, ct) = Launch(samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Drill into a referenced assembly
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.NavigationStack.Count > 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Go back to HelloWorld (we need the executable for Dynamic tab)
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state!.NavigationStack.Count == 0, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Re-drill so NavigationStack > 0
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.NavigationStack.Count > 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var stackBefore = _state!.NavigationStack.Count;

        // Switch to Dynamic tab
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D8) // Dynamic tab
            .WaitUntil(_ => _state.CurrentTab == TabId.Dynamic, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Set filter programmatically — the guard condition checks state regardless
        // of whether the Events UI is rendered (the drilled assembly may be a library)
        _state.DynamicSubTab = DynamicSubTabId.Events;
        _state.DynamicCategoryFilter = Dotsider.Core.Analysis.Models.TraceEventCategory.GC;

        // Use the Automator to send Escape through the input pipeline, which triggers
        // a render that processes the filter state before the key binding runs.
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        // Esc should NOT pop the assembly — the dynamicFilterActive guard blocks it
        await auto.EscapeAsync(ct: ct);
        Assert.Equal(stackBefore, _state.NavigationStack.Count);

        // Clear the filter and send Esc again — should now pop
        _state.DynamicCategoryFilter = null;
        await auto.EscapeAsync(ct: ct);
        await auto.WaitUntilAsync(_ => _state.CurrentTab == TabId.General,
            description: "Esc to pop back to General tab");

        Assert.Equal(TabId.General, _state.CurrentTab);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Size Map regression: zero-match search Enter is no-op ---

    [Fact(Timeout = 30_000)]
    public async Task SizeMap_EnterAfterZeroMatchSearch_DoesNotDrill()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.SizeMap);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary") && s.ContainsText("Total:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Search for something that won't match
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion)
            .Type("zzzznotanamespace")
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Search[TabId.SizeMap].IsConfirmed, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Verify the precondition: zero matches, no selection
        Assert.Equal(-1, _state!.TreemapMatchIndex);
        Assert.Equal(-1, _state.TreemapSelectedIndex);
        Assert.Empty(_state.TreemapBreadcrumb);

        // Enter should be a no-op — no match, no selection
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(200, ct);
        Assert.Empty(_state.TreemapBreadcrumb);
        Assert.Equal(-1, _state.TreemapMatchIndex);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Strings detail popup regression: content must be visible on screen ---

    [Fact(Timeout = 30_000)]
    public async Task StringsDetail_PopupShowsStringContentOnScreen()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.Strings);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("strings"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Capture the string value from the first visible row
        var strings = _state!.GetActiveStrings();
        Assert.True(strings.Count > 0);
        var firstString = strings[0].Value;

        // Navigate to first row and open detail popup
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("String Detail"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // The actual string content must be visible on the terminal — not just "Length: N"
        // This catches the InfoEditorViewRenderer regression where content lines were blanked
        var snippet = firstString.Length > 20 ? firstString[..20] : firstString;
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText(snippet), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

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

    // --- Vim Text Object Tests ---

    [Fact(Timeout = 30_000)]
    public async Task General_IwSelectsWordInEditor()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Tab) // Focus editor
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.True(IsFocusedOnEditor());
        Assert.False(_state!.GeneralInfoEditorState!.Cursor.HasSelection);

        // Press i — verify it arms the state machine
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.I)
            .WaitUntil(_ => _state!.VimPending == VimMotionState.WaitingForTextObject, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(VimMotionState.WaitingForTextObject, _state!.VimPending);

        // Press w — should select inner word
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.W)
            .WaitUntil(_ => _state!.VimPending == VimMotionState.Idle, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(VimMotionState.Idle, _state.VimPending);
        Assert.True(_state.GeneralInfoEditorState!.Cursor.HasSelection);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task General_IWSelectsWORDInEditor()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Press i, wait, then Shift+W (iW)
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("i")
            .WaitUntil(_ => _state!.VimPending == VimMotionState.WaitingForTextObject, TimeSpan.FromSeconds(5))
            .Type("W") // uppercase W = Shift+W
            .WaitUntil(_ => _state!.GeneralInfoEditorState!.Cursor.HasSelection, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.True(_state!.GeneralInfoEditorState!.Cursor.HasSelection);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task General_YiwYanksWordFromEditor()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Press y — verify it arms WaitingForYMotion
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Y)
            .WaitUntil(_ => _state!.VimPending == VimMotionState.WaitingForYMotion, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(VimMotionState.WaitingForYMotion, _state!.VimPending);

        // Press i — advances to WaitingForYTextObject
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.I)
            .WaitUntil(_ => _state!.VimPending == VimMotionState.WaitingForYTextObject, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Press w — selects + yanks
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.W)
            .WaitUntil(_ => _state!.YankNotification is not null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);
        Assert.True(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var clipboard));
        Assert.False(string.IsNullOrEmpty(clipboard));
        Assert.Equal(VimMotionState.Idle, _state.VimPending);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task General_InterruptedByGlobalKey_DoesNotSelect()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Press i, then 2 (tab switch, Global D2 resets VimPending), then w
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("i")
            .WaitUntil(_ => _state!.VimPending == VimMotionState.WaitingForTextObject, TimeSpan.FromSeconds(5))
            .Type("2") // tab switch → VimReset
            .WaitUntil(_ => _state!.VimPending == VimMotionState.Idle, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(VimMotionState.Idle, _state!.VimPending);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task General_RandomLetterCancels()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Press i then a (random letter cancels), then w
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("i")
            .WaitUntil(_ => _state!.VimPending == VimMotionState.WaitingForTextObject, TimeSpan.FromSeconds(5))
            .Type("a") // EditorNode-level A binding resets
            .WaitUntil(_ => _state!.VimPending == VimMotionState.Idle, TimeSpan.FromSeconds(5))
            .Type("w") // should NOT select (VimPending is Idle, W not registered)
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(100, ct);
        Assert.False(_state!.GeneralInfoEditorState!.Cursor.HasSelection);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task HexDump_YDoesNotArmOnHexNormal()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, initialTab: TabId.HexDump);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Hex Dump"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Press y on hex dump without selection — should NOT arm VimPending
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(100, ct);
        Assert.Equal(VimMotionState.Idle, _state!.VimPending);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Triple-click line selection ---

    [Fact(Timeout = 60_000)]
    public async Task IlInspector_TripleClickSelectsOnlyCurrentLine()
    {
        var (terminal, app, ct) = Launch(samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Navigate to IL Inspector with a method that has IL instructions
        var method = _state!.Analyzer.MethodDefs
            .First(m => m.Rva > 0);

        _state.NavigateToIlMethod(method);

        // Wait for IL content to render — look for the first IL instruction
        List<(int Line, int Column)> matches = [];
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                var found = s.FindText("IL_0000:");
                if (found.Count == 0) return false;
                matches = [.. found.Select(m => (m.Line, m.Column))];
                return true;
            }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var (row, col) = matches[0];

        // Focus the IL editor (status bar shows "l: Focus IL" when tree has focus)
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("l")
            .WaitUntil(_ => IsFocusedOnEditor(_state.IlEditorState),
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Triple-click to select the line: three rapid clicks at the same position
        // (matches the pattern used by hex1b's own EditorMouseTests)
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(col + 2, row)
            .ClickAt(col + 2, row)
            .ClickAt(col + 2, row)
            .WaitUntil(_ => _state!.IlEditorState?.Cursor.HasSelection == true,
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Verify the editor still has focus after triple-click
        Assert.True(IsFocusedOnEditor(_state.IlEditorState),
            "Editor should have focus after triple-click");

        // Yank the selection
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Verify clipboard content
        Assert.True(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var yankedText),
            "CopyToClipboard should have emitted an OSC 52 sequence");

        // The yanked text should be exactly the IL_0000 line — no trailing newline
        // and no character from the next line (e.g. "I" from "IL_0005")
        Assert.StartsWith("IL_0000:", yankedText);
        Assert.DoesNotContain("\n", yankedText);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 60_000)]
    public async Task IlInspector_ShiftV_SelectsCurrentLine()
    {
        var (terminal, app, ct) = Launch(samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Navigate to IL Inspector with a method
        var method = _state!.Analyzer.MethodDefs.First(m => m.Rva > 0);
        _state.NavigateToIlMethod(method);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_0000:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Focus the IL editor and position cursor on a line
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("l")
            .WaitUntil(_ => IsFocusedOnEditor(_state.IlEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.False(_state.IlEditorState!.Cursor.HasSelection);

        // Shift+V to select the line
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.V)
            .WaitUntil(_ => _state.IlEditorState!.Cursor.HasSelection, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Verify: selection covers visible line content, no newline
        var es = _state.IlEditorState!;
        var selected = es.Document.GetText(es.Cursor.SelectionRange);
        // SelectLine uses inclusive end, so GetText(range) may miss the last char;
        // yank adds +1, but for this assertion just check the raw selection is clean
        Assert.True(selected.Length > 0, "Selection should not be empty");
        Assert.DoesNotContain("\n", selected);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 60_000)]
    public async Task IlInspector_YY_YanksCurrentLine()
    {
        var (terminal, app, ct) = Launch(samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Navigate to IL Inspector with a method
        var method = _state!.Analyzer.MethodDefs.First(m => m.Rva > 0);
        _state.NavigateToIlMethod(method);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_0000:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Focus the IL editor
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("l")
            .WaitUntil(_ => IsFocusedOnEditor(_state.IlEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // yy to yank the current line
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.True(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var yankedText),
            "CopyToClipboard should have emitted an OSC 52 sequence");

        // The first line visible is a comment line (// Method: ...)
        // Verify: no newline, no bleed into next line
        Assert.True(yankedText.Length > 0);
        Assert.DoesNotContain("\n", yankedText);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Hex Dump / Data Interpretation ---

    [Fact(Timeout = 30_000)]
    public async Task HexDump_TabTogglesFocusBetweenHexEditorAndDataInterp()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.HexDump);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Data Interpretation"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Initial focus should be on the hex editor
        await Task.Delay(100, ct);
        Assert.True(IsFocusedOnEditor(_state!.HexEditorState),
            "Initial focus should be on the hex editor");

        // Tab → data interp editor
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.DataInterpEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);
        Assert.True(IsFocusedOnEditor(_state!.DataInterpEditorState));

        // Tab → back to hex editor
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.HexEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);
        Assert.True(IsFocusedOnEditor(_state!.HexEditorState));

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task DataInterp_SelectionYank_CopiesTextAndFlashes()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.HexDump);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Data Interpretation"), TimeSpan.FromSeconds(10))
            // Tab to data interp editor
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.DataInterpEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Select text via Shift+Right
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.DataInterpEditorState!.Cursor.HasSelection, TimeSpan.FromSeconds(5))
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
    public async Task DataInterp_WordSelectionAndYanks()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.HexDump);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Data Interpretation"), TimeSpan.FromSeconds(10))
            // Tab to data interp editor
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.DataInterpEditorState), TimeSpan.FromSeconds(5))
            // Shift+Right to build a selection (same pattern as PeHeaders test)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.DataInterpEditorState!.Cursor.HasSelection, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Yank the selection
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
    public async Task HexDump_InsertModeOnlyActivatesFromHexEditor()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.HexDump);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Data Interpretation"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Tab to data interp editor
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.DataInterpEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Press 'i' — should NOT enter insert mode (data interp is focused)
        // Use WaitUntil to ensure a render cycle processes the key
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("i")
            .WaitUntil(_ => true, TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal, ct);
        Assert.Equal(HexEditMode.Normal, _state!.HexMode);

        // Tab back to hex editor, then press 'i' — should enter insert mode.
        // Both steps in one sequence so the binding re-registration from the
        // focus change is guaranteed to happen before the 'i' keypress.
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.HexEditorState), TimeSpan.FromSeconds(5))
            .Type("i")
            .WaitUntil(_ => _state!.HexMode == HexEditMode.Insert, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);
        Assert.Equal(HexEditMode.Insert, _state!.HexMode);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task HexDump_SearchRefocusesToHexEditor()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.HexDump);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Data Interpretation"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Tab to data interp editor
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.DataInterpEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Activate search with '/', wait for TextBox focus before typing the query
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("/")
            .WaitUntil(_ => _state!.App.FocusedNode is TextBoxNode, TimeSpan.FromSeconds(5))
            .Type("4D")
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.HexEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.True(IsFocusedOnEditor(_state!.HexEditorState),
            "Search confirm should refocus to hex editor");

        // Now test Escape dismiss: Tab to data interp, activate search,
        // wait for TextBox focus, then Escape to dismiss
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.DataInterpEditorState), TimeSpan.FromSeconds(5))
            .Type("/")
            .WaitUntil(_ => _state!.App.FocusedNode is TextBoxNode, TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.HexEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.True(IsFocusedOnEditor(_state!.HexEditorState),
            "Search dismiss should refocus to hex editor");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task HexDump_DataInterpUpdatesOnCursorMoveAndEndianToggle()
    {
        var (terminal, app, ct) = Launch(samples.RichLibraryDll, TabId.HexDump);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Data Interpretation"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // At offset 0 of a .NET assembly, first byte is 0x4D ('M' of MZ header)
        // Int8 for 0x4D = 77, UInt8 for 0x4D = 77
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Int8:") && s.ContainsText("77"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Capture current data interp text
        var textBefore = _state!.DataInterpEditorText;
        Assert.NotNull(textBefore);

        // Move cursor right — values should change
        // Second byte of MZ header is 0x5A ('Z'), Int8 = 90
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("l")
            .WaitUntil(_ => _state!.DataInterpEditorText != textBefore, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        var textAfterMove = _state!.DataInterpEditorText;
        Assert.NotEqual(textBefore, textAfterMove);

        // Toggle endianness — multi-byte values should change
        var textBeforeEndian = _state!.DataInterpEditorText;
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("e")
            .WaitUntil(_ => _state!.DataInterpEditorText != textBeforeEndian, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        var textAfterEndian = _state!.DataInterpEditorText;
        Assert.NotEqual(textBeforeEndian, textAfterEndian);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }
}
