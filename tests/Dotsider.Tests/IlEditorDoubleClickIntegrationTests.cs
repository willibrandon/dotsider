using Hex1b;
using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end integration tests for double-click word selection and yank
/// in the IL Inspector, exercising the full mouse → EditorNode → one-shot
/// cursor adjustment → yank pipeline.
/// </summary>
[TestClass]
public class IlEditorDoubleClickIntegrationTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;
    private CancellationTokenSource? _cts;
    private Task? _runTask;

    private (Hex1bTerminal terminal, Hex1bApp app, CancellationToken ct) CreateDotsiderApp(string dllPath)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        _workload = new Hex1bAppWorkloadAdapter();
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
                _state ??= new DotsiderState(_hex1bApp!, dllPath);
                dotsiderApp ??= new DotsiderApp(_state);
                return Task.FromResult<Hex1bWidget>(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false,
                EnableMouse = true,
                Theme = DotsiderTheme.Create()
            });
        return (_terminal, _hex1bApp, _cts.Token);
    }

    private Task RunAppAsync(Hex1bApp app, CancellationToken ct)
    {
        _runTask = app.RunAsync(ct);
        return _runTask;
    }

    private bool TryWaitForAppExit()
    {
        if (_runTask is null) return true;
        try { return _runTask.Wait(TimeSpan.FromSeconds(5)); }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static e => e is OperationCanceledException)) { return true; }
        catch (OperationCanceledException) { return true; }
    }

    /// <summary>
    /// Expands the tree path for a method and selects it in the IL Inspector.
    /// </summary>
    private void SelectMethodInTree(
        Dotsider.Core.Analysis.Models.MethodDefInfo method)
    {
        var typeDef = _state!.Analyzer.TypeDefs.First(t => t.FullName == method.DeclaringType);
        var ns = !string.IsNullOrEmpty(typeDef.Namespace) ? typeDef.Namespace : "(global)";
        _state.IlTreeExpansionState[$"ns:{ns}"] = true;
        _state.IlTreeExpansionState[$"type:{method.DeclaringType}"] = true;
        _state.IlSelectedMethod = method;
        _state.IlFocusedTreeKey = $"method:{method.Token}";
        _state.App.Invalidate();
        _state.RequestExtraFrame();
    }

    /// <summary>
    /// Programmatic SelectWordAt (simulating what EditorNode.cs:281 does on double-click)
    /// through the full render pipeline. Verifies the one-shot cursor adjustment fires
    /// correctly and that yank produces the right text and cursor position.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SelectWordAt_ThroughRenderPipeline_AdjustsCursorAndYankWorks()
    {
        var (terminal, app, ct) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await Task.Delay(50, ct);

        // Navigate to IL Inspector tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("▶") || s.ContainsText("▼"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Find a method whose IL contains a dotted name
        var method = _state!.Analyzer.MethodDefs
            .Where(m => m.Rva > 0)
            .First(m => _state.IlDisassembler!.FormatDisassembly(m).Contains("System."));
        SelectMethodInTree(method);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_0000"), TimeSpan.FromSeconds(10))
            .WaitUntil(_ => _state!.IlEditorMethod?.Token == method.Token
                && _state.IlEditorState?.Document.GetText().Contains("System.", StringComparison.Ordinal) == true,
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var editorState = _state.IlEditorState!;
        var doc = editorState.Document;
        var fullText = doc.GetText();
        var systemIdx = fullText.IndexOf("System.", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, systemIdx, "Expected 'System.' in rendered IL editor document");

        // Simulate double-click: SelectWordAt is what EditorNode.cs:281 calls
        editorState.SelectWordAt(new DocumentOffset(systemIdx));
        _state.App.Invalidate();
        _state.RequestExtraFrame();

        // Wait for the render cycle to process AdjustWordSelectionCursorOneShot —
        // the one-shot pulls the cursor from the trailing '.' back onto the last
        // word character, so poll until the cursor lands on a letter/digit.
        await TestHelpers.WaitUntilAsync(
            () =>
            {
                if (!ReferenceEquals(_state.IlEditorState, editorState)) return false;
                var es = editorState;
                if (!es.Cursor.HasSelection) return false;
                var pos = es.Cursor.Position.Value;
                var text = es.Document.GetText();
                return pos < text.Length && char.IsLetterOrDigit(text[pos]);
            },
            TimeSpan.FromSeconds(5));

        fullText = doc.GetText();
        var cursorOffset = editorState.Cursor.Position.Value;

        // Cursor must be on last word char ('m' of "System"), not on '.'
        Assert.IsLessThan(fullText.Length, cursorOffset, "Cursor should be within document bounds");
        Assert.AreEqual('m', fullText[cursorOffset]);

        // Yank must copy the full word "System"
        var range = editorState.Cursor.SelectionRange;
        var yankEnd = new DocumentOffset(Math.Min(
            Math.Max(range.End.Value, editorState.Cursor.Position.Value + 1),
            doc.Length));
        var yankRange = new DocumentRange(range.Start, yankEnd);
        var yankText = doc.GetText(yankRange);
        Assert.AreEqual("System", yankText);

        // Post-yank cursor must land on 'm'
        var postYankCursor = new DocumentOffset(Math.Max(0, yankEnd.Value - 1));
        Assert.AreEqual('m', fullText[postYankCursor.Value]);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// Full mouse double-click via two real SGR click sequences through the terminal.
    /// Verifies EditorNode processes the
    /// double-click, calls SelectWordAt, and the one-shot cursor adjustment fires.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task DoubleClickAt_SgrMouse_SelectsWordAndAdjustsCursor()
    {
        var (terminal, app, ct) = CreateDotsiderApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await Task.Delay(50, ct);

        // Navigate to IL Inspector tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("▶") || s.ContainsText("▼"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Find a method with dotted IL names
        var method = _state!.Analyzer.MethodDefs
            .Where(m => m.Rva > 0)
            .First(m => _state.IlDisassembler!.FormatDisassembly(m).Contains("System."));
        SelectMethodInTree(method);

        // Wait for the IL editor to fully render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_0000"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Dump all FindText("System.") matches to understand screen layout
        List<(int Line, int Column)> allMatches = [];
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                var matches = s.FindText("System.");
                if (matches.Count == 0) return false;
                allMatches = [.. matches.Select(m => (m.Line, m.Column))];
                return true;
            }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);
        Assert.IsGreaterThan(0, allMatches.Count, "Expected 'System.' visible on screen");

        // Use the first match — coordinates are 0-based
        var (targetRow, targetCol) = allMatches[0];

        // Focus through the real application binding before queueing the clicks.
        // Calling RequestFocus directly from the test thread races the render loop's
        // one-shot pending-focus slot and can lose the request under parallel load.
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));
        await auto.KeyAsync(Hex1bKey.L, ct);
        await auto.WaitUntilAsync(_ =>
            _state!.App.FocusedNode is EditorNode { State: var es }
                && ReferenceEquals(es, _state.IlEditorState),
            description: "IL editor focused");

        var editorState = _state.IlEditorState!;
        var expectedOffset = editorState.Document.GetText().IndexOf("System.", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, expectedOffset);

        // Send the first click through the real mouse pipeline and prove that it lands
        // on the exact on-screen word before arming the deterministic continuation.
        Hex1bMouseCompatibility.BeginClickSequence(app);
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(targetCol, targetRow)
            .Build()
            .ApplyAsync(terminal, ct);
        await auto.WaitUntilAsync(_ =>
            ReferenceEquals(_state.IlEditorState, editorState)
                && editorState.Cursor.Position.Value == expectedOffset,
            description: "first click landed on the displayed System word");

        // The installed Hex1b version recomputes click count from wall-clock time.
        // Force only its continuation clock, then send the second real click.
        Hex1bMouseCompatibility.ContinueClickSequence(app);
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(targetCol, targetRow)
            .Build()
            .ApplyAsync(terminal, ct);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ =>
            {
                var es = _state.IlEditorState;
                if (es?.Cursor.HasSelection != true) return false;
                var text = es.Document.GetText();
                var pos = es.Cursor.Position.Value;
                return pos < text.Length && char.IsLetterOrDigit(text[pos]);
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Verify selection happened
        Assert.IsTrue(_state.IlEditorState!.Cursor.HasSelection,
            "Double-click should create a selection");

        // Cursor must be on a word character, not punctuation
        var doc = _state.IlEditorState!.Document;
        var fullText = doc.GetText();
        var cursorVal = _state.IlEditorState!.Cursor.Position.Value;
        Assert.IsLessThan(fullText.Length, cursorVal, "Cursor should be within document bounds");
        Assert.IsTrue(char.IsLetterOrDigit(fullText[cursorVal]),
            $"Cursor should be on a word character after double-click, not '{fullText[cursorVal]}' at offset {cursorVal}");

        // The selected text (via SelectionRange) should be pure word chars.
        // After one-shot adjustment, SelectionRange is one char short (cursor on last char),
        // which is correct — the yank logic adds +1 to compensate.
        var selected = doc.GetText(_state.IlEditorState!.Cursor.SelectionRange);
        Assert.IsGreaterThan(0, selected.Length, "Selection must not be empty");
        Assert.IsTrue(selected.All(char.IsLetterOrDigit),
            $"Selected text should be a pure word, got '{selected}'");

        // Verify the yank range (cursor.Position + 1) recovers the full word
        var range = _state.IlEditorState!.Cursor.SelectionRange;
        var yankEnd = new DocumentOffset(Math.Min(
            Math.Max(range.End.Value, _state.IlEditorState!.Cursor.Position.Value + 1),
            doc.Length));
        var yankRange = new DocumentRange(range.Start, yankEnd);
        var yankText = doc.GetText(yankRange);
        Assert.StartsWith("System", yankText);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _cts?.Cancel();
        if (!TryWaitForAppExit())
        {
            _hex1bApp?.Dispose();
            _terminal?.Dispose();
            _ = TryWaitForAppExit();
        }
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        _cts?.Dispose();
    }
}
