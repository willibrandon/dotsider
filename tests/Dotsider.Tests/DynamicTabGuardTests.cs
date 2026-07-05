using System.Runtime.InteropServices;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Dynamic Tab Guard.
/// </summary>
[Collection("SampleAssemblies")]
public class DynamicTabGuardTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;

    private Hex1bApp CreateApp()
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();
        _hex1bApp = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = _workload });
        return _hex1bApp;
    }

    private (Hex1bTerminal terminal, Hex1bApp app) CreateDotsiderApp(string dllPath)
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
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        GC.SuppressFinalize(this);
    }

    // --- Unit tests ---

    /// <summary>
    /// Verifies is net framework true for net fx console.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void IsNetFramework_TrueForNetFxConsole()
    {
        Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "net48 sample is Windows-only");
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.NetFxConsoleExe!);
        Assert.True(state.IsNetFramework);
        Assert.Contains(".NETFramework", state.Analyzer.TargetFramework);
    }

    /// <summary>
    /// CLR 2 root carries no <c>TargetFrameworkAttribute</c>, but the binder still detects
    /// .NET Framework via the mscorlib v2 reference. Both <c>IsNetFramework</c> (the Dynamic-tab
    /// gate) and <c>EffectiveTargetFrameworkDisplay</c> (the General-tab line) must reflect the
    /// inferred state — otherwise EventPipe tracing would be wrongly enabled and the General tab
    /// would show "(unknown)".
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void IsNetFramework_TrueForClr2RootWithoutTfa_AndDisplayShowsInferredLabel()
    {
        Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "net35 sample is Windows-only");
        Assert.NotNull(samples.NetFxBindingRedirectsClr2Exe);
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.NetFxBindingRedirectsClr2Exe!);

        Assert.True(state.IsNetFramework);
        Assert.Null(state.Analyzer.TargetFramework); // no TFA on the assembly
        var display = state.EffectiveTargetFrameworkDisplay;
        Assert.Contains("CLR v2.0", display);
        Assert.Contains("inferred", display, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies is net framework false for core apps.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void IsNetFramework_FalseForCoreApps()
    {
        var app = CreateApp();
        string[] paths = [samples.HelloWorldDll, samples.RichLibraryDll, samples.ComplexAppDll];
        foreach (var path in paths)
        {
            using var state = new DotsiderState(app, path);
            Assert.False(state.IsNetFramework, $"IsNetFramework should be false for {Path.GetFileName(path)}");
        }
    }

    /// <summary>
    /// Verifies is native aot true for native aot console.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void IsNativeAot_TrueForNativeAotConsole()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, samples.NativeAotConsoleExe!);
        Assert.True(state.IsNativeAot);
    }

    // --- Input sequence tests ---

    /// <summary>
    /// Verifies tab8 net framework shows guard message.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Tab8_NetFramework_ShowsGuardMessage()
    {
        Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "net48 sample is Windows-only");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.NetFxConsoleExe!);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8) // Navigate to Dynamic tab
            .WaitUntil(s => s.ContainsText(".NET Framework"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Also confirm the second guard line is visible.
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("EventPipe tracing requires"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Verify tracer was NOT created
        Assert.Null(_state!.Tracer);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies tab8 native aot shows idle view not guard.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Tab8_NativeAot_ShowsIdleViewNotGuard()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.NativeAotConsoleExe!);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Native AOT Sidecars Detected"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => !s.ContainsText("Native AOT Sidecars Detected"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8) // Navigate to Dynamic tab
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"),
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Confirm the old guard text is NOT visible.
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !s.ContainsText("CoreCLR") && !s.ContainsText("cannot be traced"),
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies native aot navigate all tabs no crash.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task NativeAot_NavigateAllTabs_NoCrash()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.NativeAotConsoleExe!);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        // Wait for initial render, then decline the pre-ILC offer so tab navigation isn't blocked.
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Native AOT Sidecars Detected"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => !s.ContainsText("Native AOT Sidecars Detected"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Navigate through every tab (1-8) — none should crash
        for (var key = Hex1bKey.D1; key <= Hex1bKey.D8; key++)
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .Key(key)
                .Build()
                .ApplyAsync(terminal, cts.Token);

            await Task.Delay(100, cts.Token);
        }

        // Verify Strings tab specifically rendered without throwing (tab 4 = Strings).
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D4)
            .WaitUntil(_ => _state!.CurrentTab == TabId.Strings, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(TabId.Strings, _state!.CurrentTab);

        cts.Cancel();
        await runTask;
    }
}
