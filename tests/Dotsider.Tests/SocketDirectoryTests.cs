using Dotsider.Diagnostics;
using Hex1b;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// Tests that the socket directory and socket files are created with correct permissions.
/// </summary>
[Collection("SampleAssemblies")]
public class SocketDirectoryTests(SampleAssemblyFixture samples) : IAsyncDisposable
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
    /// Verifies ensure socket directory sets mode0700.
    /// </summary>
    [Fact(Timeout = 30_000)]
    [Trait("Platform", "Unix")]
    public async Task EnsureSocketDirectory_SetsMode0700()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var ct = TestContext.Current.CancellationToken;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        var dir = Path.GetDirectoryName(socketPath)!;
        var mode = File.GetUnixFileMode(dir);
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            mode);
    }

    /// <summary>
    /// Verifies existing weak directory gets tightened.
    /// </summary>
    [Fact(Timeout = 30_000)]
    [Trait("Platform", "Unix")]
    public async Task ExistingWeakDirectory_GetsTightened()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        // Weaken the directory permissions first
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dotsider", "sockets");
        Directory.CreateDirectory(dir);
        File.SetUnixFileMode(dir,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        // Starting the TUI calls EnsureSocketDirectory which repairs permissions
        var ct = TestContext.Current.CancellationToken;
        await StartTuiWithDiagnosticsAsync(ct);

        var mode = File.GetUnixFileMode(dir);
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            mode);
    }

    /// <summary>
    /// Verifies windows directory has correct acl.
    /// </summary>
    [Fact(Timeout = 30_000)]
    [Trait("Platform", "Windows")]
    public async Task WindowsDirectory_HasCorrectAcl()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var ct = TestContext.Current.CancellationToken;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        VerifyWindowsDirectoryAcl(Path.GetDirectoryName(socketPath)!);
    }

    /// <summary>
    /// Verifies windows socket file inherits acl.
    /// </summary>
    [Fact(Timeout = 30_000)]
    [Trait("Platform", "Windows")]
    public async Task WindowsSocketFile_InheritsAcl()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var ct = TestContext.Current.CancellationToken;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        VerifyWindowsSocketFileAcl(socketPath);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void VerifyWindowsDirectoryAcl(string dir)
    {
        var dirInfo = new DirectoryInfo(dir);
        var security = dirInfo.GetAccessControl();
        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: false,
            typeof(System.Security.Principal.NTAccount));

        Assert.True(rules.Count >= 1);
        var currentUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        Assert.Contains(rules.Cast<System.Security.AccessControl.FileSystemAccessRule>(),
            r => r.IdentityReference.Value == currentUser);

        Assert.True(security.AreAccessRulesProtected);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void VerifyWindowsSocketFileAcl(string socketPath)
    {
        var fileInfo = new FileInfo(socketPath);
        var security = fileInfo.GetAccessControl();

        Assert.True(security.AreAccessRulesProtected);

        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: false,
            typeof(System.Security.Principal.NTAccount));
        var currentUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        Assert.Contains(rules.Cast<System.Security.AccessControl.FileSystemAccessRule>(),
            r => r.IdentityReference.Value == currentUser);
    }
}
