using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Widgets;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end protocol tests for the <c>correlate-method</c> session command over a real headless
/// TUI and diagnostics socket: a unique method resolved by name, the ambiguous-name error, and the
/// managed-assembly rejection.
/// </summary>
[TestClass]
public class PreIlcSessionSocketTests : IAsyncDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _app;
    private DotsiderState? _state;
    private DotsiderDiagnosticsListener? _listener;
    private CancellationTokenSource? _appCts;

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
        _listener.StartListening(overridePid: TestSocketIds.NextPid());

        _appCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = _app.RunAsync(_appCts.Token);
        await Task.Delay(100, ct);

        await TestHelpers.WaitUntilAsync(
            () => _state is not null,
            TimeSpan.FromSeconds(10));

        return _listener.SocketPath!;
    }

    /// <summary>
    /// Verifies <c>correlate-method</c> resolves a unique method by name and returns its report.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task CorrelateMethod_ByName_ReturnsReport()
    {
        TestSkip.When(Samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(Samples.NativeAotConsoleExe!, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "correlate-method", MethodOrAddress = "Greeter.Describe" }, ct);

        Assert.IsTrue(response.Success, response.Error);
        var data = (response.Data as JsonElement?)!.Value;
        Assert.Contains("Greeter::Describe", data.GetProperty("method").GetString()!);
        Assert.IsFalse(string.IsNullOrEmpty(data.GetProperty("il").GetString()));
    }

    /// <summary>
    /// Verifies an ambiguous name fails cleanly, listing the candidates in the error.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task CorrelateMethod_AmbiguousName_FailsWithCandidates()
    {
        TestSkip.When(Samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(Samples.NativeAotConsoleExe!, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "correlate-method", MethodOrAddress = "Greeter.Greet" }, ct);

        Assert.IsFalse(response.Success);
        Assert.Contains("ambiguous", response.Error!);
        Assert.Contains("Greeter::Greet", response.Error!);
    }

    /// <summary>
    /// Verifies <c>correlate-method</c> rejects a managed assembly with the Native AOT requirement.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task CorrelateMethod_Managed_Fails()
    {
        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(Samples.RichLibraryDll, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "correlate-method", MethodOrAddress = "Foo" }, ct);

        Assert.IsFalse(response.Success);
        Assert.Contains("Native AOT", response.Error!);
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
