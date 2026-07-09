using Dotsider.Diagnostics;
using Hex1b;
using System.Collections.Concurrent;
using System.Runtime.Versioning;

namespace Dotsider.Tests;

/// <summary>
/// Tests that the socket directory and socket files are created with correct permissions.
/// </summary>
[TestClass]
public class SocketDirectoryTests : IAsyncDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _app;
    private DotsiderState? _state;
    private DotsiderDiagnosticsListener? _listener;
    private CancellationTokenSource? _appCts;
    private Task? _appTask;

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
                _state ??= new DotsiderState(_app!, Samples.HelloWorldDll, pendingMutations);
                var dotsiderApp = new DotsiderApp(_state);
                return Task.FromResult<Hex1b.Widgets.Hex1bWidget>(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
        });

        _listener = new DotsiderDiagnosticsListener(() => _state);
        _listener.StartListening(overridePid: TestSocketIds.NextPid());

        _appCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _appTask = _app.RunAsync(_appCts.Token);
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
        _appCts?.Cancel();
        if (_listener is not null) await _listener.DisposeAsync();
        if (_appTask is not null)
        {
            try { await _appTask; }
            catch (OperationCanceledException) { }
        }
        _state?.Dispose();
        _app?.Dispose();
        if (_terminal is not null) await _terminal.DisposeAsync();
        _appCts?.Dispose();
    }

    /// <summary>
    /// Verifies ensure socket directory sets mode0700.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [TestCategory("Unix")]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public async Task EnsureSocketDirectory_SetsMode0700()
    {
        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        var dir = Path.GetDirectoryName(socketPath)!;
        var mode = File.GetUnixFileMode(dir);
        Assert.AreEqual(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            mode);
    }

    /// <summary>
    /// Verifies existing weak directory gets tightened.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [TestCategory("Unix")]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public async Task ExistingWeakDirectory_GetsTightened()
    {
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
        var ct = CancellationToken.None;
        await StartTuiWithDiagnosticsAsync(ct);

        var mode = File.GetUnixFileMode(dir);
        Assert.AreEqual(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            mode);
    }

    /// <summary>
    /// Verifies windows directory has correct acl.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [TestCategory("Windows")]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public async Task WindowsDirectory_HasCorrectAcl()
    {
        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        VerifyWindowsDirectoryAcl(Path.GetDirectoryName(socketPath)!);
    }

    /// <summary>
    /// Verifies windows socket file inherits acl.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [TestCategory("Windows")]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public async Task WindowsSocketFile_InheritsAcl()
    {
        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        VerifyWindowsSocketFileAcl(socketPath);
    }

    [SupportedOSPlatform("windows")]
    private static void VerifyWindowsDirectoryAcl(string dir)
    {
        var dirInfo = new DirectoryInfo(dir);
        var security = dirInfo.GetAccessControl();
        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: false,
            typeof(System.Security.Principal.NTAccount));

        Assert.IsGreaterThanOrEqualTo(1, rules.Count);
        var currentUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        Assert.Contains(r => r.IdentityReference.Value == currentUser, rules.Cast<System.Security.AccessControl.FileSystemAccessRule>());

        Assert.IsTrue(security.AreAccessRulesProtected);
    }

    [SupportedOSPlatform("windows")]
    private static void VerifyWindowsSocketFileAcl(string socketPath)
    {
        var fileInfo = new FileInfo(socketPath);
        var security = fileInfo.GetAccessControl();

        Assert.IsTrue(security.AreAccessRulesProtected);

        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: false,
            typeof(System.Security.Principal.NTAccount));
        var currentUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        Assert.Contains(r => r.IdentityReference.Value == currentUser, rules.Cast<System.Security.AccessControl.FileSystemAccessRule>());
    }
}
