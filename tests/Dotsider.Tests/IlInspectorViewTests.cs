using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Integration tests for the IL Inspector view (Tab 3).
/// </summary>
[Collection("SampleAssemblies")]
public class IlInspectorViewTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;

    private (Hex1bTerminal terminal, Hex1bApp app) CreateDotsiderApp(string dllPath, [System.Runtime.CompilerServices.CallerMemberName] string? testName = null)
    {
        TestHelpers.Diag($"Creating app for {Path.GetFileName(dllPath)}", testName);
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(120, 30)
            .Build();
        DotsiderApp? dotsiderApp = null;
        var renderCount = 0;
        _hex1bApp = new Hex1bApp(
            ctx =>
            {
                renderCount++;
                if (renderCount <= 3)
                    TestHelpers.Diag($"Render #{renderCount}", testName);
                _state ??= new DotsiderState(_hex1bApp!, dllPath);
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
    /// After clicking in the IL editor and switching tabs, returning to IL
    /// must focus the tree table so arrow keys navigate methods, not the editor cursor.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task Tab3_TreeFocusedOnReturn_AfterEditorHadFocus()
    {
        var ct = TestContext.Current.CancellationToken;
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        // Navigate to IL Inspector tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("▶") || s.ContainsText("▼"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Find and select the ToTitleCase method programmatically
        var toTitleCase = _state!.Analyzer.MethodDefs.First(m => m.Name == "ToTitleCase");
        var typeDef = _state.Analyzer.TypeDefs.First(t => t.FullName == toTitleCase.DeclaringType);
        var ns = !string.IsNullOrEmpty(typeDef.Namespace) ? typeDef.Namespace : "(global)";
        _state.IlTreeExpansionState[$"ns:{ns}"] = true;
        _state.IlTreeExpansionState[$"type:{toTitleCase.DeclaringType}"] = true;
        _state.IlSelectedMethod = toTitleCase;
        _state.IlFocusedTreeKey = $"method:{toTitleCase.Token}";
        _state.App.Invalidate();

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_0000"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Click in editor to give it focus
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(50, 15)
            .Build()
            .ApplyAsync(terminal, ct);

        // Switch to Strings tab then back to IL
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D4)
            .WaitUntil(s => s.ContainsText("User Strings") || s.ContainsText("Metadata"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("IL_0000"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Selected method must be preserved after tab round-trip
        var selectedBefore = _state!.IlSelectedMethod;
        Assert.NotNull(selectedBefore);
        Assert.Equal("ToTitleCase", selectedBefore!.Name);

        // Capture editor cursor before DownArrow
        var cursorBefore = _state.IlEditorState?.Cursor.Position;

        // Press DownArrow — should move table focus, not editor cursor
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.DownArrow)
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        // Editor cursor must not have moved (table consumed the key, not editor)
        Assert.Equal(cursorBefore, _state.IlEditorState?.Cursor.Position);

        await runTask.ContinueWith(_ => { }, ct);
    }

    /// <summary>
    /// Cross-view NavigateToIlMethod must set the tree table's focused row key
    /// to the jumped-to method, and the method must be selected.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task Tab3_CrossViewJump_FocusesTree()
    {
        var ct = TestContext.Current.CancellationToken;
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        // Go to IL tab, select a method programmatically, click in editor
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("▶") || s.ContainsText("▼"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Select a method programmatically and expand its tree path
        var firstMethod = _state!.Analyzer.MethodDefs.First(m => m.Rva > 0);
        var firstTypeDef = _state.Analyzer.TypeDefs.First(t => t.FullName == firstMethod.DeclaringType);
        var firstNs = !string.IsNullOrEmpty(firstTypeDef.Namespace) ? firstTypeDef.Namespace : "(global)";
        _state.IlTreeExpansionState[$"ns:{firstNs}"] = true;
        _state.IlTreeExpansionState[$"type:{firstMethod.DeclaringType}"] = true;
        _state.IlSelectedMethod = firstMethod;
        _state.IlFocusedTreeKey = $"method:{firstMethod.Token}";
        _state.App.Invalidate();

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_0000") || s.ContainsText("IL_"), TimeSpan.FromSeconds(10))
            .ClickAt(50, 15) // Focus editor
            .Build()
            .ApplyAsync(terminal, ct);

        // Now go to PE/Metadata MethodDef tab
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D2)
            .WaitUntil(s => s.ContainsText("PE Headers") || s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Trigger cross-view jump
        _state!.PeSubTab = PeSubTabId.MethodDef;
        var method = _state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        _state.NavigateToIlMethod(method);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // The jumped-to method must be selected in state
        Assert.Equal(method, _state.IlSelectedMethod);
        // The focused tree key must point to the jumped-to method row
        Assert.Equal($"method:{method.Token}", _state.IlFocusedTreeKey);
        // The method's namespace and type must be expanded
        var typeDef = _state.Analyzer.TypeDefs.First(t => t.FullName == method.DeclaringType);
        var ns = !string.IsNullOrEmpty(typeDef.Namespace) ? typeDef.Namespace : "(global)";
        Assert.True(_state.IlTreeExpansionState[$"ns:{ns}"],
            "Jumped-to method's namespace must be expanded");
        Assert.True(_state.IlTreeExpansionState[$"type:{method.DeclaringType}"],
            "Jumped-to method's type must be expanded");

        // DownArrow should be consumed by the table, not the editor
        var cursorBefore = _state.IlEditorState?.Cursor.Position;
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.DownArrow)
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        // Editor cursor must not have moved (table consumed the key)
        Assert.Equal(cursorBefore, _state.IlEditorState?.Cursor.Position);

        await runTask.ContinueWith(_ => { }, ct);
    }

    /// <summary>
    /// Cross-view jump must sync the inner ListNode.SelectedIndex to the jumped-to method row.
    /// This catches the stale-selection bug where the list stays on row 0 after a jump.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task Tab3_CrossViewJump_SyncsListNodeSelectedIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        // Start on IL tab — list selection defaults to row 0
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("▶") || s.ContainsText("▼"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Switch to PE/Metadata
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D2)
            .WaitUntil(s => s.ContainsText("PE Headers") || s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Pick a method that is NOT at row 0 in the flattened tree
        _state!.PeSubTab = PeSubTabId.MethodDef;
        var targetMethod = _state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        _state.NavigateToIlMethod(targetMethod);

        // Wait for IL tab to render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Compute the expected row index from the flattened tree
        var rows = Views.IlInspectorView.BuildTreeRows(_state);
        var expectedKey = $"method:{targetMethod.Token}";
        var expectedIndex = rows.FindIndex(r => r.Key == expectedKey);
        Assert.True(expectedIndex >= 0,
            $"Method {targetMethod.Name} must appear in the flattened tree rows");

        // The actual ListNode.SelectedIndex must match
        Assert.Equal(expectedKey, _state.IlFocusedTreeKey);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);
        await runTask.ContinueWith(_ => { }, ct);
    }

    /// <summary>
    /// RightArrow expands a collapsed namespace/type row and LeftArrow collapses it.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task Tab3_LeftRightArrow_ExpandCollapseTreeRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        // Go to IL tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("▶") || s.ContainsText("▼"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Find a type that starts collapsed
        var firstType = _state!.Analyzer.TypeDefs.First(t =>
            _state.Analyzer.MethodDefs.Any(m => m.DeclaringType == t.FullName));
        var typeKey = $"type:{firstType.FullName}";

        // Focus the type row programmatically
        _state.IlFocusedTreeKey = typeKey;
        _state.App.Invalidate();

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText(firstType.Name), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Type should start collapsed (default)
        Assert.False(Views.IlInspectorView.GetExpansionState(_state, typeKey, defaultExpanded: false),
            "Type should start collapsed");

        // RightArrow expands
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .Build()
            .ApplyAsync(terminal, ct);
        await Task.Delay(50, ct);

        Assert.True(_state.IlTreeExpansionState.TryGetValue(typeKey, out var expanded) && expanded,
            "RightArrow must expand the focused type row");

        // LeftArrow collapses
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.LeftArrow)
            .Build()
            .ApplyAsync(terminal, ct);
        await Task.Delay(50, ct);

        Assert.True(_state.IlTreeExpansionState.TryGetValue(typeKey, out var collapsed) && !collapsed,
            "LeftArrow must collapse the focused type row");

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);
        await runTask.ContinueWith(_ => { }, ct);
    }

    public void Dispose()
    {
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        GC.SuppressFinalize(this);
    }
}
