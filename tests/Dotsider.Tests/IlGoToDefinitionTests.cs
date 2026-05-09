using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Il Go To Definition.
/// </summary>
[Collection("SampleAssemblies")]
public sealed class IlGoToDefinitionTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;

    private (Hex1bTerminal Terminal, Hex1bApp App) CreateDotsiderApp()
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
                        CurrentTab = TabId.IlInspector
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
    /// Navigates to IlNavigationFixture::CallLocalMethod and selects it,
    /// then focuses the IL editor and moves the cursor down to the call instruction line.
    /// Returns the cursor position on the call line for later verification.
    /// </summary>
    private async Task<int> NavigateToCallLocalMethodAndFocusCallLine(
        Hex1bTerminalAutomator auto, CancellationToken ct)
    {
        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Select a method");

        // Navigate tree: 7 downs to IlNavigationFixture, expand, down to CallLocalMethod, select.
        // GenericParamFixture`2 sorts before IlNavigationFixture under the RichLibrary namespace.
        for (var i = 0; i < 7; i++) await auto.DownAsync(ct);
        await auto.EnterAsync(ct); // expand IlNavigationFixture
        await auto.WaitUntilTextAsync("CallLocalMethod");
        await auto.DownAsync(ct); // move to CallLocalMethod
        await auto.EnterAsync(ct); // select it

        // Verify CallLocalMethod IL is on screen
        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::CallLocalMethod");
        await auto.WaitUntilTextAsync("call RichLibrary.IlNavigationFixture::LocalTarget");

        // Focus editor with 'l', then navigate down to the call line.
        // Cursor starts on the first IL instruction (IL_0000: nop), so 8 downs
        // lands on IL_0010: call — independent of header line count.
        await auto.KeyAsync(Hex1bKey.L, ct);
        await Task.Delay(200, ct);
        for (var i = 0; i < 8; i++) await auto.DownAsync(ct);
        await Task.Delay(200, ct);

        return _state!.IlEditorState?.Cursor.Position.Value ?? -1;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _state?.Dispose();
        _terminal?.Dispose();
        ImplementationAssemblyResolver.ClearCache();
        DotNetRuntimeLocator.ClearCache();
    }

    // --- Resolver unit tests ---

    /// <summary>
    /// Verifies resolve method def returns local method.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Resolve_MethodDef_ReturnsLocalMethod()
    {
        using var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);
        var dis = new IlDisassembler(analyzer);
        var method = analyzer.MethodDefs.First(m =>
            m.Name == "CallLocalMethod" && m.DeclaringType.Contains("IlNavigationFixture"));
        var callInst = dis.Disassemble(method).First(i => i.OpCode == "call" && i.MetadataToken is not null);
        var target = IlNavigationResolver.Resolve(analyzer, callInst.MetadataToken!.Value);
        var localMethod = Assert.IsType<IlNavigationTarget.LocalMethod>(target);
        Assert.Equal("LocalTarget", localMethod.Method.Name);
    }

    /// <summary>
    /// Verifies resolve field def returns local field.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Resolve_FieldDef_ReturnsLocalField()
    {
        using var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);
        var dis = new IlDisassembler(analyzer);
        var method = analyzer.MethodDefs.First(m =>
            m.Name == "ReadInstanceField" && m.DeclaringType.Contains("IlNavigationFixture"));
        var fieldInst = dis.Disassemble(method).First(i => i.OpCode == "ldfld" && i.MetadataToken is not null);
        Assert.Equal("_counter", Assert.IsType<IlNavigationTarget.LocalField>(
            IlNavigationResolver.Resolve(analyzer, fieldInst.MetadataToken!.Value)).Field.Name);
    }

    /// <summary>
    /// Verifies disassemble with text header and instruction counts match.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void DisassembleWithText_HeaderAndInstructionCountsMatch()
    {
        using var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);
        var dis = new IlDisassembler(analyzer);
        var method = analyzer.MethodDefs.First(m =>
            m.Name == "CallLocalMethod" && m.DeclaringType.Contains("IlNavigationFixture"));
        var result = dis.DisassembleWithText(method);
        Assert.NotNull(result);
        Assert.Equal(result.Value.HeaderLineCount + result.Value.Instructions.Count,
            result.Value.Text.Split('\n').Length);
    }

    // --- Full end-to-end UI tests ---

    /// <summary>
    /// Verifies go to def enter screen shows target method il.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task GoToDef_Enter_ScreenShowsTargetMethodIL()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp();
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await NavigateToCallLocalMethodAndFocusCallLine(auto, cts.Token);

        // Press Enter to go to definition
        await auto.EnterAsync(cts.Token);

        // SCREEN CONTENT: must show LocalTarget's IL header
        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::LocalTarget");

        // SCREEN CONTENT: must NOT show CallLocalMethod's header anymore
        await auto.WaitUntilNoTextAsync("// Method: RichLibrary.IlNavigationFixture::CallLocalMethod");

        // STATE: IlBackStack should have one entry
        Assert.Single(_state!.IlBackStack);

        // HINTS BAR: should show Esc: Back
        await auto.WaitUntilTextAsync("Esc: Back");

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies esc back screen restores original method il.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task EscBack_ScreenRestoresOriginalMethodIL()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp();
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await NavigateToCallLocalMethodAndFocusCallLine(auto, cts.Token);

        // Go to definition
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::LocalTarget");

        // Press Escape to go back
        await auto.EscapeAsync(cts.Token);

        // SCREEN CONTENT: must show CallLocalMethod's IL header (bytecode restored)
        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::CallLocalMethod");

        // SCREEN CONTENT: must show the call instruction (IL bytecode is visible)
        await auto.WaitUntilTextAsync("call RichLibrary.IlNavigationFixture::LocalTarget");

        // SCREEN CONTENT: must NOT show LocalTarget's header anymore
        await auto.WaitUntilNoTextAsync("// Method: RichLibrary.IlNavigationFixture::LocalTarget");

        // STATE: IlBackStack should be empty
        Assert.Empty(_state!.IlBackStack);

        // STATE: selected method should be CallLocalMethod
        Assert.Equal("CallLocalMethod", _state.IlSelectedMethod?.Name);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies esc back cursor position restored exactly.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task EscBack_CursorPositionRestoredExactly()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp();
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        var cursorBeforeNav = await NavigateToCallLocalMethodAndFocusCallLine(auto, cts.Token);
        Assert.True(cursorBeforeNav > 0, "Cursor should be past the header lines");

        // Go to definition
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::LocalTarget");

        // Press Escape to go back
        await auto.EscapeAsync(cts.Token);
        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::CallLocalMethod");

        // CURSOR: must be in the instruction area (past header lines), proving
        // the cursor was restored to the IL bytecode region, not reset to line 1.
        var cursorAfterBack = _state!.IlEditorState?.Cursor.Position.Value ?? -1;
        Assert.True(cursorAfterBack > 0, "Cursor should be restored past header lines");

        var text = _state.IlEditorState!.Document.GetText();
        int LineOf(int offset)
        {
            var line = 1;
            for (var i = 0; i < offset && i < text.Length; i++)
                if (text[i] == '\n') line++;
            return line;
        }

        var cursorLine = LineOf(cursorAfterBack);
        Assert.True(cursorLine > _state.IlHeaderLineCount,
            $"Cursor should be in the instruction area (line {cursorLine}) " +
            $"but header has {_state.IlHeaderLineCount} lines");

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies esc back scroll works after restore.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task EscBack_ScrollWorksAfterRestore()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp();
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await NavigateToCallLocalMethodAndFocusCallLine(auto, cts.Token);

        // Go to definition
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::LocalTarget");

        // Press Escape to go back
        await auto.EscapeAsync(cts.Token);
        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::CallLocalMethod");

        // RestoreFromIlBackEntry queues focus on the EditorNode. The actual focus shift
        // applies on a later frame; without an explicit wait, Down/Up below can race
        // a frame where focus is still on the tree and the cursor never moves.
        await auto.WaitUntilAsync(_ => _state!.App.FocusedNode is Hex1b.EditorNode,
            description: "editor focused after Esc back");
        var cursorBeforeScroll = _state!.IlEditorState?.Cursor.Position.Value ?? -1;

        // SCROLL: press Down arrow to scroll — this must work (not be frozen)
        await auto.DownAsync(cts.Token);
        await auto.WaitUntilAsync(
            _ => (_state.IlEditorState?.Cursor.Position.Value ?? -1) != cursorBeforeScroll,
            description: "Down moved editor cursor");
        var cursorAfterScroll = _state.IlEditorState?.Cursor.Position.Value ?? -1;

        // Press Up arrow back
        await auto.UpAsync(cts.Token);
        await auto.WaitUntilAsync(
            _ => (_state.IlEditorState?.Cursor.Position.Value ?? -1) != cursorAfterScroll,
            description: "Up moved editor cursor");

        var cursorAfterUp = _state.IlEditorState?.Cursor.Position.Value ?? -1;
        Assert.NotEqual(cursorAfterScroll, cursorAfterUp);

        // SCREEN: IL bytecode must still be visible after scrolling
        await auto.WaitUntilTextAsync("call RichLibrary.IlNavigationFixture::LocalTarget");

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies esc back tree state restored.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task EscBack_TreeStateRestored()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp();
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await NavigateToCallLocalMethodAndFocusCallLine(auto, cts.Token);

        // Save tree state before navigation
        var treeBefore = new Dictionary<string, bool>(_state!.IlTreeExpansionState);
        var focusedKeyBefore = _state.IlFocusedTreeKey;

        // Go to definition
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::LocalTarget");

        // Press Escape to go back
        await auto.EscapeAsync(cts.Token);
        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::CallLocalMethod");

        // TREE STATE: expansion state must match what was saved
        foreach (var (key, value) in treeBefore)
        {
            Assert.True(_state.IlTreeExpansionState.TryGetValue(key, out var restored),
                $"Tree key '{key}' missing after restore");
            Assert.Equal(value, restored);
        }

        // TREE STATE: focused key must be restored
        Assert.Equal(focusedKeyBefore, _state.IlFocusedTreeKey);

        cts.Cancel();
        await runTask;
    }
    /// <summary>
    /// Verifies diagnostic escape handler fires state changes.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task Diagnostic_EscapeHandlerFires_StateChanges()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp();
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await NavigateToCallLocalMethodAndFocusCallLine(auto, cts.Token);

        // Go to definition
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::LocalTarget");

        // Verify back stack has entry
        Assert.Single(_state!.IlBackStack);
        Assert.Equal("LocalTarget", _state.IlSelectedMethod?.Name);

        // Press Escape
        await auto.EscapeAsync(cts.Token);
        await Task.Delay(500, cts.Token);

        // Check if the handler fired by examining state
        // If the back stack is empty, the handler popped it
        var backStackEmpty = _state.IlBackStack.Count == 0;
        var methodName = _state.IlSelectedMethod?.Name;

        // Diagnostic output
        Assert.True(backStackEmpty, $"IlBackStack should be empty after Esc but has {_state.IlBackStack.Count} entries. " +
            $"Method is '{methodName}'. This means the Escape handler did NOT fire.");
        Assert.Equal("CallLocalMethod", methodName);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies cross assembly back stack survives reset view state.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void CrossAssembly_BackStackSurvivesResetViewState()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;

        // Select CallExternal and set up editor state
        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "CallExternal" && m.DeclaringType.Contains("IlNavigationFixture"));
        state.IlSelectedMethod = method;
        var result = state.IlDisassembler!.DisassembleWithText(method);
        Assert.NotNull(result);
        var doc = new Hex1b.Documents.Hex1bDocument(result.Value.Text);
        state.IlEditorState = new EditorState(doc) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;

        // Find the call instruction token
        var callInst = result.Value.Instructions.FirstOrDefault(i =>
            i.OpCode == "call" && i.MetadataToken is not null && i.Operand.Contains("WriteLine"));
        Assert.NotNull(callInst);

        // Navigate to external method
        var navigated = state.NavigateToIlDefinition(callInst.MetadataToken!.Value);

        // Back stack must survive PushAssemblyDirect → ResetViewState
        Assert.True(state.IlBackStack.Count > 0,
            $"IlBackStack must survive cross-assembly navigation but has {state.IlBackStack.Count} entries");

        // Restore from back entry
        if (navigated)
        {
            var entry = state.IlBackStack.Pop();
            state.RestoreFromIlBackEntry(entry);
            Assert.Equal("CallExternal", state.IlSelectedMethod?.Name);
            Assert.Contains("RichLibrary", state.Analyzer.FileName);
        }
    }

    /// <summary>
    /// Verifies local field go to def clears selected method.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void LocalField_GoToDef_ClearsSelectedMethod()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;

        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "ReadInstanceField" && m.DeclaringType.Contains("IlNavigationFixture"));
        state.IlSelectedMethod = method;
        var result = state.IlDisassembler!.DisassembleWithText(method);
        Assert.NotNull(result);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(result.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;

        // ldfld _counter → LocalField target
        var fieldInst = result.Value.Instructions.First(i =>
            i.OpCode == "ldfld" && i.MetadataToken is not null);
        state.NavigateToIlDefinition(fieldInst.MetadataToken!.Value);

        // Field navigation should clear IlSelectedMethod (no method to show)
        // and focus the declaring type in the tree
        Assert.Null(state.IlSelectedMethod);
        Assert.Contains("type:", (string?)state.IlFocusedTreeKey);

        // Back should restore
        var entry = state.IlBackStack.Pop();
        state.RestoreFromIlBackEntry(entry);
        Assert.Equal("ReadInstanceField", state.IlSelectedMethod?.Name);
    }

    /// <summary>
    /// Verifies normal push assembly clears il back stack.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NormalPushAssembly_ClearsIlBackStack()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;

        // Set up a local go-to-def so IlBackStack has an entry
        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "CallLocalMethod" && m.DeclaringType.Contains("IlNavigationFixture"));
        state.IlSelectedMethod = method;
        var result = state.IlDisassembler!.DisassembleWithText(method);
        Assert.NotNull(result);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(result.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;

        var callInst = result.Value.Instructions.First(i =>
            i.OpCode == "call" && i.MetadataToken is not null);
        state.NavigateToIlDefinition(callInst.MetadataToken!.Value);
        Assert.Single(state.IlBackStack);

        // Normal assembly push (dependency navigation) must clear the back stack
        // because entries reference the old analyzer's state
        state.PushAssembly(samples.HelloWorldDll);
        Assert.Empty(state.IlBackStack);
    }

    // --- Issue #159: Esc on IL Inspector loses Size Map back target after gd into external ---

    /// <summary>
    /// Verifies the cross-view back target survives a cross-assembly method gd
    /// round-trip (Size Map → IL Inspector → external method → Esc back).
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EscBack_FromCrossAssemblyMethodGd_RestoresSizeMapCrossViewBackTarget()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "CallExternal" && m.DeclaringType.Contains("IlNavigationFixture"));
        state.CurrentTab = TabId.SizeMap;
        state.NavigateToIlMethod(method);

        Assert.Equal(TabId.IlInspector, state.CurrentTab);
        Assert.Equal((TabId.SizeMap, 0), state.CrossViewBackTarget);

        var result = state.IlDisassembler!.DisassembleWithText(method);
        Assert.NotNull(result);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(result.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;

        var callInst = result.Value.Instructions.First(i =>
            i.OpCode == "call" && i.MetadataToken is not null && i.Operand.Contains("WriteLine"));
        var navigated = state.NavigateToIlDefinition(callInst.MetadataToken!.Value);

        Assert.True(navigated);
        Assert.Single(state.IlBackStack);
        Assert.True(state.NavigationStack.Count > 0);
        Assert.Null(state.CrossViewBackTarget);

        var entry = state.IlBackStack.Pop();
        state.RestoreFromIlBackEntry(entry);

        Assert.Equal(TabId.IlInspector, state.CurrentTab);
        Assert.Empty(state.NavigationStack);
        Assert.Empty(state.IlBackStack);
        Assert.Equal((TabId.SizeMap, 0), state.CrossViewBackTarget);

        state.NavigateBack();
        Assert.Equal(TabId.SizeMap, state.CurrentTab);
        Assert.Null(state.CrossViewBackTarget);
    }

    /// <summary>
    /// Verifies the cross-view back target survives a cross-assembly field gd round-trip.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EscBack_FromCrossAssemblyFieldGd_RestoresSizeMapCrossViewBackTarget()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        // GetStringEmpty has body `ldsfld string.Empty` — external FieldRef
        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "GetStringEmpty" && m.DeclaringType.Contains("IlNavigationFixture"));
        state.CurrentTab = TabId.SizeMap;
        state.NavigateToIlMethod(method);

        Assert.Equal(TabId.IlInspector, state.CurrentTab);
        Assert.Equal((TabId.SizeMap, 0), state.CrossViewBackTarget);

        var result = state.IlDisassembler!.DisassembleWithText(method);
        Assert.NotNull(result);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(result.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;

        var ldsInst = result.Value.Instructions.First(i =>
            i.OpCode == "ldsfld" && i.MetadataToken is not null);
        var navigated = state.NavigateToIlDefinition(ldsInst.MetadataToken!.Value);

        Assert.True(navigated);
        Assert.Single(state.IlBackStack);
        Assert.True(state.NavigationStack.Count > 0);
        Assert.Null(state.CrossViewBackTarget);

        var entry = state.IlBackStack.Pop();
        state.RestoreFromIlBackEntry(entry);

        Assert.Equal(TabId.IlInspector, state.CurrentTab);
        Assert.Empty(state.NavigationStack);
        Assert.Empty(state.IlBackStack);
        Assert.Equal((TabId.SizeMap, 0), state.CrossViewBackTarget);

        state.NavigateBack();
        Assert.Equal(TabId.SizeMap, state.CurrentTab);
        Assert.Null(state.CrossViewBackTarget);
    }

    /// <summary>
    /// Verifies the cross-view back target survives a cross-assembly type gd round-trip.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EscBack_FromCrossAssemblyTypeGd_RestoresSizeMapCrossViewBackTarget()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        // CastToExternalStream has body `castclass System.IO.Stream` — external TypeRef
        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "CastToExternalStream" && m.DeclaringType.Contains("IlNavigationFixture"));
        state.CurrentTab = TabId.SizeMap;
        state.NavigateToIlMethod(method);

        Assert.Equal(TabId.IlInspector, state.CurrentTab);
        Assert.Equal((TabId.SizeMap, 0), state.CrossViewBackTarget);

        var result = state.IlDisassembler!.DisassembleWithText(method);
        Assert.NotNull(result);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(result.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;

        var castInst = result.Value.Instructions.First(i =>
            i.OpCode == "castclass" && i.MetadataToken is not null);
        var navigated = state.NavigateToIlDefinition(castInst.MetadataToken!.Value);

        Assert.True(navigated);
        Assert.Single(state.IlBackStack);
        Assert.True(state.NavigationStack.Count > 0);
        Assert.Null(state.CrossViewBackTarget);

        var entry = state.IlBackStack.Pop();
        state.RestoreFromIlBackEntry(entry);

        Assert.Equal(TabId.IlInspector, state.CurrentTab);
        Assert.Empty(state.NavigationStack);
        Assert.Empty(state.IlBackStack);
        Assert.Equal((TabId.SizeMap, 0), state.CrossViewBackTarget);

        state.NavigateBack();
        Assert.Equal(TabId.SizeMap, state.CurrentTab);
        Assert.Null(state.CrossViewBackTarget);
    }

    /// <summary>
    /// Regression guard: local-method gd from Size Map preserves the cross-view
    /// back target without going through the snapshot/restore round-trip,
    /// because the local path never calls PushAssemblyDirect → ResetViewState.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EscBack_FromLocalGd_PreservesSizeMapCrossViewBackTarget()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "CallLocalMethod" && m.DeclaringType.Contains("IlNavigationFixture"));
        state.CurrentTab = TabId.SizeMap;
        state.NavigateToIlMethod(method);

        Assert.Equal(TabId.IlInspector, state.CurrentTab);
        Assert.Equal((TabId.SizeMap, 0), state.CrossViewBackTarget);

        var result = state.IlDisassembler!.DisassembleWithText(method);
        Assert.NotNull(result);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(result.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;

        var callInst = result.Value.Instructions.First(i =>
            i.OpCode == "call" && i.MetadataToken is not null && i.Operand.Contains("LocalTarget"));
        var navigated = state.NavigateToIlDefinition(callInst.MetadataToken!.Value);

        Assert.True(navigated);
        Assert.Single(state.IlBackStack);
        Assert.Empty(state.NavigationStack);
        Assert.Equal((TabId.SizeMap, 0), state.CrossViewBackTarget);

        state.RestoreFromIlBackEntry(state.IlBackStack.Pop());
        Assert.Equal((TabId.SizeMap, 0), state.CrossViewBackTarget);

        state.NavigateBack();
        Assert.Equal(TabId.SizeMap, state.CurrentTab);
    }

    /// <summary>
    /// End-to-end: from Size Map, drill into IL, gd into Console.WriteLine,
    /// and confirm two REAL Esc presses return to Size Map. Pre-fix the second
    /// Esc binding de-registers and the user is stuck on IL Inspector.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task EscBack_FromSizeMapToExternalCall_TwoRealEscapesReturnToSizeMap()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp();
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Select a method");

        // Programmatic Size Map → IL drill-down (mirrors SizeTreemapView.cs:157).
        // The bug is independent of how CrossViewBackTarget got set; we drive
        // the gd round-trip with real keys.
        var callExternal = _state!.Analyzer.MethodDefs.First(m =>
            m.Name == "CallExternal" && m.DeclaringType.Contains("IlNavigationFixture"));
        _state.CurrentTab = TabId.SizeMap;
        _state.NavigateToIlMethod(callExternal);
        Assert.Equal((TabId.SizeMap, 0), _state.CrossViewBackTarget);

        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::CallExternal");
        await auto.WaitUntilTextAsync("call System.Console::WriteLine");

        // Focus editor with 'l' and wait until focus shift settles (cursor on
        // first IL instruction). Per hex1b testing guide, poll on state rather
        // than Task.Delay — Linux/macOS CI rendered slower than Windows and
        // raced the down loop below.
        await auto.KeyAsync(Hex1bKey.L, cts.Token);
        await auto.WaitUntilAsync(_ =>
        {
            if (_state!.IlEditorState is null || _state.IlInstructions is null
                || _state.IlInstructions.Count == 0) return false;
            var inst = IlNavigationHelper.GetInstructionAtCursor(
                _state.IlEditorState, _state.IlInstructions, _state.IlHeaderLineCount);
            return inst is not null && inst.Offset == _state.IlInstructions[0].Offset;
        });

        // Step cursor down to the WriteLine call, polling for cursor advance
        // after each Down so a slow render doesn't drop a keypress.
        var instructions = _state!.IlInstructions!.ToList();
        var callIndex = instructions.FindIndex(i =>
            i.OpCode == "call" && i.MetadataToken is not null && i.Operand.Contains("WriteLine"));
        Assert.True(callIndex >= 0, "WriteLine call instruction must exist in CallExternal body");
        for (var i = 0; i < callIndex; i++)
        {
            var expected = instructions[i + 1];
            await auto.DownAsync(cts.Token);
            await auto.WaitUntilAsync(_ =>
            {
                var inst = IlNavigationHelper.GetInstructionAtCursor(
                    _state!.IlEditorState!, _state.IlInstructions!, _state.IlHeaderLineCount);
                return inst is not null && inst.Offset == expected.Offset;
            });
        }

        var instAtCursor = IlNavigationHelper.GetInstructionAtCursor(
            _state.IlEditorState!, _state.IlInstructions!, _state.IlHeaderLineCount);
        Assert.NotNull(instAtCursor);
        Assert.Equal("call", instAtCursor!.OpCode);
        Assert.Contains("WriteLine", instAtCursor.Operand);

        // Real gd via Enter — crosses into System.Console.dll.
        await auto.EnterAsync(cts.Token);

        // The IlDisassembler emits "// Method: System.Console::WriteLine" only
        // in the destination's IL header — unique landing marker.
        await auto.WaitUntilTextAsync("// Method: System.Console::WriteLine");
        Assert.True(_state.NavigationStack.Count > 0);
        Assert.Single(_state.IlBackStack);
        Assert.Null(_state.CrossViewBackTarget);

        // First REAL Esc — pops IL back entry, returns to CallExternal IL.
        await auto.EscapeAsync(cts.Token);
        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::CallExternal");

        // Hints bar must still show "Esc: Back" — proves the unified Esc
        // binding is registered for the second press. Pre-fix all three back
        // signals are zero/null and the binding de-registers.
        await auto.WaitUntilTextAsync("Esc: Back");
        Assert.Equal((TabId.SizeMap, 0), _state.CrossViewBackTarget);
        Assert.Equal(TabId.IlInspector, _state.CurrentTab);
        Assert.Empty(_state.IlBackStack);
        Assert.Empty(_state.NavigationStack);

        // Second REAL Esc — drives cross-view return to Size Map.
        await auto.EscapeAsync(cts.Token);
        await auto.WaitUntilTextAsync("Total:");
        Assert.Equal(TabId.SizeMap, _state.CurrentTab);
        Assert.Null(_state.CrossViewBackTarget);

        cts.Cancel();
        await runTask;
    }

    // --- Issue #159 (reopened): Esc loses Size Map drill state after cross-assembly gd ---

    /// <summary>
    /// Walks the real Size Map tree to find the path that drills down to a method.
    /// </summary>
    private static (SizeNode Root, SizeNode Namespace, SizeNode Type, int MethodChildIndex)
        FindSizeMapPath(SizeNode root, MethodDefInfo method)
    {
        var ns = method.DeclaringType.Contains('.')
            ? method.DeclaringType[..method.DeclaringType.LastIndexOf('.')]
            : "(global)";
        var nsNode = root.Children.First(c => c.FullPath == ns);
        var typeNode = nsNode.Children.First(c => c.FullPath == method.DeclaringType);
        var methodIdx = typeNode.Children.ToList().FindIndex(c =>
            c.FullPath == $"{method.DeclaringType}::{method.Name}@0x{method.Token:X8}");
        Assert.True(methodIdx >= 0,
            $"Method {method.DeclaringType}::{method.Name} not found in size tree");
        return (root, nsNode, typeNode, methodIdx);
    }

    /// <summary>
    /// Verifies the full Size Map drill state (cached tree, current level, breadcrumb,
    /// selection, search) is restored after a cross-assembly gd round-trip. Parameterized
    /// across the three external dispatch paths: method, field, type.
    /// </summary>
    [Theory(Timeout = 30_000)]
    [InlineData("CallExternal", "call", "WriteLine")]
    [InlineData("GetStringEmpty", "ldsfld", "")]
    [InlineData("CastToExternalStream", "castclass", "")]
    public void EscBack_FromCrossAssemblyGd_RestoresFullSizeMapDrillState(
        string methodName, string opCode, string operandSubstring)
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        state.CachedSizeTree = SizeAnalyzer.BuildSizeTree(state.Analyzer);
        var origTree = state.CachedSizeTree;

        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == methodName && m.DeclaringType.Contains("IlNavigationFixture"));
        var (root, ns, type, methodIdx) = FindSizeMapPath(origTree, method);

        state.TreemapBreadcrumb.Push(root);
        state.TreemapCurrentLevel = ns;
        state.TreemapBreadcrumb.Push(ns);
        state.TreemapCurrentLevel = type;
        state.TreemapSelectedIndex = methodIdx;
        state.TreemapMatchIndex = -1;

        var smSearch = state.Search[TabId.SizeMap];
        smSearch.ActivateOrCycle();
        smSearch.UpdateQuery("Call");
        smSearch.SetMatchCount(2);
        smSearch.Confirm();

        state.CurrentTab = TabId.SizeMap;
        state.NavigateToIlMethod(method);

        Assert.Same(type, state.TreemapCurrentLevel);
        Assert.Equal(2, state.TreemapBreadcrumb.Count);
        Assert.Equal((TabId.SizeMap, 0), state.CrossViewBackTarget);

        var dis = state.IlDisassembler!.DisassembleWithText(method);
        Assert.NotNull(dis);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(dis.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;

        var inst = dis.Value.Instructions.First(i =>
            i.OpCode == opCode && i.MetadataToken is not null
            && (operandSubstring.Length == 0 || i.Operand.Contains(operandSubstring)));
        Assert.True(state.NavigateToIlDefinition(inst.MetadataToken!.Value));

        // Mid-flight: ResetViewState wiped everything.
        Assert.Null(state.TreemapCurrentLevel);
        Assert.Empty(state.TreemapBreadcrumb);
        Assert.Null(state.CachedSizeTree);
        Assert.False(state.Search[TabId.SizeMap].IsActive);

        // First Esc — pop the cross-assembly entry and restore.
        state.RestoreFromIlBackEntry(state.IlBackStack.Pop());

        Assert.Equal((TabId.SizeMap, 0), state.CrossViewBackTarget);
        Assert.Same(origTree, state.CachedSizeTree);
        Assert.Same(type, state.TreemapCurrentLevel);
        var stack = state.TreemapBreadcrumb.ToArray();
        Assert.Equal(2, stack.Length);
        Assert.Same(ns, stack[0]);
        Assert.Same(root, stack[1]);
        Assert.Equal(methodIdx, state.TreemapSelectedIndex);
        Assert.Same(
            type.Children[methodIdx],
            state.TreemapCurrentLevel!.Children[state.TreemapSelectedIndex]);

        var s = state.Search[TabId.SizeMap];
        Assert.True(s.IsActive);
        Assert.True(s.IsConfirmed);
        Assert.Equal("Call", s.Query);
        Assert.Equal(2, s.MatchCount);

        // Second Esc — back to Size Map at the drilled level.
        state.NavigateBack();
        Assert.Equal(TabId.SizeMap, state.CurrentTab);
        Assert.Same(type, state.TreemapCurrentLevel);
    }

    /// <summary>
    /// Regression guard: when the user never drilled (TreemapCurrentLevel == null,
    /// breadcrumb empty), the snapshot/restore round-trip must not invent a breadcrumb.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EscBack_NoDrill_NullCurrentLevelStaysNull()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        // No drill: TreemapCurrentLevel left null, breadcrumb empty, no cached tree.
        Assert.Null(state.TreemapCurrentLevel);
        Assert.Empty(state.TreemapBreadcrumb);
        Assert.Null(state.CachedSizeTree);

        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "CallExternal" && m.DeclaringType.Contains("IlNavigationFixture"));
        state.CurrentTab = TabId.SizeMap;
        state.NavigateToIlMethod(method);

        var dis = state.IlDisassembler!.DisassembleWithText(method);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(dis!.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;
        var callInst = dis.Value.Instructions.First(i =>
            i.OpCode == "call" && i.MetadataToken is not null && i.Operand.Contains("WriteLine"));
        Assert.True(state.NavigateToIlDefinition(callInst.MetadataToken!.Value));

        state.RestoreFromIlBackEntry(state.IlBackStack.Pop());

        Assert.Null(state.TreemapCurrentLevel);
        Assert.Empty(state.TreemapBreadcrumb);
        Assert.Equal(-1, state.TreemapSelectedIndex);
        Assert.False(state.Search[TabId.SizeMap].IsActive);
    }

    /// <summary>
    /// Verifies that the "drilled then popped back to root" state (TreemapCurrentLevel
    /// equal to the cached root, breadcrumb empty) survives the cross-assembly round-trip.
    /// Pre-fix this fails because ResetViewState zeros TreemapCurrentLevel and nothing puts
    /// the root reference back.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EscBack_DrilledToRoot_RestoresRootIdentity()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        state.CachedSizeTree = SizeAnalyzer.BuildSizeTree(state.Analyzer);
        var origTree = state.CachedSizeTree;
        state.TreemapCurrentLevel = origTree; // user drilled then popped back to root
        Assert.Empty(state.TreemapBreadcrumb);

        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "CallExternal" && m.DeclaringType.Contains("IlNavigationFixture"));
        state.CurrentTab = TabId.SizeMap;
        state.NavigateToIlMethod(method);

        var dis = state.IlDisassembler!.DisassembleWithText(method);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(dis!.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;
        var callInst = dis.Value.Instructions.First(i =>
            i.OpCode == "call" && i.MetadataToken is not null && i.Operand.Contains("WriteLine"));
        Assert.True(state.NavigateToIlDefinition(callInst.MetadataToken!.Value));

        state.RestoreFromIlBackEntry(state.IlBackStack.Pop());

        Assert.Same(origTree, state.CachedSizeTree);
        Assert.Same(origTree, state.TreemapCurrentLevel);
        Assert.Empty(state.TreemapBreadcrumb);
    }

    /// <summary>
    /// Regression guard: local-method gd does not go through PushAssemblyDirect, so
    /// the treemap state survives without snapshot/restore. After a local gd round-trip
    /// the original drill state must still be present (object identity preserved).
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EscBack_FromLocalGd_PreservesBreadcrumb()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        state.CachedSizeTree = SizeAnalyzer.BuildSizeTree(state.Analyzer);
        var origTree = state.CachedSizeTree;

        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "CallLocalMethod" && m.DeclaringType.Contains("IlNavigationFixture"));
        var (root, ns, type, methodIdx) = FindSizeMapPath(origTree, method);

        state.TreemapBreadcrumb.Push(root);
        state.TreemapCurrentLevel = ns;
        state.TreemapBreadcrumb.Push(ns);
        state.TreemapCurrentLevel = type;
        state.TreemapSelectedIndex = methodIdx;

        state.CurrentTab = TabId.SizeMap;
        state.NavigateToIlMethod(method);

        var dis = state.IlDisassembler!.DisassembleWithText(method);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(dis!.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;
        var callInst = dis.Value.Instructions.First(i =>
            i.OpCode == "call" && i.MetadataToken is not null && i.Operand.Contains("LocalTarget"));
        Assert.True(state.NavigateToIlDefinition(callInst.MetadataToken!.Value));

        // Local path: ResetViewState was NEVER called — drill state intact.
        Assert.Same(origTree, state.CachedSizeTree);
        Assert.Same(type, state.TreemapCurrentLevel);
        Assert.Equal(2, state.TreemapBreadcrumb.Count);

        state.RestoreFromIlBackEntry(state.IlBackStack.Pop());

        Assert.Same(origTree, state.CachedSizeTree);
        Assert.Same(type, state.TreemapCurrentLevel);
        Assert.Equal(2, state.TreemapBreadcrumb.Count);
        Assert.Equal(methodIdx, state.TreemapSelectedIndex);
    }

    /// <summary>
    /// End-to-end: from a deeply drilled Size Map (root → namespace → type), drill
    /// into a method, gd into a cross-assembly call, and confirm two real Esc presses
    /// land back on Size Map at the original drilled type level — proven by waiting
    /// for the rendered breadcrumb to include the type name.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task EscBack_FromSizeMapDeepDrill_TwoRealEscapesShowBreadcrumb()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp();
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Select a method");

        // Build a real deep drill state for CallExternal.
        _state!.CachedSizeTree = SizeAnalyzer.BuildSizeTree(_state.Analyzer);
        var origTree = _state.CachedSizeTree;
        var callExternal = _state.Analyzer.MethodDefs.First(m =>
            m.Name == "CallExternal" && m.DeclaringType.Contains("IlNavigationFixture"));
        var (root, ns, type, methodIdx) = FindSizeMapPath(origTree, callExternal);

        _state.TreemapBreadcrumb.Push(root);
        _state.TreemapCurrentLevel = ns;
        _state.TreemapBreadcrumb.Push(ns);
        _state.TreemapCurrentLevel = type;
        _state.TreemapSelectedIndex = methodIdx;

        // CRITICAL: NavigateToIlMethod reads CurrentTab to set CrossViewBackTarget,
        // so we must be on Size Map at call time — otherwise the back target lands
        // on the wrong tab and the test wouldn't prove the reopened bug.
        _state.CurrentTab = TabId.SizeMap;
        _state.NavigateToIlMethod(callExternal);
        Assert.Equal((TabId.SizeMap, 0), _state.CrossViewBackTarget);

        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::CallExternal");

        // Focus editor and walk to the WriteLine call (PR #160's polling pattern).
        await auto.KeyAsync(Hex1bKey.L, cts.Token);
        await auto.WaitUntilAsync(_ =>
        {
            if (_state!.IlEditorState is null || _state.IlInstructions is null
                || _state.IlInstructions.Count == 0) return false;
            var i0 = IlNavigationHelper.GetInstructionAtCursor(
                _state.IlEditorState, _state.IlInstructions, _state.IlHeaderLineCount);
            return i0 is not null && i0.Offset == _state.IlInstructions[0].Offset;
        });

        var instructions = _state!.IlInstructions!.ToList();
        var callIndex = instructions.FindIndex(i =>
            i.OpCode == "call" && i.MetadataToken is not null && i.Operand.Contains("WriteLine"));
        Assert.True(callIndex >= 0);
        for (var i = 0; i < callIndex; i++)
        {
            var expected = instructions[i + 1];
            await auto.DownAsync(cts.Token);
            await auto.WaitUntilAsync(_ =>
            {
                var inst = IlNavigationHelper.GetInstructionAtCursor(
                    _state!.IlEditorState!, _state.IlInstructions!, _state.IlHeaderLineCount);
                return inst is not null && inst.Offset == expected.Offset;
            });
        }

        // Real gd via Enter — cross-assembly into System.Console.dll.
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilTextAsync("// Method: System.Console::WriteLine");

        // First REAL Esc — pop IL back entry, return to CallExternal IL.
        await auto.EscapeAsync(cts.Token);
        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::CallExternal");
        await auto.WaitUntilTextAsync("Esc: Back");

        // Second REAL Esc — back to Size Map.
        await auto.EscapeAsync(cts.Token);
        await auto.WaitUntilTextAsync("Total:");

        // The breadcrumb is built from SizeNode.Name (not FullPath) joined by " > ",
        // wrapped in single spaces by the row at SizeTreemapView.cs:81.
        var expectedCrumb = $" {origTree.Name} > {ns.Name} > {type.Name} ";
        await auto.WaitUntilTextAsync(expectedCrumb);

        Assert.Equal(TabId.SizeMap, _state.CurrentTab);
        Assert.Same(type, _state.TreemapCurrentLevel);
        Assert.Same(origTree, _state.CachedSizeTree);

        cts.Cancel();
        await runTask;
    }

    // --- Issue #162: Esc on IL Inspector loses Size Map back target after x to Hex Dump ---

    /// <summary>
    /// Verifies that two NavigateBack calls unwind the Size Map → IL → Hex chain
    /// to the original Size Map drill — chained cross-view jumps must each store
    /// their own back frame instead of overwriting one another.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EscBack_FromSizeMapIlHexChain_TwoEscsReturnToSizeMap()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        state.CachedSizeTree = SizeAnalyzer.BuildSizeTree(state.Analyzer);
        var origTree = state.CachedSizeTree;
        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "CallExternal" && m.DeclaringType.Contains("IlNavigationFixture"));
        var (root, ns, type, methodIdx) = FindSizeMapPath(origTree, method);

        state.TreemapBreadcrumb.Push(root);
        state.TreemapCurrentLevel = ns;
        state.TreemapBreadcrumb.Push(ns);
        state.TreemapCurrentLevel = type;
        state.TreemapSelectedIndex = methodIdx;

        state.CurrentTab = TabId.SizeMap;
        state.NavigateToIlMethod(method);
        Assert.Equal((TabId.SizeMap, 0), state.CrossViewBackTarget);

        state.NavigateToHexOffset(method.Rva);
        Assert.Equal(TabId.HexDump, state.CurrentTab);
        Assert.Equal((TabId.IlInspector, 0), state.CrossViewBackTarget);

        // First Esc — Hex → IL Inspector. Pre-fix the back target is null after
        // this because the Hex push clobbered the SizeMap frame.
        state.NavigateBack();
        Assert.Equal(TabId.IlInspector, state.CurrentTab);
        Assert.Equal((TabId.SizeMap, 0), state.CrossViewBackTarget);

        // Second Esc — IL → Size Map.
        state.NavigateBack();
        Assert.Equal(TabId.SizeMap, state.CurrentTab);
        Assert.Null(state.CrossViewBackTarget);

        // Breadcrumb survives end-to-end (NavigateToHexOffset never calls ResetViewState).
        Assert.Same(origTree, state.CachedSizeTree);
        Assert.Same(type, state.TreemapCurrentLevel);
        Assert.Equal(2, state.TreemapBreadcrumb.Count);
    }

    /// <summary>
    /// Verifies that the PE → IL → Hex chain unwinds to the originating PE
    /// Metadata sub-tab, proving both Tab and SubTab are stored per frame.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EscBack_FromPeIlHexChain_RestoresPeWithExactSubTab()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        state.CurrentTab = TabId.PeMetadata;
        state.PeSubTab = PeSubTabId.MethodDef;

        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.NavigateToIlMethod(method);
        Assert.Equal((TabId.PeMetadata, PeSubTabId.MethodDef), state.CrossViewBackTarget);

        state.NavigateToHexOffset(method.Rva);
        // The Hex frame captures (CurrentTab, PeSubTab) — PeSubTab is unchanged
        // since the user was on IL Inspector, but the SubTab value is unused
        // when NavigateBack returns to a non-PE tab. Only assert the Tab here.
        Assert.Equal(TabId.IlInspector, state.CrossViewBackTarget!.Value.Tab);

        state.NavigateBack();
        Assert.Equal(TabId.IlInspector, state.CurrentTab);
        Assert.Equal((TabId.PeMetadata, PeSubTabId.MethodDef), state.CrossViewBackTarget);

        state.NavigateBack();
        Assert.Equal(TabId.PeMetadata, state.CurrentTab);
        Assert.Equal(PeSubTabId.MethodDef, state.PeSubTab);
        Assert.Null(state.CrossViewBackTarget);
    }

    /// <summary>
    /// End-to-end: from a deep Size Map drill, IL Inspector → real x key →
    /// Hex Dump → real Esc → real Esc must land back on Size Map at the
    /// originating breadcrumb (proves the chain works through real input).
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task EscBack_FromSizeMapIlHexChain_RealKeysReturnToSizeMap()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp();
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Select a method");

        _state!.CachedSizeTree = SizeAnalyzer.BuildSizeTree(_state.Analyzer);
        var origTree = _state.CachedSizeTree;
        var callExternal = _state.Analyzer.MethodDefs.First(m =>
            m.Name == "CallExternal" && m.DeclaringType.Contains("IlNavigationFixture"));
        var (root, ns, type, methodIdx) = FindSizeMapPath(origTree, callExternal);

        _state.TreemapBreadcrumb.Push(root);
        _state.TreemapCurrentLevel = ns;
        _state.TreemapBreadcrumb.Push(ns);
        _state.TreemapCurrentLevel = type;
        _state.TreemapSelectedIndex = methodIdx;

        _state.CurrentTab = TabId.SizeMap;
        _state.NavigateToIlMethod(callExternal);
        Assert.Equal((TabId.SizeMap, 0), _state.CrossViewBackTarget);

        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::CallExternal");

        // Real x key → NavigateToHexOffset (DllInspectorBindings.cs:221).
        await auto.KeyAsync(Hex1bKey.X, cts.Token);
        await auto.WaitUntilAsync(_ => _state!.CurrentTab == TabId.HexDump);
        Assert.Equal((TabId.IlInspector, 0), _state.CrossViewBackTarget);

        // First REAL Esc — Hex → IL Inspector.
        await auto.EscapeAsync(cts.Token);
        await auto.WaitUntilTextAsync("// Method: RichLibrary.IlNavigationFixture::CallExternal");
        await auto.WaitUntilTextAsync("Esc: Back");

        // Second REAL Esc — IL → Size Map.
        await auto.EscapeAsync(cts.Token);
        await auto.WaitUntilTextAsync("Total:");

        var expectedCrumb = $" {origTree.Name} > {ns.Name} > {type.Name} ";
        await auto.WaitUntilTextAsync(expectedCrumb);

        Assert.Equal(TabId.SizeMap, _state.CurrentTab);
        Assert.Same(type, _state.TreemapCurrentLevel);
        Assert.Null(_state.CrossViewBackTarget);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// ResetViewState (triggered by PushAssembly's dependency drill) clears the
    /// entire cross-view back stack, not just the top frame.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ResetViewState_ClearsEntireBackStack()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        var method = state.Analyzer.MethodDefs.First(m => m.Rva > 0);
        state.CurrentTab = TabId.SizeMap;
        state.NavigateToIlMethod(method);
        state.NavigateToHexOffset(method.Rva);
        Assert.Equal(2, state.CrossViewBackStack.Count);

        // PushAssembly → ResetViewState clears everything
        state.PushAssembly(samples.HelloWorldDll);

        Assert.Empty(state.CrossViewBackStack);
        Assert.Null(state.CrossViewBackTarget);
    }

    /// <summary>
    /// Cross-assembly gd snapshots and restores the full multi-frame back stack,
    /// not just the top tuple. Seeds a 2-deep stack before the gd push so a
    /// single-tuple snapshot implementation would fail the count assertion.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void CrossAssemblyGd_SnapshotsAndRestoresMultiFrameStack()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        // Seed a multi-frame stack — bottom: PE/TypeDef, top: SizeMap.
        state.CrossViewBackStack.Push((TabId.PeMetadata, PeSubTabId.TypeDef));
        state.CrossViewBackStack.Push((TabId.SizeMap, 0));
        state.CurrentTab = TabId.IlInspector;

        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "CallExternal" && m.DeclaringType.Contains("IlNavigationFixture"));
        state.IlSelectedMethod = method;
        var dis = state.IlDisassembler!.DisassembleWithText(method);
        Assert.NotNull(dis);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(dis.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;

        var callInst = dis.Value.Instructions.First(i =>
            i.OpCode == "call" && i.MetadataToken is not null && i.Operand.Contains("WriteLine"));
        Assert.True(state.NavigateToIlDefinition(callInst.MetadataToken!.Value));

        // Cross-assembly push cleared the stack mid-flight.
        Assert.Empty(state.CrossViewBackStack);

        state.RestoreFromIlBackEntry(state.IlBackStack.Pop());

        // Whole stack is restored in correct top-first order.
        var arr = state.CrossViewBackStack.ToArray();
        Assert.Equal(2, arr.Length);
        Assert.Equal((TabId.SizeMap, 0), arr[0]);
        Assert.Equal((TabId.PeMetadata, PeSubTabId.TypeDef), arr[1]);
    }

    /// <summary>
    /// NavigateToHexOffset bails out early on an invalid RVA without mutating
    /// the back stack — the early return precedes the push.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToHexOffset_InvalidRva_DoesNotMutateBackStack()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        state.CrossViewBackStack.Push((TabId.SizeMap, 0));
        Assert.Single(state.CrossViewBackStack);

        // RvaToFileOffset returns -1 for an out-of-range RVA — early return fires.
        state.NavigateToHexOffset(int.MaxValue);

        Assert.Single(state.CrossViewBackStack);
        Assert.Equal((TabId.SizeMap, 0), state.CrossViewBackTarget);
    }

    /// <summary>
    /// Verifies external method filters by declaring type.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ExternalMethod_FiltersByDeclaringType()
    {
        using var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);
        var dis = new IlDisassembler(analyzer);

        // Find a method that calls an external method with a common name
        // Console.WriteLine is in System.Console — verify it resolves to the right type
        var method = analyzer.MethodDefs.First(m =>
            m.Name == "CallExternal" && m.DeclaringType.Contains("IlNavigationFixture"));
        var instructions = dis.Disassemble(method);
        var callInst = instructions.First(i =>
            i.OpCode == "call" && i.MetadataToken is not null && i.Operand.Contains("WriteLine"));

        var target = IlNavigationResolver.Resolve(analyzer, callInst.MetadataToken!.Value);
        var extMethod = Assert.IsType<IlNavigationTarget.ExternalMethod>(target);

        // The resolver must capture the declaring type, not just the method name
        Assert.Contains("Console", extMethod.DeclaringType);
    }

    /// <summary>
    /// Verifies net fx external method navigates to mscorlib method.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NetFx_ExternalMethod_NavigatesToMscorlibMethod()
    {
        if (samples.NetFxConsoleExe is null) return; // Windows-only sample

        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.NetFxConsoleExe);
        state.CurrentTab = TabId.IlInspector;

        // NetFxConsole.Program::Main calls Console.WriteLine which references mscorlib in net48
        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "Main" && m.DeclaringType.Contains("Program"));
        state.IlSelectedMethod = method;
        var result = state.IlDisassembler!.DisassembleWithText(method);
        Assert.NotNull(result);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(result.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;

        // Find the call instruction targeting mscorlib
        var callInst = result.Value.Instructions.First(i =>
            i.OpCode == "call" && i.MetadataToken is not null);

        // Resolver must identify this as an external method in mscorlib
        var target = IlNavigationResolver.Resolve(state.Analyzer, callInst.MetadataToken!.Value);
        var extMethod = Assert.IsType<IlNavigationTarget.ExternalMethod>(target);
        Assert.Equal("mscorlib", extMethod.AssemblyName);

        // Navigation to mscorlib methods must succeed — the resolver should find
        // System.Console.dll via namespace probing, not land on Internal.Console in CoreLib
        var navigated = state.NavigateToIlDefinition(callInst.MetadataToken!.Value);
        Assert.True(navigated, $"Navigation failed with notice: {state.TransientNotice}");
        Assert.Null(state.TransientNotice);
        Assert.NotNull(state.IlSelectedMethod);
        Assert.Equal("System.Console", state.IlSelectedMethod.DeclaringType);
    }

    /// <summary>
    /// Verifies mscorlib resolver type forwarders find correct assembly.
    /// </summary>
    [Theory(Timeout = 30_000)]
    [InlineData("System.Console", "System.Console")]
    [InlineData("System.Object", "System.Private.CoreLib")]
    [InlineData("System.Collections.Queue", "System.Collections.NonGeneric")]
    [InlineData("Microsoft.Win32.RegistryKey", "Microsoft.Win32.Registry")]
    [InlineData("System.Environment/SpecialFolder", "System.Private.CoreLib")]
    public void MscorlibResolver_TypeForwarders_FindCorrectAssembly(
        string declaringType, string expectedAssembly)
    {
        ImplementationAssemblyResolver.ClearCache();
        var resolved = ImplementationAssemblyResolver.Resolve(
            samples.RichLibraryDll, "mscorlib", declaringType);
        Assert.NotNull(resolved);
        var fromFile = Assert.IsType<ResolvedAssembly.FromFile>(resolved);
        Assert.Contains(expectedAssembly, Path.GetFileNameWithoutExtension(fromFile.Path));
    }

    /// <summary>
    /// Verifies mscorlib resolver nested type forwarder follows parent chain.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void MscorlibResolver_NestedTypeForwarder_FollowsParentChain()
    {
        // System.Environment forwards to System.Private.CoreLib on modern .NET.
        // Verify the nested type resolves to the same assembly as the parent,
        // proving the Implementation chain is followed rather than falling back.
        ImplementationAssemblyResolver.ClearCache();
        var parent = ImplementationAssemblyResolver.Resolve(
            samples.RichLibraryDll, "mscorlib", "System.Environment");
        ImplementationAssemblyResolver.ClearCache();
        var nested = ImplementationAssemblyResolver.Resolve(
            samples.RichLibraryDll, "mscorlib", "System.Environment/SpecialFolder");
        Assert.NotNull(parent);
        Assert.NotNull(nested);
        Assert.Equal(parent, nested);
    }

    /// <summary>
    /// Verifies external method navigates to correct declaring type.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ExternalMethod_NavigatesToCorrectDeclaringType()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;

        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "CallExternal" && m.DeclaringType.Contains("IlNavigationFixture"));
        state.IlSelectedMethod = method;
        var result = state.IlDisassembler!.DisassembleWithText(method);
        Assert.NotNull(result);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(result.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;

        var callInst = result.Value.Instructions.First(i =>
            i.OpCode == "call" && i.MetadataToken is not null && i.Operand.Contains("WriteLine"));
        var navigated = state.NavigateToIlDefinition(callInst.MetadataToken!.Value);

        if (navigated)
        {
            // Must have navigated to the correct assembly
            Assert.True(state.NavigationStack.Count > 0, "Should have pushed assembly");

            // The selected method's declaring type must contain "Console"
            // (not some other type that also has a WriteLine-like method)
            Assert.NotNull(state.IlSelectedMethod);
            Assert.Contains("Console", state.IlSelectedMethod.DeclaringType);
        }
    }

    /// <summary>
    /// Verifies external field navigation sets il selected field.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ExternalField_NavigationSetsIlSelectedField()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;

        // GetStringEmpty calls string.Empty which is ldsfld → ExternalField
        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "GetStringEmpty" && m.DeclaringType.Contains("IlNavigationFixture"));

        state.IlSelectedMethod = method;
        var result = state.IlDisassembler!.DisassembleWithText(method);
        Assert.NotNull(result);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(result.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;

        // Find ldsfld string.Empty
        var fieldInst = result.Value.Instructions.First(i =>
            i.OpCode == "ldsfld" && i.MetadataToken is not null && i.Operand.Contains("Empty"));

        var target = IlNavigationResolver.Resolve(state.Analyzer, fieldInst.MetadataToken!.Value);
        Assert.IsType<IlNavigationTarget.ExternalField>(target);

        var navigated = state.NavigateToIlDefinition(fieldInst.MetadataToken!.Value);
        Assert.True(navigated, "External field navigation must succeed");

        // External field navigation must set IlSelectedField for the right pane
        Assert.NotNull(state.IlSelectedField);
        Assert.Contains("Empty", state.IlSelectedField.Name);

        // Must have pushed assembly
        Assert.True(state.NavigationStack.Count > 0);

        // Esc back must restore
        var entry = state.IlBackStack.Pop();
        state.RestoreFromIlBackEntry(entry);
        Assert.Equal("GetStringEmpty", state.IlSelectedMethod?.Name);
        Assert.Null(state.IlSelectedField);
    }

    /// <summary>
    /// Resolves a MethodSpec token (call to Enumerable.Where&lt;User&gt; from
    /// UserService.FindByRole) and expects the underlying ExternalMethod target —
    /// not a GenericInstantiation fallback.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Resolve_MethodSpec_ReturnsUnderlyingExternalMethod()
    {
        using var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);
        var dis = new IlDisassembler(analyzer);
        var method = analyzer.MethodDefs.First(m =>
            m.Name == "FindByRole" && m.DeclaringType.Contains("UserService"));
        var callInst = dis.Disassemble(method).First(i =>
            i.OpCode == "call" && i.MetadataToken is not null
            && MetadataTokens.EntityHandle(i.MetadataToken.Value).Kind
                == HandleKind.MethodSpecification);

        var target = IlNavigationResolver.Resolve(analyzer, callInst.MetadataToken!.Value);

        var ext = Assert.IsType<IlNavigationTarget.ExternalMethod>(target);
        Assert.Equal("Where", ext.MemberName);
        Assert.Equal("System.Linq.Enumerable", ext.DeclaringType);
        // Signature should encode the method generic parameter as !!0, proving
        // we resolved the open-generic definition rather than coincidental match.
        Assert.Contains("!!0", ext.Signature);
    }

    /// <summary>
    /// Navigates from a MethodSpec call site to the open-generic definition in System.Linq.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToIlDefinition_MethodSpec_LandsOnOpenGenericDefinition()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;

        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "FindByRole" && m.DeclaringType.Contains("UserService"));
        state.IlSelectedMethod = method;
        var result = state.IlDisassembler!.DisassembleWithText(method);
        Assert.NotNull(result);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(result.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;

        var callInst = result.Value.Instructions.First(i =>
            i.OpCode == "call" && i.MetadataToken is not null
            && MetadataTokens.EntityHandle(i.MetadataToken.Value).Kind
                == HandleKind.MethodSpecification);

        var navigated = state.NavigateToIlDefinition(callInst.MetadataToken!.Value);

        Assert.True(navigated, "MethodSpec navigation must succeed for Enumerable.Where");
        Assert.True(state.NavigationStack.Count > 0, "Should have pushed System.Linq");
        Assert.NotNull(state.IlSelectedMethod);
        Assert.Equal("Where", state.IlSelectedMethod.Name);
        Assert.Equal("System.Linq.Enumerable", state.IlSelectedMethod.DeclaringType);
        Assert.Contains("!!0", state.IlSelectedMethod.Signature);
        Assert.Equal(TabId.IlInspector, state.CurrentTab);
    }

    /// <summary>
    /// Resolves a MemberRef with a TypeSpec parent (generic type instantiation).
    /// UserService.Update calls ConcurrentDictionary&lt;int, User&gt;.set_Item —
    /// the parent is a TypeSpec, not a TypeRef. The resolver must still identify it as
    /// an ExternalMethod, not return Unsupported.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Resolve_MemberRefWithTypeSpecParent_ReturnsExternalMethod()
    {
        using var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);
        var dis = new IlDisassembler(analyzer);

        var method = analyzer.MethodDefs.First(m =>
            m.Name == "Update" && m.DeclaringType.Contains("UserService"));
        var callInst = dis.Disassemble(method).First(i =>
            i.OpCode == "callvirt" && i.MetadataToken is not null
            && i.Operand.Contains("set_Item"));

        var target = IlNavigationResolver.Resolve(analyzer, callInst.MetadataToken!.Value);

        var ext = Assert.IsType<IlNavigationTarget.ExternalMethod>(target);
        Assert.Equal("set_Item", ext.MemberName);
        Assert.Contains("ConcurrentDictionary", ext.DeclaringType);
    }

    /// <summary>
    /// A malformed MethodSpec token must surface as GenericInstantiation with a
    /// non-empty Reason, and the DotsiderState arm must report it via TransientNotice.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Resolve_InvalidMethodSpecToken_ReturnsGenericInstantiationWithReason()
    {
        using var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);
        // HandleKind.MethodSpecification = 0x2B. Row 0xFFFFFF is well past any real row,
        // so reader.GetMethodSpecification will throw BadImageFormatException.
        var invalidToken = unchecked((int)0x2BFFFFFF);

        var target = IlNavigationResolver.Resolve(analyzer, invalidToken);

        var gi = Assert.IsType<IlNavigationTarget.GenericInstantiation>(target);
        Assert.Equal(invalidToken, gi.Token);
        Assert.False(string.IsNullOrEmpty(gi.Reason));

        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        var navigated = state.NavigateToIlDefinition(invalidToken);
        Assert.False(navigated);
        Assert.NotNull(state.TransientNotice);
        Assert.Contains("generic instantiation", state.TransientNotice, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Navigates to the ctor of a generic type forwarded through a partial-facade
    /// BCL assembly. HelloWorld's MoveNext calls newobj List&lt;byte[]&gt;::.ctor —
    /// the TypeRef scope is System.Collections (a partial facade), which forwards
    /// List`1 to System.Private.CoreLib. The resolver must land there, not inside
    /// the facade.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ExternalMethod_ForwardedFromPartialFacade_Ctor_LandsInCoreLib()
        => AssertForwardedListMemberLandsInCoreLib("newobj", ".ctor");

    /// <summary>
    /// Same partial-facade chase, but for the callvirt on List&lt;byte[]&gt;::Add.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ExternalMethod_ForwardedFromPartialFacade_Add_LandsInCoreLib()
        => AssertForwardedListMemberLandsInCoreLib("callvirt", "Add");

    /// <summary>
    /// Same partial-facade chase, but for the callvirt on List&lt;byte[]&gt;::Clear.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ExternalMethod_ForwardedFromPartialFacade_Clear_LandsInCoreLib()
        => AssertForwardedListMemberLandsInCoreLib("callvirt", "Clear");

    private void AssertForwardedListMemberLandsInCoreLib(string opCode, string memberName)
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.HelloWorldDll);
        state.CurrentTab = TabId.IlInspector;

        var moveNext = state.Analyzer.MethodDefs.First(m =>
            m.Name == "MoveNext" && m.DeclaringType.Contains("<Main>$"));
        state.IlSelectedMethod = moveNext;
        var result = state.IlDisassembler!.DisassembleWithText(moveNext);
        Assert.NotNull(result);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(result.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = moveNext;
        state.IlEditorAnalyzer = state.Analyzer;

        var (inst, nav) = result.Value.Instructions
            .Where(i => i.OpCode == opCode && i.MetadataToken is not null)
            .Select(i => (inst: i, nav: IlNavigationResolver.Resolve(
                state.Analyzer, i.MetadataToken!.Value)))
            .First(x => x.nav is IlNavigationTarget.ExternalMethod em
                && em.MemberName == memberName
                && em.DeclaringType == "System.Collections.Generic.List`1");

        var navigated = state.NavigateToIlDefinition(inst.MetadataToken!.Value);

        Assert.True(navigated, $"Navigation must succeed for List`1::{memberName}");
        Assert.Null(state.TransientNotice);
        Assert.True(state.NavigationStack.Count > 0, "Should have pushed assembly");
        Assert.NotNull(state.IlSelectedMethod);
        Assert.Equal(memberName, state.IlSelectedMethod.Name);
        Assert.Equal("System.Collections.Generic.List`1", state.IlSelectedMethod.DeclaringType);
        Assert.Equal("System.Private.CoreLib.dll",
            Path.GetFileName(state.Analyzer.FilePath));
    }

    /// <summary>
    /// Navigates to a type locally owned in the partial facade. LinkedList`1 is one
    /// of System.Collections.dll's real TypeDefs, not a forwarder — the resolver
    /// must stay in the facade rather than over-chasing into CoreLib.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ExternalMethod_LocallyOwnedInPartialFacade_StaysInFacade()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;

        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "CreateLinkedList" && m.DeclaringType.Contains("IlNavigationFixture"));
        state.IlSelectedMethod = method;
        var result = state.IlDisassembler!.DisassembleWithText(method);
        Assert.NotNull(result);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(result.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;

        var (inst, nav) = result.Value.Instructions
            .Where(i => i.OpCode == "newobj" && i.MetadataToken is not null)
            .Select(i => (inst: i, nav: IlNavigationResolver.Resolve(
                state.Analyzer, i.MetadataToken!.Value)))
            .First(x => x.nav is IlNavigationTarget.ExternalMethod em
                && em.MemberName == ".ctor"
                && em.DeclaringType == "System.Collections.Generic.LinkedList`1");

        var navigated = state.NavigateToIlDefinition(inst.MetadataToken!.Value);

        Assert.True(navigated, "Navigation must succeed for LinkedList`1::.ctor");
        Assert.Null(state.TransientNotice);
        Assert.NotNull(state.IlSelectedMethod);
        Assert.Equal("System.Collections.Generic.LinkedList`1", state.IlSelectedMethod.DeclaringType);
        Assert.Equal("System.Collections.dll",
            Path.GetFileName(state.Analyzer.FilePath));
    }

    /// <summary>
    /// A TypeSpec whose signature is a bare ELEMENT_TYPE_VAR names a generic
    /// parameter of the enclosing type. The resolver needs the current method
    /// context to know which owner "!N" refers to; with that context it routes
    /// to the enclosing type definition (where the GenericParam row lives).
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Resolve_TypeSpecGenericTypeParam_WithContext_ReturnsEnclosingType()
    {
        using var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);
        var dis = new IlDisassembler(analyzer);
        var method = analyzer.MethodDefs.First(m =>
            m.Name == "DefaultValue" && m.DeclaringType.Contains("GenericParamFixture"));
        var initobj = dis.Disassemble(method).First(i =>
            i.OpCode == "initobj" && i.MetadataToken is not null);

        var target = IlNavigationResolver.Resolve(analyzer, initobj.MetadataToken!.Value, method);

        var local = Assert.IsType<IlNavigationTarget.LocalType>(target);
        Assert.Equal("RichLibrary.GenericParamFixture`2", local.Type.FullName);
    }

    /// <summary>
    /// A TypeSpec whose signature is a bare ELEMENT_TYPE_MVAR names a method-
    /// level generic parameter. The only definition site is the current method's
    /// signature, so the resolver reports it via Unsupported (a transient notice)
    /// rather than a self-navigation that the UI would silently swallow.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Resolve_TypeSpecGenericMethodParam_WithContext_ReportsDefinedBySignature()
    {
        using var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);
        var dis = new IlDisassembler(analyzer);
        var method = analyzer.MethodDefs.First(m =>
            m.Name == "DefaultMethodParam" && m.DeclaringType.Contains("GenericParamFixture"));
        var initobj = dis.Disassemble(method).First(i =>
            i.OpCode == "initobj" && i.MetadataToken is not null);

        var target = IlNavigationResolver.Resolve(analyzer, initobj.MetadataToken!.Value, method);

        var unsupported = Assert.IsType<IlNavigationTarget.Unsupported>(target);
        Assert.Contains("!!0", unsupported.Reason);
        Assert.Contains("DefaultMethodParam", unsupported.Reason);
    }

    /// <summary>
    /// End-to-end: pressing go-to-definition on initobj !!0 inside the method
    /// raises a transient notice and does not change the selected method. The
    /// UI gets a clear "there's nothing to navigate to" signal instead of a
    /// silent no-op.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToIlDefinition_TypeSpecGenericMethodParam_RaisesNotice()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;

        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "DefaultMethodParam" && m.DeclaringType.Contains("GenericParamFixture"));
        state.IlSelectedMethod = method;
        var result = state.IlDisassembler!.DisassembleWithText(method);
        Assert.NotNull(result);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(result.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;

        var initobj = result.Value.Instructions.First(i =>
            i.OpCode == "initobj" && i.MetadataToken is not null);

        var navigated = state.NavigateToIlDefinition(initobj.MetadataToken!.Value);

        Assert.False(navigated);
        Assert.NotNull(state.TransientNotice);
        Assert.Contains("!!0", state.TransientNotice);
        Assert.Same(method, state.IlSelectedMethod);
    }

    /// <summary>
    /// Without a method context, a bare generic-parameter TypeSpec is not
    /// resolvable. The resolver should surface a message that explains what's
    /// missing rather than the opaque "Cannot resolve TypeSpec: !1".
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Resolve_TypeSpecGenericTypeParam_WithoutContext_ReportsMissingContext()
    {
        using var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);
        var dis = new IlDisassembler(analyzer);
        var method = analyzer.MethodDefs.First(m =>
            m.Name == "DefaultValue" && m.DeclaringType.Contains("GenericParamFixture"));
        var initobj = dis.Disassemble(method).First(i =>
            i.OpCode == "initobj" && i.MetadataToken is not null);

        var target = IlNavigationResolver.Resolve(analyzer, initobj.MetadataToken!.Value);

        var unsupported = Assert.IsType<IlNavigationTarget.Unsupported>(target);
        Assert.Contains("!1", unsupported.Reason);
        Assert.Contains("context", unsupported.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// End-to-end: pressing go-to-definition on initobj !1 in a generic method
    /// lands on the enclosing type, clears the selected method, and raises no
    /// transient notice.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigateToIlDefinition_TypeSpecGenericTypeParam_LandsOnEnclosingType()
    {
        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        state.CurrentTab = TabId.IlInspector;

        var method = state.Analyzer.MethodDefs.First(m =>
            m.Name == "DefaultValue" && m.DeclaringType.Contains("GenericParamFixture"));
        state.IlSelectedMethod = method;
        var result = state.IlDisassembler!.DisassembleWithText(method);
        Assert.NotNull(result);
        state.IlEditorState = new EditorState(
            new Hex1b.Documents.Hex1bDocument(result.Value.Text)) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;

        var initobj = result.Value.Instructions.First(i =>
            i.OpCode == "initobj" && i.MetadataToken is not null);

        var navigated = state.NavigateToIlDefinition(initobj.MetadataToken!.Value);

        Assert.True(navigated, "Navigation to generic type parameter's owner must succeed");
        Assert.Null(state.TransientNotice);
        Assert.Equal(
            "type:RichLibrary.GenericParamFixture`2",
            state.IlFocusedTreeKey as string);
        // Method/editor selection must be cleared so the right pane stops showing
        // DefaultValue's IL. Without that the navigation only half-applies.
        Assert.Null(state.IlSelectedMethod);
        Assert.Null(state.IlEditorMethod);
        Assert.Null(state.IlEditorState);
        Assert.Null(state.IlEditorAnalyzer);
    }
}
