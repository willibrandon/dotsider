using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Dotsider.Tests.Shared;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Widgets;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end protocol tests exercising enhanced assembly-info, get-current-view,
/// list-fields, bundle methods, resolve-assembly, IL navigation, navigate-back,
/// and push-assembly over a real headless TUI and diagnostics socket.
/// </summary>
[TestClass]
public class SessionBundleTests : IAsyncDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _app;
    private DotsiderState? _state;
    private DotsiderDiagnosticsListener? _listener;
    private CancellationTokenSource? _appCts;
    private Task? _appTask;

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
        _listener.StartListening(overridePid: TestSocketIds.NextPid());

        // Start the TUI and wait for first render
        _appCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _appTask = _app.RunAsync(_appCts.Token);
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
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task AssemblyInfo_FileBacked_IncludesProperties()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.RichLibraryDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "assembly-info" }, ct);
        Assert.IsTrue(response.Success);

        var data = response.Data!.Value;
        var displayName = data.GetProperty("displayName").GetString();
        var fileName = data.GetProperty("fileName").GetString();
        Assert.AreEqual(fileName, displayName);

        Assert.IsFalse(data.GetProperty("isBundleBacked").GetBoolean());
        Assert.IsTrue(data.GetProperty("canSaveInPlace").GetBoolean());
        Assert.AreEqual("Microsoft.NETCore.App", data.GetProperty("preferredRuntimePack").GetString());
    }

    /// <summary>
    /// Verifies that assembly-info for an ASP.NET Core assembly returns
    /// "Microsoft.AspNetCore.App" as the preferredRuntimePack.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task AssemblyInfo_AspNetCore_PreferredPack()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.MinimalApiDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "assembly-info" }, ct);
        Assert.IsTrue(response.Success);

        var data = response.Data!.Value;
        Assert.AreEqual("Microsoft.AspNetCore.App", data.GetProperty("preferredRuntimePack").GetString());
    }

    // --- Enhanced get-current-view ---

    /// <summary>
    /// Verifies that get-current-view for a managed DLL with an entry point reports
    /// hasEntryPoint true, hexIsDirty false, isNativeAot false, isNetFramework false.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetCurrentView_ExeHasEntryPoint()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.HelloWorldDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.IsTrue(response.Success);

        var data = response.Data!.Value;
        Assert.AreEqual("General", data.GetProperty("tabLabel").GetString());
        Assert.IsTrue(data.GetProperty("hasEntryPoint").GetBoolean());
        Assert.IsFalse(data.GetProperty("hexIsDirty").GetBoolean());
        Assert.IsFalse(data.GetProperty("isNativeAot").GetBoolean());
        Assert.IsFalse(data.GetProperty("isNetFramework").GetBoolean());
    }

    /// <summary>
    /// Verifies that get-current-view for a class library reports hasEntryPoint false.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetCurrentView_LibraryHasNoEntryPoint()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.RichLibraryDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.IsTrue(response.Success);

        var data = response.Data!.Value;
        Assert.IsFalse(data.GetProperty("hasEntryPoint").GetBoolean());
    }

    /// <summary>
    /// Verifies that get-current-view reports hexIsDirty true after a byte edit
    /// is applied to the hex document.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetCurrentView_HexIsDirty_AfterEdit()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.HelloWorldDll, ct);

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
        Assert.IsTrue(response.Success);

        var data = response.Data!.Value;
        Assert.IsTrue(data.GetProperty("hexIsDirty").GetBoolean());
    }

    // --- list-fields ---

    /// <summary>
    /// Verifies that list-fields returns a non-empty array of field definitions
    /// for a library with fields.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ListFields_ReturnsFieldDefinitions()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.RichLibraryDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "list-fields" }, ct);
        Assert.IsTrue(response.Success);

        var data = response.Data!.Value;
        Assert.AreEqual(JsonValueKind.Array, data.ValueKind);
        Assert.IsGreaterThan(0, data.GetArrayLength(), "Expected at least one field definition");
    }

    /// <summary>
    /// Verifies that list-fields with a Query parameter returns a filtered subset.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ListFields_WithQuery_Filters()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.RichLibraryDll, ct);

        // Get unfiltered count
        var allResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "list-fields" }, ct);
        Assert.IsTrue(allResponse.Success);
        var allCount = (allResponse.Data!.Value).GetArrayLength();

        // Filter by a specific query that should match fewer results
        var filteredResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "list-fields", Query = "_counter" }, ct);
        Assert.IsTrue(filteredResponse.Success);

        var filteredData = filteredResponse.Data!.Value;
        Assert.AreEqual(JsonValueKind.Array, filteredData.ValueKind);
        var filteredCount = filteredData.GetArrayLength();
        Assert.IsGreaterThan(0, filteredCount, "Expected at least one field matching '_counter'");
        Assert.IsLessThan(allCount, filteredCount, "Filtered results should be fewer than all fields");
    }

    // --- Bundle methods ---

    /// <summary>
    /// Verifies that is-bundle returns true for a self-contained single-file executable.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task IsBundle_SelfContainedExe_ReturnsTrue()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.HelloWorldDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "is-bundle", AssemblyPath = Samples.SelfContainedConsoleExe }, ct);
        Assert.IsTrue(response.Success);

        var data = response.Data!.Value;
        Assert.IsTrue(data.GetProperty("isBundle").GetBoolean());
    }

    /// <summary>
    /// Verifies that is-bundle returns false for a regular class library DLL.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task IsBundle_RegularDll_ReturnsFalse()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.HelloWorldDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "is-bundle", AssemblyPath = Samples.RichLibraryDll }, ct);
        Assert.IsTrue(response.Success);

        var data = response.Data!.Value;
        Assert.IsFalse(data.GetProperty("isBundle").GetBoolean());
    }

    /// <summary>
    /// Verifies that get-bundle-manifest returns a manifest with fileCount greater than zero
    /// for a self-contained single-file executable.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetBundleManifest_ReturnsEntries()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.HelloWorldDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-bundle-manifest", AssemblyPath = Samples.SelfContainedConsoleExe }, ct);
        Assert.IsTrue(response.Success);

        var data = response.Data!.Value;
        Assert.IsGreaterThan(0, data.GetProperty("fileCount").GetInt32());
    }

    /// <summary>
    /// Verifies that diagnostics reports a malformed recognized bundle with a stable generic failure.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetBundleManifest_MalformedBundle_ReturnsSafeFailure()
    {
        var path = SyntheticSingleFileBundle.Create(fileCount: 0);
        try
        {
            var ct = CancellationToken.None;
            var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.HelloWorldDll, ct);

            var response = await DotsiderClient.SendAsync(socketPath,
                new DotsiderRequest { Method = "get-bundle-manifest", AssemblyPath = path }, ct);

            Assert.IsFalse(response.Success);
            Assert.AreEqual("Invalid single-file bundle manifest", response.Error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // --- resolve-assembly ---

    /// <summary>
    /// Verifies that resolve-assembly for a shared framework assembly like System.Runtime
    /// returns a file-backed result.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ResolveAssembly_SharedFramework_ReturnsFile()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.RichLibraryDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "resolve-assembly", AssemblyName = "System.Runtime" }, ct);
        Assert.IsTrue(response.Success);

        var data = response.Data!.Value;
        Assert.AreEqual("file", data.GetProperty("kind").GetString());
    }

    /// <summary>
    /// Verifies that resolve-assembly for a nonexistent assembly name returns a null resolved value.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ResolveAssembly_Nonexistent_ReturnsNull()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.RichLibraryDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "resolve-assembly", AssemblyName = "This.Assembly.Does.Not.Exist.At.All" }, ct);
        Assert.IsTrue(response.Success);

        // Unresolved assembly returns null data
        Assert.IsTrue(response.Data is null
            || (response.Data is JsonElement el && el.ValueKind == JsonValueKind.Null));
    }

    // --- IL navigation ---

    /// <summary>
    /// Verifies that navigate-to-il-definition for a local method token returns status "queued".
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task NavigateToIlDefinition_LocalMethod_Queued()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.RichLibraryDll, ct);

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
        Assert.IsTrue(disasmResponse.Success);

        var disasmData = disasmResponse.Data!.Value;
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

        Assert.IsNotNull(callToken);

        // Send navigate-to-il-definition with the found token
        var navResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate-to-il-definition", Token = callToken }, ct);
        Assert.IsTrue(navResponse.Success);

        var navData = navResponse.Data!.Value;
        Assert.AreEqual("queued", navData.GetProperty("status").GetString());
    }

    /// <summary>
    /// Verifies that navigate-to-il-definition for an external method token (Console.WriteLine)
    /// results in cross-assembly navigation, increasing the navigation depth.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task NavigateToIlDefinition_ExternalMethod_NavigatesCrossAssembly()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.RichLibraryDll, ct);

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
        Assert.IsTrue(disasmResponse.Success);

        var disasmData = disasmResponse.Data!.Value;
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

        Assert.IsNotNull(callToken);

        // Get depth before navigation
        var viewBefore = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.IsTrue(viewBefore.Success);
        var depthBefore = viewBefore.Data!.Value.GetProperty("navigationDepth").GetInt32();

        // Send navigate-to-il-definition with the external token
        var navResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate-to-il-definition", Token = callToken }, ct);
        Assert.IsTrue(navResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.NavigationStack.Count > depthBefore,
            TimeSpan.FromSeconds(5));
        Assert.IsGreaterThan(depthBefore, _state!.NavigationStack.Count);
    }

    /// <summary>
    /// Verifies that disassemble and resolve-token render MethodSpec tokens consistently
    /// through the production diagnostics socket.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task MethodSpecs_ThroughDiagnosticsSession_ReturnConstructedGenericMethods()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(
            typeof(MethodSpecReproFixture).Assembly.Location, ct);

        var disassembleResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest
            {
                Method = "disassemble",
                TypeName = MethodSpecReproFixture.TypeName,
                MethodName = MethodSpecReproFixture.MethodName
            }, ct);
        Assert.IsTrue(disassembleResponse.Success);

        var instructions = (disassembleResponse.Data!.Value)
            .GetProperty("instructions");
        var methodSpecs = instructions.EnumerateArray()
            .Where(instruction => instruction.TryGetProperty("metadataToken", out var token)
                && token.ValueKind == JsonValueKind.Number
                && (uint)token.GetInt32() >> 24 == 0x2B)
            .Select(instruction => new
            {
                Token = instruction.GetProperty("metadataToken").GetInt32(),
                Operand = instruction.GetProperty("operand").GetString()!
            })
            .ToArray();

        Assert.AreSequenceEqual(
            MethodSpecReproFixture.ExpectedDisplays,
            methodSpecs.Select(methodSpec => methodSpec.Operand));

        var resolvedNames = new List<string>();
        foreach (var methodSpec in methodSpecs)
        {
            var resolveResponse = await DotsiderClient.SendAsync(socketPath,
                new DotsiderRequest { Method = "resolve-token", Token = methodSpec.Token }, ct);
            Assert.IsTrue(resolveResponse.Success);

            var resolved = resolveResponse.Data!.Value;
            Assert.AreEqual(methodSpec.Token, resolved.GetProperty("token").GetInt32());
            resolvedNames.Add(resolved.GetProperty("resolved").GetString()!);
        }

        Assert.AreSequenceEqual(MethodSpecReproFixture.ExpectedDisplays, resolvedNames);
    }

    // --- navigate-back ---

    /// <summary>
    /// Verifies that navigate-back after push-assembly restores the navigation depth.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task NavigateBack_AfterPush_RestoresDepth()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.RichLibraryDll, ct);

        // Push System.Runtime to increase depth
        var pushResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "push-assembly", AssemblyName = "System.Runtime" }, ct);
        Assert.IsTrue(pushResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.NavigationStack.Count == 1,
            TimeSpan.FromSeconds(5));

        // Navigate back
        var backResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate-back" }, ct);
        Assert.IsTrue(backResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.NavigationStack.Count == 0,
            TimeSpan.FromSeconds(5));
        Assert.IsEmpty(_state!.NavigationStack);
    }

    // --- push-assembly ---

    /// <summary>
    /// Verifies that push-assembly by name resolves and pushes System.Runtime,
    /// increasing the navigation depth.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task PushAssembly_ByName_ResolvesAndPushes()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.RichLibraryDll, ct);

        // Get initial depth
        var viewBefore = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.IsTrue(viewBefore.Success);
        var depthBefore = viewBefore.Data!.Value.GetProperty("navigationDepth").GetInt32();

        // Push by assembly name
        var pushResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "push-assembly", AssemblyName = "System.Runtime" }, ct);
        Assert.IsTrue(pushResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.NavigationStack.Count > depthBefore,
            TimeSpan.FromSeconds(5));
        Assert.IsGreaterThan(depthBefore, _state!.NavigationStack.Count);
    }

    /// <summary>
    /// Verifies that push-assembly by path opens the specified assembly directly,
    /// increasing the navigation depth.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task PushAssembly_ByPath_OpensDirectly()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.RichLibraryDll, ct);

        // Get initial depth
        var viewBefore = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.IsTrue(viewBefore.Success);
        var depthBefore = viewBefore.Data!.Value.GetProperty("navigationDepth").GetInt32();

        // Push by path
        var pushResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "push-assembly", AssemblyPath = Samples.HelloWorldDll }, ct);
        Assert.IsTrue(pushResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.NavigationStack.Count > depthBefore,
            TimeSpan.FromSeconds(5));
        Assert.IsGreaterThan(depthBefore, _state!.NavigationStack.Count);
    }

    /// <summary>
    /// Verifies that push-assembly by path accepts a raw SDK-produced WebAssembly runtime module.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task PushAssembly_ByWasmPath_OpensRawWasmModule()
    {
        var wasmPath = GetWasmNativePath();
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.RichLibraryDll, ct);

        var pushResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "push-assembly", AssemblyPath = wasmPath }, ct);
        Assert.IsTrue(pushResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.Analyzer.BinaryKind == global::Dotsider.Core.Analysis.Models.BinaryKind.Wasm,
            TimeSpan.FromSeconds(5));

        var info = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "assembly-info" }, ct);
        Assert.IsTrue(info.Success);
        var data = info.Data!.Value;
        Assert.AreEqual("wasm", data.GetProperty("binaryKind").GetString());
        Assert.IsFalse(data.GetProperty("hasMetadata").GetBoolean());
        Assert.IsGreaterThan(0, data.GetProperty("wasm").GetProperty("definedFunctionCount").GetInt32());
    }

    /// <summary>
    /// Verifies that push-assembly by path correctly handles an apphost exe
    /// by loading the companion managed assembly instead of the native host.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task PushAssembly_ByApphostPath_OpensCompanion()
    {
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.RichLibraryDll, ct);

        var pushResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "push-assembly", AssemblyPath = Samples.HelloWorldExe }, ct);
        Assert.IsTrue(pushResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.NavigationStack.Count > 0,
            TimeSpan.FromSeconds(5));

        // Verify the pushed assembly has metadata (companion DLL, not the native host)
        var info = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "assembly-info" }, ct);
        Assert.IsTrue(info.Success);
        var data = info.Data!.Value;
        Assert.IsTrue(data.GetProperty("hasMetadata").GetBoolean());
    }

    /// <summary>
    /// Verifies that push-assembly by path correctly handles a single-file bundle
    /// by extracting and loading the entry assembly with metadata.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task PushAssembly_ByBundlePath_OpensEntryAssembly()
    {
        Assert.IsNotNull(Samples.SelfContainedConsoleExe);
        var ct = CancellationToken.None;
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.RichLibraryDll, ct);

        var pushResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "push-assembly", AssemblyPath = Samples.SelfContainedConsoleExe }, ct);
        Assert.IsTrue(pushResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.Analyzer.AssemblyName == "SelfContainedConsole",
            TimeSpan.FromSeconds(5));

        // Verify the pushed assembly has metadata (entry assembly, not the bundle host)
        var info = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "assembly-info" }, ct);
        Assert.IsTrue(info.Success);
        var data = info.Data!.Value;
        Assert.IsTrue(data.GetProperty("hasMetadata").GetBoolean());
        Assert.AreEqual("SelfContainedConsole", data.GetProperty("assemblyName").GetString());
    }

    /// <summary>
    /// Verifies that navigate-back restores the apphost dialog when popping
    /// back to a native apphost with a known companion DLL.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task NavigateBack_ToApphost_RestoresApphostDialog()
    {
        var ct = CancellationToken.None;
        // Open the apphost exe — DotsiderState sets ApphostDialogOpen = true
        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.HelloWorldExe, ct);

        // Push the companion DLL to navigate away from the dialog
        var pushResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "push-assembly", AssemblyPath = Samples.HelloWorldDll }, ct);
        Assert.IsTrue(pushResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.NavigationStack.Count > 0,
            TimeSpan.FromSeconds(5));

        // Navigate back — should return to the apphost and reopen the dialog
        var backResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate-back" }, ct);
        Assert.IsTrue(backResponse.Success);
        await TestHelpers.WaitUntilAsync(
            () => _state?.NavigationStack.Count == 0,
            TimeSpan.FromSeconds(5));

        // The analyzer should be the native apphost (no metadata)
        Assert.IsFalse(_state!.Analyzer.HasMetadata);

        // The apphost dialog must be reopened so the user can navigate to the companion
        Assert.IsTrue(_state.ApphostDialogOpen);
        Assert.IsNotNull(_state.ApphostCompanionDllPath);
    }

    private static string GetWasmNativePath()
    {
        TestSkip.When(
            Samples.WasmConsoleNativeWasm is null && Samples.ReadyToRunConsoleWasmNativeWasm is null,
            "browser-wasm publish did not run on this leg.");

        return Samples.WasmConsoleNativeWasm ?? Samples.ReadyToRunConsoleWasmNativeWasm!;
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
        if (_appTask is not null)
        {
            try { await _appTask; }
            catch (OperationCanceledException) { }
        }
        _state?.Dispose();
        _app?.Dispose();
        if (_terminal is not null)
            await _terminal.DisposeAsync();
        _appCts?.Dispose();
    }
}
