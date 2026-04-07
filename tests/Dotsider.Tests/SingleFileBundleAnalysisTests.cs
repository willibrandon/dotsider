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
[Collection("SampleAssemblies")]
public sealed class SingleFileBundleAnalysisTests(SampleAssemblyFixture samples) : IDisposable
{
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
    [Fact(Timeout = 30_000)]
    public void OpenSingleFileExe_LoadsEntryAssembly()
    {
        Assert.NotNull(samples.SelfContainedConsoleExe);

        var result = AssemblyLoader.Open(samples.SelfContainedConsoleExe!);
        var bundle = Assert.IsType<AssemblyOpenResult.BundleEntry>(result);

        Assert.True(bundle.EntryAnalyzer.HasMetadata);
        Assert.Equal("SelfContainedConsole", bundle.EntryAnalyzer.AssemblyName);
        Assert.Equal(samples.SelfContainedConsoleExe, bundle.EntryAnalyzer.SourceBundlePath);

        bundle.EntryAnalyzer.Dispose();
    }

    /// <summary>
    /// Verifies that the entry assembly extracted from a single-file bundle
    /// includes System.Runtime in its assembly references.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void OpenSingleFileExe_AssemblyRefs_ContainsSystemRuntime()
    {
        Assert.NotNull(samples.SelfContainedConsoleExe);
        var result = AssemblyLoader.Open(samples.SelfContainedConsoleExe!);
        var bundle = Assert.IsType<AssemblyOpenResult.BundleEntry>(result);

        Assert.Contains(bundle.EntryAnalyzer.AssemblyRefs,
            r => r.Name == "System.Runtime");

        bundle.EntryAnalyzer.Dispose();
    }

    /// <summary>
    /// Opens a single-file bundle in the headless TUI, then drills down into a
    /// referenced assembly to verify that bundle-aware resolution succeeds end-to-end.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task OpenSingleFileExe_DrillDown_SystemRuntime_Succeeds()
    {
        Assert.NotNull(samples.SelfContainedConsoleExe);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.SelfContainedConsoleExe!);
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
        Assert.Single(_state!.NavigationStack);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies that hex save is blocked for bundle-backed analyzers,
    /// no temp file is created, and the bundle file remains unchanged.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void OpenSingleFileExe_HexSave_IsBlocked()
    {
        Assert.NotNull(samples.SelfContainedConsoleExe);

        // Record the bundle's original bytes
        var originalBytes = File.ReadAllBytes(samples.SelfContainedConsoleExe!);

        var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = new Hex1bAppWorkloadAdapter() });
        using var state = new DotsiderState(app, samples.SelfContainedConsoleExe!);

        // Verify the guard blocks save
        Assert.True(state.Analyzer.IsBundleBacked);
        Assert.False(state.Analyzer.CanSaveInPlace);
        DotsiderApp.SaveHexChanges(state);
        Assert.NotNull(state.HexNotification);
        Assert.Contains("single-file bundle", state.HexNotification!);

        // No temp file should exist
        Assert.False(File.Exists(samples.SelfContainedConsoleExe + ".tmp"));

        // Bundle file must be unchanged
        var afterBytes = File.ReadAllBytes(samples.SelfContainedConsoleExe!);
        Assert.Equal(originalBytes, afterBytes);
    }

    /// <summary>
    /// Verifies that the <see cref="AssemblyAnalyzer.LaunchPath"/> for a bundle-backed
    /// analyzer points to the bundle executable, enabling Dynamic tab tracing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void OpenSingleFileExe_LaunchPath_PointsToBundleExecutable()
    {
        Assert.NotNull(samples.SelfContainedConsoleExe);

        var result = AssemblyLoader.Open(samples.SelfContainedConsoleExe!);
        var bundle = Assert.IsType<AssemblyOpenResult.BundleEntry>(result);

        Assert.Equal(samples.SelfContainedConsoleExe, bundle.EntryAnalyzer.LaunchPath);
        Assert.True(File.Exists(bundle.EntryAnalyzer.LaunchPath));

        bundle.EntryAnalyzer.Dispose();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _state?.Dispose();
        _terminal?.Dispose();
    }
}
