using System.Collections.Concurrent;
using System.Text.Json;
using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end protocol tests exercising enhanced assembly-info, get-current-view,
/// list-fields, bundle methods, resolve-assembly, IL navigation, navigate-back,
/// and push-assembly over a real headless TUI and diagnostics socket.
/// </summary>
[Collection("SampleAssemblies")]
public class SessionBundleTests(SampleAssemblyFixture samples) : IAsyncDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _app;
    private DotsiderState? _state;
    private DotsiderDiagnosticsListener? _listener;
    private CancellationTokenSource? _appCts;

    /// <summary>
    /// Starts a headless dotsider TUI with the diagnostics socket listener,
    /// reproducing the full production stack.
    /// </summary>
    private async Task<(Hex1bApp app, string socketPath)> StartTuiWithDiagnosticsAsync(
        string dllPath, CancellationToken ct)
    {
        var pendingMutations = new ConcurrentQueue<Action<DotsiderState>>();

        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(120, 30)
            .Build();

        _app = new Hex1bApp(
            ctx =>
            {
                _state ??= new DotsiderState(_app!, dllPath, pendingMutations);

                var dotsiderApp = new DotsiderApp(_state);
                return Task.FromResult<Hex1bWidget>(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
            });

        _listener = new DotsiderDiagnosticsListener(() => _state);
        _listener.StartListening();

        // Start the TUI and wait for first render
        _appCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = _app.RunAsync(_appCts.Token);
        await Task.Delay(100, ct);

        await TestHelpers.WaitUntilAsync(
            () => _state is not null,
            TimeSpan.FromSeconds(10));

        return (_app, _listener.SocketPath!);
    }

    // --- Enhanced assembly-info ---

    /// <summary>
    /// Verifies that assembly-info for a file-backed library returns the expected
    /// displayName, isBundleBacked, canSaveInPlace, and preferredRuntimePack properties.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task AssemblyInfo_FileBacked_IncludesProperties()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.RichLibraryDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "assembly-info" }, ct);
        Assert.True(response.Success);

        var data = (response.Data as JsonElement?)!.Value;
        var displayName = data.GetProperty("displayName").GetString();
        var fileName = data.GetProperty("fileName").GetString();
        Assert.Equal(fileName, displayName);

        Assert.False(data.GetProperty("isBundleBacked").GetBoolean());
        Assert.True(data.GetProperty("canSaveInPlace").GetBoolean());
        Assert.Equal("Microsoft.NETCore.App", data.GetProperty("preferredRuntimePack").GetString());
    }

    /// <summary>
    /// Verifies that assembly-info for an ASP.NET Core assembly returns
    /// "Microsoft.AspNetCore.App" as the preferredRuntimePack.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task AssemblyInfo_AspNetCore_PreferredPack()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.MinimalApiDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "assembly-info" }, ct);
        Assert.True(response.Success);

        var data = (response.Data as JsonElement?)!.Value;
        Assert.Equal("Microsoft.AspNetCore.App", data.GetProperty("preferredRuntimePack").GetString());
    }

    // --- Enhanced get-current-view ---

    /// <summary>
    /// Verifies that get-current-view for a managed DLL with an entry point reports
    /// hasEntryPoint true, hexIsDirty false, isNativeAot false, isNetFramework false.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task GetCurrentView_ExeHasEntryPoint()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.HelloWorldDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.True(response.Success);

        var data = (response.Data as JsonElement?)!.Value;
        Assert.Equal("General", data.GetProperty("tabLabel").GetString());
        Assert.True(data.GetProperty("hasEntryPoint").GetBoolean());
        Assert.False(data.GetProperty("hexIsDirty").GetBoolean());
        Assert.False(data.GetProperty("isNativeAot").GetBoolean());
        Assert.False(data.GetProperty("isNetFramework").GetBoolean());
    }

    /// <summary>
    /// Verifies that get-current-view for a class library reports hasEntryPoint false.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task GetCurrentView_LibraryHasNoEntryPoint()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.RichLibraryDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.True(response.Success);

        var data = (response.Data as JsonElement?)!.Value;
        Assert.False(data.GetProperty("hasEntryPoint").GetBoolean());
    }

    /// <summary>
    /// Verifies that get-current-view reports hexIsDirty true after a byte edit
    /// is applied to the hex document.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task GetCurrentView_HexIsDirty_AfterEdit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.HelloWorldDll, ct);

        // Queue a mutation that modifies the hex document to make it dirty
        _state!.PendingMutations.Enqueue(s =>
        {
            s.HexEditorState.IsReadOnly = false;
            s.HexEditorState.Document.ApplyBytes(
                new ByteReplaceOperation(4, 1, [0xFF]));
            s.HexEditorState.IsReadOnly = true;
        });
        _app!.Invalidate();
        await TestHelpers.WaitUntilAsync(
            () => _state!.HexIsDirty,
            TimeSpan.FromSeconds(5));

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.True(response.Success);

        var data = (response.Data as JsonElement?)!.Value;
        Assert.True(data.GetProperty("hexIsDirty").GetBoolean());
    }

    // --- list-fields ---

    /// <summary>
    /// Verifies that list-fields returns a non-empty array of field definitions
    /// for a library with fields.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ListFields_ReturnsFieldDefinitions()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.RichLibraryDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "list-fields" }, ct);
        Assert.True(response.Success);

        var data = (response.Data as JsonElement?)!.Value;
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.True(data.GetArrayLength() > 0, "Expected at least one field definition");
    }

    /// <summary>
    /// Verifies that list-fields with a Query parameter returns a filtered subset.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ListFields_WithQuery_Filters()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.RichLibraryDll, ct);

        // Get unfiltered count
        var allResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "list-fields" }, ct);
        Assert.True(allResponse.Success);
        var allCount = ((allResponse.Data as JsonElement?)!.Value).GetArrayLength();

        // Filter by a specific query that should match fewer results
        var filteredResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "list-fields", Query = "_counter" }, ct);
        Assert.True(filteredResponse.Success);

        var filteredData = (filteredResponse.Data as JsonElement?)!.Value;
        Assert.Equal(JsonValueKind.Array, filteredData.ValueKind);
        var filteredCount = filteredData.GetArrayLength();
        Assert.True(filteredCount > 0, "Expected at least one field matching '_counter'");
        Assert.True(filteredCount < allCount, "Filtered results should be fewer than all fields");
    }

    // --- Bundle methods ---

    /// <summary>
    /// Verifies that is-bundle returns true for a self-contained single-file executable.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task IsBundle_SelfContainedExe_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.HelloWorldDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "is-bundle", AssemblyPath = samples.SelfContainedConsoleExe }, ct);
        Assert.True(response.Success);

        var data = (response.Data as JsonElement?)!.Value;
        Assert.True(data.GetProperty("isBundle").GetBoolean());
    }

    /// <summary>
    /// Verifies that is-bundle returns false for a regular class library DLL.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task IsBundle_RegularDll_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.HelloWorldDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "is-bundle", AssemblyPath = samples.RichLibraryDll }, ct);
        Assert.True(response.Success);

        var data = (response.Data as JsonElement?)!.Value;
        Assert.False(data.GetProperty("isBundle").GetBoolean());
    }

    /// <summary>
    /// Verifies that get-bundle-manifest returns a manifest with fileCount greater than zero
    /// for a self-contained single-file executable.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task GetBundleManifest_ReturnsEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.HelloWorldDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-bundle-manifest", AssemblyPath = samples.SelfContainedConsoleExe }, ct);
        Assert.True(response.Success);

        var data = (response.Data as JsonElement?)!.Value;
        Assert.True(data.GetProperty("fileCount").GetInt32() > 0);
    }

    // --- resolve-assembly ---

    /// <summary>
    /// Verifies that resolve-assembly for a shared framework assembly like System.Runtime
    /// returns a file-backed result.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ResolveAssembly_SharedFramework_ReturnsFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.RichLibraryDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "resolve-assembly", AssemblyName = "System.Runtime" }, ct);
        Assert.True(response.Success);

        var data = (response.Data as JsonElement?)!.Value;
        Assert.Equal("file", data.GetProperty("kind").GetString());
    }

    /// <summary>
    /// Verifies that resolve-assembly for a nonexistent assembly name returns a null resolved value.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ResolveAssembly_Nonexistent_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.RichLibraryDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "resolve-assembly", AssemblyName = "This.Assembly.Does.Not.Exist.At.All" }, ct);
        Assert.True(response.Success);

        // Unresolved assembly returns null data
        Assert.True(response.Data is null
            || (response.Data is JsonElement el && el.ValueKind == JsonValueKind.Null));
    }

    // --- IL navigation ---

    /// <summary>
    /// Verifies that navigate-to-il-definition for a local method token returns status "queued".
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task NavigateToIlDefinition_LocalMethod_Queued()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.RichLibraryDll, ct);

        // Navigate to the IL tab first
        await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate", TabId = TabId.IlInspector + 1 }, ct);
        await TestHelpers.WaitUntilAsync(
            () => _state?.CurrentTab == TabId.IlInspector,
            TimeSpan.FromSeconds(5));

        // Find CallLocalMethod's token by disassembling it
        var disasmResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest
            {
                Method = "disassemble",
                TypeName = "IlNavigationFixture",
                MethodName = "CallLocalMethod"
            }, ct);
        Assert.True(disasmResponse.Success);

        var disasmData = (disasmResponse.Data as JsonElement?)!.Value;
        var instructions = disasmData.GetProperty("instructions");

        // Find the call instruction that references LocalTarget
        int? callToken = null;
        foreach (var instr in instructions.EnumerateArray())
        {
            var opCode = instr.GetProperty("opCode").GetString();
            if (opCode is "call" or "callvirt" &&
                instr.TryGetProperty("metadataToken", out var tokenEl) &&
                tokenEl.ValueKind == JsonValueKind.Number)
            {
                var operand = instr.GetProperty("operand").GetString();
                if (operand?.Contains("LocalTarget") == true)
                {
                    callToken = tokenEl.GetInt32();
                    break;
                }
            }
        }

        Assert.NotNull(callToken);

        // Send navigate-to-il-definition with the found token
        var navResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate-to-il-definition", Token = callToken }, ct);
        Assert.True(navResponse.Success);

        var navData = (navResponse.Data as JsonElement?)!.Value;
        Assert.Equal("queued", navData.GetProperty("status").GetString());
    }

    /// <summary>
    /// Verifies that navigate-to-il-definition for an external method token (Console.WriteLine)
    /// results in cross-assembly navigation, increasing the navigation depth.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task NavigateToIlDefinition_ExternalMethod_NavigatesCrossAssembly()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.RichLibraryDll, ct);

        // Navigate to the IL tab first
        await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate", TabId = TabId.IlInspector + 1 }, ct);
        await TestHelpers.WaitUntilAsync(
            () => _state?.CurrentTab == TabId.IlInspector,
            TimeSpan.FromSeconds(5));

        // Disassemble CallExternal to find the Console.WriteLine call token
        var disasmResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest
            {
                Method = "disassemble",
                TypeName = "IlNavigationFixture",
                MethodName = "CallExternal"
            }, ct);
        Assert.True(disasmResponse.Success);

        var disasmData = (disasmResponse.Data as JsonElement?)!.Value;
        var instructions = disasmData.GetProperty("instructions");

        // Find the call instruction to Console.WriteLine
        int? callToken = null;
        foreach (var instr in instructions.EnumerateArray())
        {
            var opCode = instr.GetProperty("opCode").GetString();
            if (opCode is "call" or "callvirt" &&
                instr.TryGetProperty("metadataToken", out var tokenEl) &&
                tokenEl.ValueKind == JsonValueKind.Number)
            {
                var operand = instr.GetProperty("operand").GetString();
                if (operand?.Contains("WriteLine") == true)
                {
                    callToken = tokenEl.GetInt32();
                    break;
                }
            }
        }

        Assert.NotNull(callToken);

        // Get depth before navigation
        var viewBefore = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.True(viewBefore.Success);
        var depthBefore = (viewBefore.Data as JsonElement?)!.Value.GetProperty("navigationDepth").GetInt32();

        // Send navigate-to-il-definition with the external token
        var navResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate-to-il-definition", Token = callToken }, ct);
        Assert.True(navResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.NavigationStack.Count > depthBefore,
            TimeSpan.FromSeconds(5));
        Assert.True(_state!.NavigationStack.Count > depthBefore);
    }

    // --- navigate-back ---

    /// <summary>
    /// Verifies that navigate-back after push-assembly restores the navigation depth.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task NavigateBack_AfterPush_RestoresDepth()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.RichLibraryDll, ct);

        // Push System.Runtime to increase depth
        var pushResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "push-assembly", AssemblyName = "System.Runtime" }, ct);
        Assert.True(pushResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.NavigationStack.Count == 1,
            TimeSpan.FromSeconds(5));

        // Navigate back
        var backResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate-back" }, ct);
        Assert.True(backResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.NavigationStack.Count == 0,
            TimeSpan.FromSeconds(5));
        Assert.Empty(_state!.NavigationStack);
    }

    // --- push-assembly ---

    /// <summary>
    /// Verifies that push-assembly by name resolves and pushes System.Runtime,
    /// increasing the navigation depth.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PushAssembly_ByName_ResolvesAndPushes()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.RichLibraryDll, ct);

        // Get initial depth
        var viewBefore = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.True(viewBefore.Success);
        var depthBefore = (viewBefore.Data as JsonElement?)!.Value.GetProperty("navigationDepth").GetInt32();

        // Push by assembly name
        var pushResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "push-assembly", AssemblyName = "System.Runtime" }, ct);
        Assert.True(pushResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.NavigationStack.Count > depthBefore,
            TimeSpan.FromSeconds(5));
        Assert.True(_state!.NavigationStack.Count > depthBefore);
    }

    /// <summary>
    /// Verifies that push-assembly by path opens the specified assembly directly,
    /// increasing the navigation depth.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PushAssembly_ByPath_OpensDirectly()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.RichLibraryDll, ct);

        // Get initial depth
        var viewBefore = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.True(viewBefore.Success);
        var depthBefore = (viewBefore.Data as JsonElement?)!.Value.GetProperty("navigationDepth").GetInt32();

        // Push by path
        var pushResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "push-assembly", AssemblyPath = samples.HelloWorldDll }, ct);
        Assert.True(pushResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.NavigationStack.Count > depthBefore,
            TimeSpan.FromSeconds(5));
        Assert.True(_state!.NavigationStack.Count > depthBefore);
    }

    /// <summary>
    /// Verifies that push-assembly by path correctly handles an apphost exe
    /// by loading the companion managed assembly instead of the native host.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PushAssembly_ByApphostPath_OpensCompanion()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.RichLibraryDll, ct);

        var pushResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "push-assembly", AssemblyPath = samples.HelloWorldExe }, ct);
        Assert.True(pushResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.NavigationStack.Count > 0,
            TimeSpan.FromSeconds(5));

        // Verify the pushed assembly has metadata (companion DLL, not the native host)
        var info = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "assembly-info" }, ct);
        Assert.True(info.Success);
        var data = (info.Data as JsonElement?)!.Value;
        Assert.True(data.GetProperty("hasMetadata").GetBoolean());
    }

    /// <summary>
    /// Verifies that push-assembly by path correctly handles a single-file bundle
    /// by extracting and loading the entry assembly with metadata.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PushAssembly_ByBundlePath_OpensEntryAssembly()
    {
        Assert.NotNull(samples.SelfContainedConsoleExe);
        var ct = TestContext.Current.CancellationToken;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.RichLibraryDll, ct);

        var pushResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "push-assembly", AssemblyPath = samples.SelfContainedConsoleExe }, ct);
        Assert.True(pushResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.Analyzer.AssemblyName == "SelfContainedConsole",
            TimeSpan.FromSeconds(5));

        // Verify the pushed assembly has metadata (entry assembly, not the bundle host)
        var info = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "assembly-info" }, ct);
        Assert.True(info.Success);
        var data = (info.Data as JsonElement?)!.Value;
        Assert.True(data.GetProperty("hasMetadata").GetBoolean());
        Assert.Equal("SelfContainedConsole", data.GetProperty("assemblyName").GetString());
    }

    /// <summary>
    /// Verifies that navigate-back restores the apphost dialog when popping
    /// back to a native apphost with a known companion DLL.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task NavigateBack_ToApphost_RestoresApphostDialog()
    {
        var ct = TestContext.Current.CancellationToken;
        // Open the apphost exe — DotsiderState sets ApphostDialogOpen = true
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.HelloWorldExe, ct);

        // Push the companion DLL to navigate away from the dialog
        var pushResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "push-assembly", AssemblyPath = samples.HelloWorldDll }, ct);
        Assert.True(pushResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.NavigationStack.Count > 0,
            TimeSpan.FromSeconds(5));

        // Navigate back — should return to the apphost and reopen the dialog
        var backResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate-back" }, ct);
        Assert.True(backResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.NavigationStack.Count == 0,
            TimeSpan.FromSeconds(5));

        // The analyzer should be the native apphost (no metadata)
        Assert.False(_state!.Analyzer.HasMetadata);

        // The apphost dialog must be reopened so the user can navigate to the companion
        Assert.True(_state.ApphostDialogOpen);
        Assert.NotNull(_state.ApphostCompanionDllPath);
    }

    /// <summary>
    /// Disposes the diagnostics listener, state, and terminal.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        _appCts?.Cancel();
        if (_listener is not null)
            await _listener.DisposeAsync();
        _state?.Dispose();
        if (_terminal is not null)
            await _terminal.DisposeAsync();
        _appCts?.Dispose();
    }
}
