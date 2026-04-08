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
[Collection("SampleAssemblies")]
public class IlEditorDoubleClickIntegrationTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;
    private CancellationTokenSource? _cts;

    private (Hex1bTerminal terminal, Hex1bApp app, CancellationToken ct) CreateDotsiderApp(string dllPath)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
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
    }

    /// <summary>
    /// Programmatic SelectWordAt (simulating what EditorNode.cs:281 does on double-click)
    /// through the full render pipeline. Verifies the one-shot cursor adjustment fires
    /// correctly and that yank produces the right text and cursor position.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task SelectWordAt_ThroughRenderPipeline_AdjustsCursorAndYankWorks()
    {
        var (terminal, app, ct) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);
        await Task.Delay(50, ct);

        // Navigate to IL Inspector tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("▶") || s.ContainsText("▼"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Find a method whose IL contains a dotted name
        var method = _state!.Analyzer.MethodDefs
            .Where(m => m.Rva > 0)
            .First(m => _state.IlDisassembler!.FormatDisassembly(m).Contains("System."));
        SelectMethodInTree(method);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_0000"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Find first "System." in the disassembly
        var disassembly = _state.IlDisassembler!.FormatDisassembly(method);
        var systemIdx = disassembly.IndexOf("System.", StringComparison.Ordinal);
        Assert.True(systemIdx >= 0, "Expected 'System.' in disassembly");

        var doc = _state.IlEditorState!.Document;

        // Let a render cycle seed the one-shot tracking state
        await Task.Delay(50, ct);

        // Simulate double-click: SelectWordAt is what EditorNode.cs:281 calls
        _state.IlEditorState!.SelectWordAt(new DocumentOffset(systemIdx));
        _state.App.Invalidate();

        // Wait for the render cycle to process AdjustWordSelectionCursorOneShot —
        // the one-shot pulls the cursor from the trailing '.' back onto the last
        // word character, so poll until the cursor lands on a letter/digit.
        await TestHelpers.WaitUntilAsync(
            () =>
            {
                var es = _state.IlEditorState!;
                if (!es.Cursor.HasSelection) return false;
                var pos = es.Cursor.Position.Value;
                var text = es.Document.GetText();
                return pos < text.Length && char.IsLetterOrDigit(text[pos]);
            },
            TimeSpan.FromSeconds(5));

        var fullText = doc.GetText();
        var cursorOffset = _state.IlEditorState!.Cursor.Position.Value;

        // Cursor must be on last word char ('m' of "System"), not on '.'
        Assert.True(cursorOffset < fullText.Length,
            "Cursor should be within document bounds");
        Assert.Equal('m', fullText[cursorOffset]);

        // Yank must copy the full word "System"
        var range = _state.IlEditorState!.Cursor.SelectionRange;
        var yankEnd = new DocumentOffset(Math.Min(
            Math.Max(range.End.Value, _state.IlEditorState!.Cursor.Position.Value + 1),
            doc.Length));
        var yankRange = new DocumentRange(range.Start, yankEnd);
        var yankText = doc.GetText(yankRange);
        Assert.Equal("System", yankText);

        // Post-yank cursor must land on 'm'
        var postYankCursor = new DocumentOffset(Math.Max(0, yankEnd.Value - 1));
        Assert.Equal('m', fullText[postYankCursor.Value]);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// Full mouse double-click via the automation API's DoubleClickAt, sending real
    /// SGR mouse events through the terminal. Verifies EditorNode processes the
    /// double-click, calls SelectWordAt, and the one-shot cursor adjustment fires.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task DoubleClickAt_SgrMouse_SelectsWordAndAdjustsCursor()
    {
        var (terminal, app, ct) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);
        await Task.Delay(50, ct);

        // Navigate to IL Inspector tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("▶") || s.ContainsText("▼"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Find a method with dotted IL names
        var method = _state!.Analyzer.MethodDefs
            .Where(m => m.Rva > 0)
            .First(m => _state.IlDisassembler!.FormatDisassembly(m).Contains("System."));
        SelectMethodInTree(method);

        // Wait for the IL editor to fully render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_0000"), TimeSpan.FromSeconds(5))
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
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);
        Assert.True(allMatches.Count > 0, "Expected 'System.' visible on screen");

        // Use the first match — coordinates are 0-based
        var (targetRow, targetCol) = allMatches[0];

        // Single click first to give the editor focus (tree panel has focus by default).
        // Wait for the click to be processed — the editor cursor position changes
        // when the editor receives focus and handles the mouse event.
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));
        await auto.ClickAtAsync(targetCol, targetRow, ct: ct);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => _state.IlEditorState?.Cursor.Position.Value > 0, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Double-click to select the word. Wait for HasSelection AND for the
        // AdjustWordSelectionCursorOneShot to fire (runs on the next Build after
        // selection, pulling the cursor back from punctuation to a word character).
        await auto.DoubleClickAtAsync(targetCol, targetRow, ct: ct);
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
        Assert.True(_state.IlEditorState!.Cursor.HasSelection,
            "Double-click should create a selection");

        // Cursor must be on a word character, not punctuation
        var doc = _state.IlEditorState!.Document;
        var fullText = doc.GetText();
        var cursorVal = _state.IlEditorState!.Cursor.Position.Value;
        Assert.True(cursorVal < fullText.Length,
            "Cursor should be within document bounds");
        Assert.True(char.IsLetterOrDigit(fullText[cursorVal]),
            $"Cursor should be on a word character after double-click, not '{fullText[cursorVal]}' at offset {cursorVal}");

        // The selected text (via SelectionRange) should be pure word chars.
        // After one-shot adjustment, SelectionRange is one char short (cursor on last char),
        // which is correct — the yank logic adds +1 to compensate.
        var selected = doc.GetText(_state.IlEditorState!.Cursor.SelectionRange);
        Assert.True(selected.Length > 0, "Selection must not be empty");
        Assert.True(selected.All(char.IsLetterOrDigit),
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
