using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Integration tests verifying that single-file bundle executables are fully
/// analyzable — entry assembly loads, references are populated, and drill-down
/// into bundled System assemblies succeeds.
/// </summary>
[TestClass]
public sealed class SingleFileBundleAnalysisTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;

    /// <summary>
    /// Creates a headless DotsiderApp wired to the given file path,
    /// following the same pattern as <see cref="IlGoToDefinitionTests"/>.
    /// </summary>
    private (Hex1bTerminal Terminal, Hex1bApp App) CreateDotsiderApp(string filePath)
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
                _state ??= new DotsiderState(_hex1bApp!, filePath);
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
    /// Verifies that opening a self-contained single-file executable via
    /// <see cref="AssemblyLoader.Open"/> produces a <see cref="AssemblyOpenResult.BundleEntry"/>
    /// with a valid entry analyzer.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void OpenSingleFileExe_LoadsEntryAssembly()
    {
        Assert.IsNotNull(Samples.SelfContainedConsoleExe);

        var result = AssemblyLoader.Open(Samples.SelfContainedConsoleExe!);
        var bundle = Assert.IsExactInstanceOfType<AssemblyOpenResult.BundleEntry>(result);

        Assert.IsTrue(bundle.EntryAnalyzer.HasMetadata);
        Assert.AreEqual("SelfContainedConsole", bundle.EntryAnalyzer.AssemblyName);
        Assert.AreEqual(Samples.SelfContainedConsoleExe, bundle.EntryAnalyzer.SourceBundlePath);

        bundle.EntryAnalyzer.Dispose();
    }

    /// <summary>
    /// Verifies that the entry assembly extracted from a single-file bundle
    /// includes System.Runtime in its assembly references.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void OpenSingleFileExe_AssemblyRefs_ContainsSystemRuntime()
    {
        Assert.IsNotNull(Samples.SelfContainedConsoleExe);
        var result = AssemblyLoader.Open(Samples.SelfContainedConsoleExe!);
        var bundle = Assert.IsExactInstanceOfType<AssemblyOpenResult.BundleEntry>(result);

        Assert.Contains(r => r.Name == "System.Runtime", bundle.EntryAnalyzer.AssemblyRefs);

        bundle.EntryAnalyzer.Dispose();
    }

    /// <summary>
    /// Opens a single-file bundle in the headless TUI, then drills down into a
    /// referenced assembly to verify that bundle-aware resolution succeeds end-to-end.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task OpenSingleFileExe_DrillDown_SystemRuntime_Succeeds()
    {
        Assert.IsNotNull(Samples.SelfContainedConsoleExe);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            CancellationToken.None);
        var (terminal, app) = CreateDotsiderApp(Samples.SelfContainedConsoleExe!);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        // Wait for the app to render, then drill down on a reference
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            // DownArrow selects a reference, Enter drills in
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Enter)
            // After drill-down, the title should no longer show SelfContainedConsole
            .WaitUntil(s => !s.ContainsText("SelfContainedConsole"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Verify we navigated — stack should have the original analyzer
        Assert.ContainsSingle(_state!.NavigationStack);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies that hex save is blocked for bundle-backed analyzers,
    /// no temp file is created, and the bundle file remains unchanged.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void OpenSingleFileExe_HexSave_IsBlocked()
    {
        Assert.IsNotNull(Samples.SelfContainedConsoleExe);

        // Record the bundle's original bytes
        var originalBytes = File.ReadAllBytes(Samples.SelfContainedConsoleExe!);

        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, Samples.SelfContainedConsoleExe!);

        // Verify the guard blocks save
        Assert.IsTrue(state.Analyzer.IsBundleBacked);
        Assert.IsFalse(state.Analyzer.CanSaveInPlace);
        DotsiderApp.SaveHexChanges(state);
        Assert.IsNotNull(state.HexNotification);
        Assert.Contains("single-file bundle", state.HexNotification!);

        // No temp file should exist
        Assert.IsFalse(File.Exists(Samples.SelfContainedConsoleExe + ".tmp"));

        // Bundle file must be unchanged
        var afterBytes = File.ReadAllBytes(Samples.SelfContainedConsoleExe!);
        Assert.AreSequenceEqual(originalBytes, afterBytes);
    }

    /// <summary>
    /// Verifies that the <see cref="AssemblyAnalyzer.LaunchPath"/> for a bundle-backed
    /// analyzer points to the bundle executable, enabling Dynamic tab tracing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void OpenSingleFileExe_LaunchPath_PointsToBundleExecutable()
    {
        Assert.IsNotNull(Samples.SelfContainedConsoleExe);

        var result = AssemblyLoader.Open(Samples.SelfContainedConsoleExe!);
        var bundle = Assert.IsExactInstanceOfType<AssemblyOpenResult.BundleEntry>(result);

        Assert.AreEqual(Samples.SelfContainedConsoleExe, bundle.EntryAnalyzer.LaunchPath);
        Assert.IsTrue(File.Exists(bundle.EntryAnalyzer.LaunchPath));

        bundle.EntryAnalyzer.Dispose();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _state?.Dispose();
        _terminal?.Dispose();
    }
}
