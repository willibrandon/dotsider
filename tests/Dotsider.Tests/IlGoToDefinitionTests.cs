using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
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

        // Navigate tree: 6 downs to IlNavigationFixture, expand, down to CallLocalMethod, select
        for (var i = 0; i < 6; i++) await auto.DownAsync(ct);
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

        // SCROLL: press Down arrow to scroll — this must work (not be frozen)
        await auto.DownAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        // Verify cursor moved (scroll not frozen)
        var cursorAfterScroll = _state!.IlEditorState?.Cursor.Position.Value ?? -1;

        // Press Up arrow back
        await auto.UpAsync(cts.Token);
        await Task.Delay(100, cts.Token);

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

        var target = result.Value.Instructions
            .Where(i => i.OpCode == opCode && i.MetadataToken is not null)
            .Select(i => (inst: i, nav: IlNavigationResolver.Resolve(
                state.Analyzer, i.MetadataToken!.Value)))
            .First(x => x.nav is IlNavigationTarget.ExternalMethod em
                && em.MemberName == memberName
                && em.DeclaringType == "System.Collections.Generic.List`1");

        var navigated = state.NavigateToIlDefinition(target.inst.MetadataToken!.Value);

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

        var target = result.Value.Instructions
            .Where(i => i.OpCode == "newobj" && i.MetadataToken is not null)
            .Select(i => (inst: i, nav: IlNavigationResolver.Resolve(
                state.Analyzer, i.MetadataToken!.Value)))
            .First(x => x.nav is IlNavigationTarget.ExternalMethod em
                && em.MemberName == ".ctor"
                && em.DeclaringType == "System.Collections.Generic.LinkedList`1");

        var navigated = state.NavigateToIlDefinition(target.inst.MetadataToken!.Value);

        Assert.True(navigated, "Navigation must succeed for LinkedList`1::.ctor");
        Assert.Null(state.TransientNotice);
        Assert.NotNull(state.IlSelectedMethod);
        Assert.Equal("System.Collections.Generic.LinkedList`1", state.IlSelectedMethod.DeclaringType);
        Assert.Equal("System.Collections.dll",
            Path.GetFileName(state.Analyzer.FilePath));
    }
}
