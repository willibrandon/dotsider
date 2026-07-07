using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// Tests peer credential verification on the diagnostics socket.
/// Uses the full headless TUI stack with real assemblies.
///
/// Note: Cross-user rejection e2e tests require elevated privileges and are not
/// feasible in CI. The <see cref="DotsiderDiagnosticsListener.ForceRejectPeers"/>
/// seam exercises the rejection code path deterministically. Platform-specific
/// <see cref="IPeerCredentialVerifier"/> implementations are tested positively
/// (same-user returns true).
/// </summary>
[Collection("SampleAssemblies")]
public class PeerCredentialTests(SampleAssemblyFixture samples) : IAsyncDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _app;
    private DotsiderState? _state;
    private DotsiderDiagnosticsListener? _listener;

    private async Task<string> StartTuiWithDiagnosticsAsync(CancellationToken ct)
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
                _state ??= new DotsiderState(_app!, samples.HelloWorldDll, pendingMutations);
                var dotsiderApp = new DotsiderApp(_state);
                return Task.FromResult<Hex1b.Widgets.Hex1bWidget>(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
            });

        _listener = new DotsiderDiagnosticsListener(() => _state);
        _listener.StartListening(overridePid: Random.Shared.Next(100_000, 999_999));

        _ = _app.RunAsync(ct);
        await Task.Delay(100, ct);

        await TestHelpers.WaitUntilAsync(
            () => _state is not null,
            TimeSpan.FromSeconds(10));

        return _listener.SocketPath!;
    }

    /// <summary>
    /// Releases fixture state after tests complete.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (_listener is not null) await _listener.DisposeAsync();
        _state?.Dispose();
        _app?.Dispose();
        if (_terminal is not null) await _terminal.DisposeAsync();
    }

    /// <summary>
    /// Verifies same user connection accepted.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task SameUser_ConnectionAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        // Normal same-user connection should succeed
        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "assembly-info" }, ct);

        Assert.True(response.Success);
    }

    /// <summary>
    /// Verifies platform verifier returns true for same user.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PlatformVerifier_ReturnsTrueForSameUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        // Multiple sequential requests all succeed — verifier consistently passes
        for (var i = 0; i < 3; i++)
        {
            var response = await DotsiderClient.SendAsync(socketPath,
                new DotsiderRequest { Method = "assembly-info" }, ct);
            Assert.True(response.Success);
        }
    }

    /// <summary>
    /// Verifies peer rejection sends error response.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeerRejection_SendsErrorResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        _listener!.ForceRejectPeers = true;

        var rawResponse = await DotsiderClient.SendRawAsync(socketPath,
            JsonSerializer.Serialize(new DotsiderRequest { Method = "assembly-info" },
                DotsiderJsonOptions.Default), ct);

        var response = JsonSerializer.Deserialize<DotsiderResponse>(rawResponse, DotsiderJsonOptions.Default);
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Contains("peer", response.Error!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies peer rejection response contains version.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeerRejection_ResponseContainsVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        _listener!.ForceRejectPeers = true;

        var rawResponse = await DotsiderClient.SendRawAsync(socketPath,
            JsonSerializer.Serialize(new DotsiderRequest { Method = "assembly-info" },
                DotsiderJsonOptions.Default), ct);

        var doc = JsonDocument.Parse(rawResponse);
        Assert.Equal(1, doc.RootElement.GetProperty("v").GetInt32());
    }
}
