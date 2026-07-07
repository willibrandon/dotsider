using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Widgets;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end protocol tests for the <c>get-native-symbols</c> session method and the symbol
/// provenance fields on <c>assembly-info</c>, over a real headless TUI and diagnostics socket.
/// </summary>
[Collection("SampleAssemblies")]
public class SessionSymbolTests(SampleAssemblyFixture samples) : IAsyncDisposable
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
    private async Task<string> StartTuiWithDiagnosticsAsync(string dllPath, CancellationToken ct)
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

        _appCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = _app.RunAsync(_appCts.Token);
        await Task.Delay(100, ct);

        await TestHelpers.WaitUntilAsync(
            () => _state is not null,
            TimeSpan.FromSeconds(10));

        return _listener.SocketPath!;
    }

    /// <summary>
    /// Verifies <c>get-native-symbols</c> returns the symbol list with provenance for a Native
    /// AOT binary.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task GetNativeSymbols_NativeAot_ReturnsSymbolsWithProvenance()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var ct = TestContext.Current.CancellationToken;
        var socketPath = await StartTuiWithDiagnosticsAsync(samples.NativeAotConsoleExe!, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-native-symbols" }, ct);

        Assert.True(response.Success);
        var data = (response.Data as JsonElement?)!.Value;
        Assert.True(data.GetProperty("symbols").GetArrayLength() > 0);
        Assert.False(string.IsNullOrEmpty(data.GetProperty("source").GetString()));
        Assert.False(string.IsNullOrEmpty(data.GetProperty("status").GetString()));
    }

    /// <summary>Verifies <c>get-native-symbols</c> fails cleanly for a managed assembly.</summary>
    [Fact(Timeout = 30_000)]
    public async Task GetNativeSymbols_Managed_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        var socketPath = await StartTuiWithDiagnosticsAsync(samples.RichLibraryDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-native-symbols" }, ct);

        Assert.False(response.Success);
        Assert.Contains("no native symbols", response.Error);
    }

    /// <summary>
    /// Verifies <c>get-native-symbols</c> returns WebAssembly function symbols from a raw SDK
    /// browser-wasm runtime module opened in the TUI.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task GetNativeSymbols_Wasm_ReturnsSymbolsWithProvenance()
    {
        var wasmPath = GetWasmNativePath();

        var ct = TestContext.Current.CancellationToken;
        var socketPath = await StartTuiWithDiagnosticsAsync(wasmPath, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-native-symbols" }, ct);

        Assert.True(response.Success);
        var data = (response.Data as JsonElement?)!.Value;
        Assert.Equal("webAssembly", data.GetProperty("source").GetString());
        Assert.Equal("wasm32", data.GetProperty("architecture").GetString());
        Assert.True(data.GetProperty("symbols").GetArrayLength() > 0);
    }

    /// <summary>
    /// Verifies <c>disassemble-native</c> accepts WebAssembly <c>func:N</c> identifiers through
    /// the diagnostics socket, matching the CLI and MCP native-disassembly surfaces.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task DisassembleNative_Wasm_ByFunctionIndex_ReturnsInstructions()
    {
        var wasmPath = GetWasmNativePath();
        string funcAlias;
        using (var analyzer = new Dotsider.Core.Analysis.AssemblyAnalyzer(wasmPath))
        {
            var symbol = analyzer.NativeSymbols!.Symbols.First(s =>
                s.Aliases.Any(static alias => alias.StartsWith("func:", StringComparison.Ordinal)));
            funcAlias = symbol.Aliases.First(static alias => alias.StartsWith("func:", StringComparison.Ordinal));
        }

        var ct = TestContext.Current.CancellationToken;
        var socketPath = await StartTuiWithDiagnosticsAsync(wasmPath, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "disassemble-native", SymbolName = funcAlias }, ct);

        Assert.True(response.Success);
        var data = (response.Data as JsonElement?)!.Value;
        Assert.Equal("Wasm32", data.GetProperty("architecture").GetString());
        Assert.True(data.GetProperty("instructions").GetArrayLength() > 0);
    }

    /// <summary>
    /// Verifies <c>assembly-info</c> carries the native symbol count, source, and status for a
    /// Native AOT binary.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task AssemblyInfo_NativeAot_CarriesSymbolProvenance()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var ct = TestContext.Current.CancellationToken;
        var socketPath = await StartTuiWithDiagnosticsAsync(samples.NativeAotConsoleExe!, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "assembly-info" }, ct);

        Assert.True(response.Success);
        var data = (response.Data as JsonElement?)!.Value;
        Assert.True(data.GetProperty("nativeSymbolCount").GetInt32() > 0);
        Assert.False(string.IsNullOrEmpty(data.GetProperty("nativeSymbolSource").GetString()));
        Assert.False(string.IsNullOrEmpty(data.GetProperty("nativeSymbolStatus").GetString()));
    }

    private string GetWasmNativePath()
    {
        Assert.SkipWhen(samples.WasmConsoleNativeWasm is null && samples.ReadyToRunConsoleWasmNativeWasm is null,
            "browser-wasm publish did not run on this leg.");

        return samples.WasmConsoleNativeWasm ?? samples.ReadyToRunConsoleWasmNativeWasm!;
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
