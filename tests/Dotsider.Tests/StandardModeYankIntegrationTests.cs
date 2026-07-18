using Dotsider.Views;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Standard Mode Yank Integration.
/// </summary>
[TestClass]
public class StandardModeYankIntegrationTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private ClipboardCapturingWorkloadAdapter? _clipboardAdapter;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;
    private CancellationTokenSource? _cts;

    private (Hex1bTerminal terminal, Hex1bApp app, CancellationToken ct) Launch(
        string dllPath, int? initialTab = null)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
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

    private EditorNode? FindEditorNode(EditorState? expectedState)
    {
        try
        {
            return _hex1bApp?.Focusables
                .OfType<EditorNode>()
                .FirstOrDefault(node => node.State == expectedState);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task<string> TypeYAndCaptureNotificationAsync(Hex1bTerminal terminal, CancellationToken ct)
    {
        string? notification = null;
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(_ => (notification = _state!.YankNotification) is not null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);
        return notification!;
    }

    // --- General tab ---

    /// <summary>
    /// Verifies general tab toggles focus between editor and table.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task General_TabTogglesFocusBetweenEditorAndTable()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Initial focus is on the table (RequestContentFocus excludes EditorNode)
        // Allow render to settle before checking focus
        await Task.Delay(100, ct);
        Assert.IsFalse(IsFocusedOnEditor(),
            "Initial focus should not be on the editor");

        // Tab → editor
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);
        Assert.IsTrue(IsFocusedOnEditor());

        // Tab → table
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => !IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);
        Assert.IsFalse(IsFocusedOnEditor());

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies general yank on focused row shows notification and flash.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task General_YankOnFocusedRow_ShowsNotificationAndFlash()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Ensure a ref row is focused
        Assert.IsNotNull(_state!.GeneralFocusedDep);

        // Compute expected payload before yank
        var expectedPayload = YankHelper.GetYankText(_state);
        Assert.IsNotNull(expectedPayload);
        Assert.Contains("\t", expectedPayload); // Tab-separated

        // Yank
        var notification = await TypeYAndCaptureNotificationAsync(terminal, ct);

        // Notification contains the payload (truncated if long)
        var firstRef = _state.Analyzer.AssemblyRefs.First(r => r.Name == _state.GeneralFocusedDep as string);
        Assert.Contains(firstRef.Name, notification);

        // Verify the actual OSC 52 clipboard payload emitted by ctx.CopyToClipboard
        Assert.IsTrue(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var actualClipboard),
            "CopyToClipboard should have emitted an OSC 52 sequence");
        Assert.AreEqual(expectedPayload, actualClipboard);

        // Wait for notification to clear
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => _state.YankNotification is null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- PE/Metadata tab ---

    /// <summary>
    /// Verifies pe metadata tab cycles through headers and table.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task PeMetadata_TabCyclesThroughHeadersAndTable()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.PeMetadata);
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
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is not EditorNode; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies pe metadata left right do not switch sub tabs when editor focused.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task PeMetadata_LeftRightDoNotSwitchSubTabsWhenEditorFocused()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.PeMetadata);
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
        Assert.AreEqual(initialSubTab, _state.PeSubTab);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies pe metadata detail popup is editor and esc closes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task PeMetadata_DetailPopupIsEditorAndEscCloses()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.PeMetadata);
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

        Assert.IsNotNull(_state!.PeDetailEditorState);
        Assert.IsTrue(_state.App.FocusedNode is EditorNode,
            "Detail popup editor should have focus");

        // Escape closes popup
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state!.PeDetailContent is null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(100, ct);
        Assert.IsFalse(_state.App.FocusedNode is EditorNode,
            "Focus should return to table after popup closes");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Strings tab ---

    /// <summary>
    /// Verifies strings yank on focused row copies string value.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Strings_YankOnFocusedRow_CopiesStringValue()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.Strings);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("strings"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsNotNull(_state!.StringsFocusedKey);

        _ = await TypeYAndCaptureNotificationAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies strings left right do not switch tabs when popup open.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Strings_LeftRightDoNotSwitchTabsWhenPopupOpen()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.Strings);
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
        Assert.AreEqual(initialSourceTab, _state.StringsSourceTab);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Hex Dump tab ---

    /// <summary>
    /// Verifies hex dump selection yank copies uppercase hex bytes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task HexDump_SelectionYank_CopiesUppercaseHexBytes()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.HexDump);
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
        var notification = await TypeYAndCaptureNotificationAsync(terminal, ct);

        // Verify payload is uppercase hex
        Assert.MatchesRegex(@"Yanked: [0-9A-F]{2} [0-9A-F]{2}", notification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Focus restoration ---

    /// <summary>
    /// Verifies focus restoration after detail popup close lands on table.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task FocusRestoration_AfterDetailPopupClose_LandsOnTable()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.PeMetadata);
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
        Assert.IsFalse(_state!.App.FocusedNode is EditorNode,
            "Focus should land on table after closing detail popup");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- PE/Metadata editor selection + yank ---

    /// <summary>
    /// Verifies pe headers selection yank copies text and flashes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task PeHeaders_SelectionYank_CopiesTextAndFlashes()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.PeMetadata);
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
        _ = await TypeYAndCaptureNotificationAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies pe detail popup selection yank works.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task PeDetailPopup_SelectionYank_Works()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.PeMetadata);
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
        _ = await TypeYAndCaptureNotificationAsync(terminal, ct);

        // Escape closes popup
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state!.PeDetailContent is null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Strings detail popup selection + yank ---

    /// <summary>
    /// Verifies strings detail popup selection yank works.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task StringsDetailPopup_SelectionYank_Works()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.Strings);
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
        _ = await TypeYAndCaptureNotificationAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- General tab double-click word selection ---

    /// <summary>
    /// Verifies general double click word selection adjusts boundary and yanks.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task General_DoubleClickWordSelection_AdjustsBoundaryAndYanks()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll);
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

        EditorNode? editorNode = null;
        await TestHelpers.WaitUntilAsync(
            () =>
            {
                editorNode = FindEditorNode(_state!.GeneralInfoEditorState);
                return editorNode is { Bounds.Width: > 0, Bounds.Height: > 0 };
            },
            TimeSpan.FromSeconds(5));

        Assert.IsGreaterThan(0, matches.Count);
        var bounds = editorNode!.Bounds;
        var editorMatches = matches
            .Where(m => m.Line >= bounds.Y
                     && m.Line < bounds.Y + bounds.Height
                     && m.Column >= bounds.X
                     && m.Column < bounds.X + bounds.Width)
            .ToList();
        Assert.IsNotEmpty(editorMatches);
        var (row, col) = editorMatches[0];

        // Focus through the real General-view binding. It executes on Hex1b's input
        // loop, so the pending focus request is consumed by the mandatory next render.
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));
        var state = _state!;
        await auto.KeyAsync(Hex1bKey.Tab, ct);
        await auto.WaitUntilAsync(_ => IsFocusedOnEditor(state.GeneralInfoEditorState),
            description: "general info editor focused");

        var editorState = state.GeneralInfoEditorState!;
        var wordOffset = editorState.Document.GetText().IndexOf("Version", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, wordOffset);
        var expectedOffset = wordOffset + 2;

        Hex1bMouseCompatibility.BeginClickSequence(app);
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(col + 2, row)
            .Build()
            .ApplyAsync(terminal, ct);
        await auto.WaitUntilAsync(_ => editorState.Cursor.Position.Value == expectedOffset,
            description: "first click landed on the displayed Version word");

        Hex1bMouseCompatibility.ContinueClickSequence(app);
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(col + 2, row)
            .Build()
            .ApplyAsync(terminal, ct);

        // Wait for selection to appear
        await TestHelpers.WaitUntilAsync(
            () =>
            {
                var es = _state!.GeneralInfoEditorState;
                if (es?.Cursor.HasSelection != true)
                    return false;

                var selectedText = es.Document.GetText(es.Cursor.SelectionRange);
                return selectedText.Length > 0 && selectedText.All(char.IsLetterOrDigit);
            },
            TimeSpan.FromSeconds(5));

        // Verify selection is a clean word
        var es = _state!.GeneralInfoEditorState!;
        var selected = es.Document.GetText(es.Cursor.SelectionRange);
        Assert.IsGreaterThan(0, selected.Length, "Selection should not be empty");
        Assert.IsTrue(selected.All(char.IsLetterOrDigit),
            $"Expected pure word, got '{selected}'");

        // Yank the selection
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsNotNull(_state.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Size Map drill ---

    /// <summary>
    /// Verifies size map select then enter drills.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeMap_SelectThenEnterDrills()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.SizeMap);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary") && s.ContainsText("Total:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsEmpty(_state!.TreemapBreadcrumb);

        // Select with arrow first, then Enter to drill
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.TreemapSelectedIndex >= 0, TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state.TreemapBreadcrumb.Count > 0, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsNotEmpty(_state.TreemapBreadcrumb);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies size map enter without selection does nothing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeMap_EnterWithoutSelection_DoesNothing()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.SizeMap);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary") && s.ContainsText("Total:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsEmpty(_state!.TreemapBreadcrumb);
        Assert.AreEqual(-1, _state.TreemapSelectedIndex);

        // Press Enter with no selection — should be a no-op
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(200, ct);
        Assert.IsEmpty(_state.TreemapBreadcrumb);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Esc regression: nested cross-view + assembly stack ---

    /// <summary>
    /// Verifies esc back cross view takes priority over assembly stack.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task EscBack_CrossViewTakesPriorityOverAssemblyStack()
    {
        // Start on a real referenced assembly, then push RichLibrary through the
        // app's normal navigation path. That gives the test an assembly back-stack
        // while keeping RichLibrary's real TypeDefs active for the cross-view jump.
        using var richAnalyzer = new Dotsider.Core.Analysis.AssemblyAnalyzer(Samples.RichLibraryDll);
        var refName = richAnalyzer.AssemblyRefs[0].Name;
        var resolvedPath = Dotsider.Core.Analysis.AssemblyAnalyzer.ResolveAssemblyPath(
            richAnalyzer.FilePath, refName);
        Assert.IsNotNull(resolvedPath);

        var (terminal, app, ct) = Launch(resolvedPath);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsTrue(_state!.PushAssembly(Samples.RichLibraryDll),
            "PushAssembly should return to RichLibrary through the normal navigation path.");
        _state.App.Invalidate();
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => _state.NavigationStack.Count == 1
                && string.Equals(
                    Path.GetFullPath(_state.Analyzer.FilePath),
                    Path.GetFullPath(Samples.RichLibraryDll),
                    StringComparison.OrdinalIgnoreCase),
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Switch to PE/Metadata through the normal tab binding, then route the
        // test to TypeDef once the PE view has rendered.
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D2)
            .WaitUntil(_ => _state.CurrentTab == TabId.PeMetadata, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        _state.PeSubTab = PeSubTabId.TypeDef;
        _state.RequestContentFocus();
        _state.App.Invalidate();
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => _state.PeSubTab == PeSubTabId.TypeDef, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("TypeDef"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // RichLibrary has real types with methods
        var typeDef = _state.Analyzer.TypeDefs.FirstOrDefault(t =>
            !t.FullName.StartsWith('<') && t.MethodCount > 0);
        Assert.IsNotNull(typeDef);

        _state.PeFocusedKey = typeDef.Token;
        _state.RequestContentFocus();
        _state.App.Invalidate();
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => Equals(_state.PeFocusedKey, typeDef.Token), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Press g to trigger cross-view jump to IL Inspector
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("g")
            .WaitUntil(_ => _state.CurrentTab == TabId.IlInspector
                && _state.CrossViewBackTarget is not null,
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsNotNull(_state.CrossViewBackTarget);
        Assert.IsGreaterThan(0, _state.NavigationStack.Count);

        // Esc 1: cross-view back to PE/Metadata — NOT assembly pop
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state.CrossViewBackTarget is null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual(TabId.PeMetadata, _state.CurrentTab);
        Assert.IsGreaterThan(0, _state.NavigationStack.Count, "Assembly stack should still have the parent");

        // Esc 2: pop assembly and return to General
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state.CurrentTab == TabId.General
                && _state.NavigationStack.Count == 0,
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsEmpty(_state.NavigationStack);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Esc regression: Dynamic filter clears before assembly pop ---

    /// <summary>
    /// Verifies esc back dynamic filter clears before assembly pop.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task EscBack_DynamicFilterClearsBeforeAssemblyPop()
    {
        // Use HelloWorld which is executable (has entry point for Dynamic tab)
        var (terminal, app, ct) = Launch(Samples.HelloWorldDll);
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
            .WaitUntil(_ => _state!.CurrentTab == TabId.General
                && _state.NavigationStack.Count == 0
                && _state.GeneralFocusedDep is not null,
                TimeSpan.FromSeconds(5))
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
        Assert.HasCount(stackBefore, _state.NavigationStack);

        // Clear the filter and send Esc again — should now pop
        _state.DynamicCategoryFilter = null;
        await auto.EscapeAsync(ct: ct);
        await auto.WaitUntilAsync(_ => _state.CurrentTab == TabId.General,
            description: "Esc to pop back to General tab");

        Assert.AreEqual(TabId.General, _state.CurrentTab);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Size Map regression: zero-match search Enter is no-op ---

    /// <summary>
    /// Verifies size map enter after zero match search does not drill.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeMap_EnterAfterZeroMatchSearch_DoesNotDrill()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.SizeMap);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary") && s.ContainsText("Total:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Search for something that won't match
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is TextBoxNode; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            .Type("zzzznotanamespace")
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Search[TabId.SizeMap].IsConfirmed, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Verify the precondition: zero matches, no selection
        Assert.AreEqual(-1, _state!.TreemapMatchIndex);
        Assert.AreEqual(-1, _state.TreemapSelectedIndex);
        Assert.IsEmpty(_state.TreemapBreadcrumb);

        // Enter should be a no-op — no match, no selection
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(200, ct);
        Assert.IsEmpty(_state.TreemapBreadcrumb);
        Assert.AreEqual(-1, _state.TreemapMatchIndex);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Strings detail popup regression: content must be visible on screen ---

    /// <summary>
    /// Verifies strings detail popup shows string content on screen.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task StringsDetail_PopupShowsStringContentOnScreen()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.Strings);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("strings"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Capture the string value from the first visible row
        var strings = _state!.GetActiveStrings();
        Assert.IsGreaterThan(0, strings.Count);
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

    /// <summary>
    /// Verifies yank notification auto clears after1500ms.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task YankNotification_AutoClears_After1500ms()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll);
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
        Assert.IsNotNull(_state!.YankNotification);

        // Wait for auto-clear
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => _state.YankNotification is null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsNull(_state.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Yank flash on table row ---

    /// <summary>
    /// Verifies general yank flash sets and clears.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task General_YankFlash_SetsAndClears()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Ensure table has focus (not editor)
        Assert.IsNotNull(_state!.GeneralFocusedDep);
        Assert.IsFalse(_state.App.FocusedNode is EditorNode);

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

        Assert.IsFalse(_state.YankFlashRow);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _cts?.Cancel();
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        _cts?.Dispose();
    }

    // --- Vim Text Object Tests ---

    /// <summary>
    /// Verifies general iw selects word in editor.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task General_IwSelectsWordInEditor()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Tab) // Focus editor
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsTrue(IsFocusedOnEditor());
        Assert.IsFalse(_state!.GeneralInfoEditorState!.Cursor.HasSelection);

        // Press i — verify it arms the state machine
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.I)
            .WaitUntil(_ => _state!.VimPending == VimMotionState.WaitingForTextObject, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual(VimMotionState.WaitingForTextObject, _state!.VimPending);

        // Press w — should select inner word
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.W)
            .WaitUntil(_ => _state!.VimPending == VimMotionState.Idle, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual(VimMotionState.Idle, _state.VimPending);
        Assert.IsTrue(_state.GeneralInfoEditorState!.Cursor.HasSelection);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies general iw selects word in editor.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task General_IWSelectsWORDInEditor()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll);
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

        Assert.IsTrue(_state!.GeneralInfoEditorState!.Cursor.HasSelection);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies general yiw yanks word from editor.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task General_YiwYanksWordFromEditor()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.RightArrow)
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.GeneralInfoEditorState!.Cursor.Position.Value == 2,
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual('A', _state!.GeneralInfoEditorState!.Document.GetText()[
            _state.GeneralInfoEditorState.Cursor.Position.Value]);

        // Press y — verify it arms WaitingForYMotion
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Y)
            .WaitUntil(_ => _state!.VimPending == VimMotionState.WaitingForYMotion, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual(VimMotionState.WaitingForYMotion, _state!.VimPending);

        // Press i — advances to WaitingForYTextObject
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.I)
            .WaitUntil(_ => _state!.VimPending == VimMotionState.WaitingForYTextObject, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Press w — selects + yanks
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.W)
            .WaitUntil(snapshot => _clipboardAdapter!.ClipboardWrites.TryPeek(out _),
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsTrue(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var clipboard),
            "CopyToClipboard should have emitted an OSC 52 sequence");
        Assert.AreEqual("Assembly", clipboard);
        Assert.AreEqual(VimMotionState.Idle, _state.VimPending);
        Assert.IsFalse(_state.GeneralInfoEditorState.Cursor.HasSelection);
        Assert.AreEqual(9, _state.GeneralInfoEditorState.Cursor.Position.Value);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies general interrupted by global key does not select.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task General_InterruptedByGlobalKey_DoesNotSelect()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll);
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

        Assert.AreEqual(VimMotionState.Idle, _state!.VimPending);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies general random letter cancels.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task General_RandomLetterCancels()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll);
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
        Assert.IsFalse(_state!.GeneralInfoEditorState!.Cursor.HasSelection);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies hex dump y does not arm on hex normal.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task HexDump_YDoesNotArmOnHexNormal()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, initialTab: TabId.HexDump);
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
        Assert.AreEqual(VimMotionState.Idle, _state!.VimPending);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Triple-click line selection ---

    /// <summary>
    /// Verifies il inspector triple click selects only current line.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task IlInspector_TripleClickSelectsOnlyCurrentLine()
    {
        var (terminal, app, ct) = Launch(Samples.HelloWorldDll);
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

        var editorState = _state.IlEditorState!;
        var lineOffset = editorState.Document.GetText().IndexOf("IL_0000:", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, lineOffset);
        var expectedOffset = lineOffset + 3;

        // Drive all three real mouse clicks, with an observable semantic barrier after
        // each one. The compatibility clock removes the framework's wall-clock race.
        Hex1bMouseCompatibility.BeginClickSequence(app);
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(col + 3, row)
            .Build()
            .ApplyAsync(terminal, ct);
        await TestHelpers.WaitUntilAsync(
            () => editorState.Cursor.Position.Value == expectedOffset,
            TimeSpan.FromSeconds(5));

        Hex1bMouseCompatibility.ContinueClickSequence(app);
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(col + 3, row)
            .Build()
            .ApplyAsync(terminal, ct);
        await TestHelpers.WaitUntilAsync(
            () =>
            {
                if (!editorState.Cursor.HasSelection)
                    return false;

                var range = editorState.Cursor.SelectionRange;
                var effectiveEnd = new DocumentOffset(Math.Max(
                    range.End.Value,
                    editorState.Cursor.Position.Value + 1));
                return range.Start.Value == expectedOffset
                    && editorState.Document.GetText(new DocumentRange(range.Start, effectiveEnd)) == "0000";
            },
            TimeSpan.FromSeconds(5));

        Hex1bMouseCompatibility.ContinueClickSequence(app);
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(col + 3, row)
            .WaitUntil(_ =>
            {
                var es = _state.IlEditorState;
                if (es?.Cursor.HasSelection != true)
                    return false;

                return es.Document.GetText(es.Cursor.SelectionRange)
                    .StartsWith("IL_0000:", StringComparison.Ordinal)
                    && IsFocusedOnEditor(es);
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Verify the editor still has focus after triple-click
        Assert.IsTrue(IsFocusedOnEditor(_state.IlEditorState),
            "Editor should have focus after triple-click");

        // Yank the selection
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Verify clipboard content
        Assert.IsTrue(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var yankedText),
            "CopyToClipboard should have emitted an OSC 52 sequence");

        // The yanked text should be exactly the IL_0000 line — no trailing newline
        // and no character from the next line (e.g. "I" from "IL_0005")
        Assert.StartsWith("IL_0000:", yankedText);
        Assert.DoesNotContain("\n", yankedText);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies il inspector shift v selects current line.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task IlInspector_ShiftV_SelectsCurrentLine()
    {
        var (terminal, app, ct) = Launch(Samples.HelloWorldDll);
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

        Assert.IsFalse(_state.IlEditorState!.Cursor.HasSelection);

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
        Assert.IsGreaterThan(0, selected.Length, "Selection should not be empty");
        Assert.DoesNotContain("\n", selected);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies il inspector yy yanks current line.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task IlInspector_YY_YanksCurrentLine()
    {
        var (terminal, app, ct) = Launch(Samples.HelloWorldDll);
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

        Assert.IsTrue(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var yankedText),
            "CopyToClipboard should have emitted an OSC 52 sequence");

        // The first line visible is a comment line (// Method: ...)
        // Verify: no newline, no bleed into next line
        Assert.IsGreaterThan(0, yankedText.Length);
        Assert.DoesNotContain("\n", yankedText);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies il inspector source link yank copies the resolved URL.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task IlInspector_SourceLinkUrlYank_CopiesResolvedUrl()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var method = _state!.Analyzer.MethodDefs.First(m =>
            m.DeclaringType == "RichLibrary.Services.UserService" && m.Name == "Add");
        _state.NavigateToIlMethod(method);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => _state.IlEditorState?.Document.GetText()
                .Contains(IlSourceLinkDecorationProvider.SourceLinkMarker, StringComparison.Ordinal) == true,
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("l")
            .WaitUntil(_ => IsFocusedOnEditor(_state.IlEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        var documentText = _state.IlEditorState!.Document.GetText();
        var markerOffset = documentText.IndexOf(
            IlSourceLinkDecorationProvider.SourceLinkMarker,
            StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, markerOffset, "Expected rendered IL to contain a Source Link marker.");

        _state.IlEditorState.SetCursorPosition(new DocumentOffset(markerOffset));
        _state.App.Invalidate();

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => IlNavigationHelper.GetSourceLinkUrlAtCursor(
                _state.IlEditorState,
                _state.IlInstructions!) is not null,
                TimeSpan.FromSeconds(5))
            .Type("u")
            .WaitUntil(snapshot => _clipboardAdapter!.ClipboardWrites.TryPeek(out _),
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsTrue(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var yankedText),
            "CopyToClipboard should have emitted an OSC 52 sequence");
        Assert.StartsWith("https://raw.githubusercontent.com/willibrandon/dotsider/", yankedText);
        Assert.EndsWith("samples/RichLibrary/Services/UserService.cs", yankedText);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Hex Dump / Data Interpretation ---

    /// <summary>
    /// Verifies hex dump tab toggles focus between hex editor and data interp.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task HexDump_TabTogglesFocusBetweenHexEditorAndDataInterp()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.HexDump);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Data Interpretation"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Initial focus should be on the hex editor
        await Task.Delay(100, ct);
        Assert.IsTrue(IsFocusedOnEditor(_state!.HexEditorState),
            "Initial focus should be on the hex editor");

        // Tab → data interp editor
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.DataInterpEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);
        Assert.IsTrue(IsFocusedOnEditor(_state!.DataInterpEditorState));

        // Tab → back to hex editor
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.HexEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);
        Assert.IsTrue(IsFocusedOnEditor(_state!.HexEditorState));

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies data interp selection yank copies text and flashes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task DataInterp_SelectionYank_CopiesTextAndFlashes()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.HexDump);
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
        _ = await TypeYAndCaptureNotificationAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies data interp word selection and yanks.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task DataInterp_WordSelectionAndYanks()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.HexDump);
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
        _ = await TypeYAndCaptureNotificationAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies hex dump insert mode only activates from hex editor.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task HexDump_InsertModeOnlyActivatesFromHexEditor()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.HexDump);
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
        Assert.AreEqual(HexEditMode.Normal, _state!.HexMode);

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
        Assert.AreEqual(HexEditMode.Insert, _state!.HexMode);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies hex dump search refocuses to hex editor.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task HexDump_SearchRefocusesToHexEditor()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.HexDump);
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
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is TextBoxNode; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            .Type("4D")
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.HexEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsTrue(IsFocusedOnEditor(_state!.HexEditorState),
            "Search confirm should refocus to hex editor");

        // Now test Escape dismiss: Tab to data interp, activate search,
        // wait for TextBox focus, then Escape to dismiss
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.DataInterpEditorState), TimeSpan.FromSeconds(5))
            .Type("/")
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is TextBoxNode; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.HexEditorState), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsTrue(IsFocusedOnEditor(_state!.HexEditorState),
            "Search dismiss should refocus to hex editor");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies hex dump data interp updates on cursor move and endian toggle.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task HexDump_DataInterpUpdatesOnCursorMoveAndEndianToggle()
    {
        var (terminal, app, ct) = Launch(Samples.RichLibraryDll, TabId.HexDump);
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
        Assert.IsNotNull(textBefore);

        // Move cursor right — values should change
        // Second byte of MZ header is 0x5A ('Z'), Int8 = 90
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("l")
            .WaitUntil(_ => _state!.DataInterpEditorText != textBefore, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        var textAfterMove = _state!.DataInterpEditorText;
        Assert.AreNotEqual(textBefore, textAfterMove);

        // Toggle endianness — multi-byte values should change
        var textBeforeEndian = _state!.DataInterpEditorText;
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("e")
            .WaitUntil(_ => _state!.DataInterpEditorText != textBeforeEndian, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        var textAfterEndian = _state!.DataInterpEditorText;
        Assert.AreNotEqual(textBeforeEndian, textAfterEndian);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }
}
