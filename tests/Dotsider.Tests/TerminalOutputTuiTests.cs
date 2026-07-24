using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Exercises hostile metadata through the real headless TUI rendering pipeline.
/// </summary>
[TestClass]
public sealed class TerminalOutputTuiTests(TestContext testContext)
{
    private readonly TestContext _testContext = testContext;

    /// <summary>
    /// Verifies a hostile user string renders visibly in both the table and detail editor without
    /// executing its OSC clipboard payload.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task StringsView_ControlPayload_RendersVisibleTextWithoutTerminalEffects()
    {
        var assemblyPath = CreateSyntheticAssembly();
        var workload = new Hex1bAppWorkloadAdapter();
        using var clipboard = new ClipboardCapturingWorkloadAdapter(workload);
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(140, 35)
            .Build();
        DotsiderState? state = null;
        DotsiderApp? dotsiderApp = null;
        Hex1bApp? app = null;
        app = new Hex1bApp(
            context =>
            {
                state ??= new DotsiderState(app!, assemblyPath)
                {
                    CurrentTab = TabId.Strings
                };
                dotsiderApp ??= new DotsiderApp(state);
                return Task.FromResult<Hex1bWidget>(dotsiderApp.Build(context));
            },
            new Hex1bAppOptions
            {
                EnableInputCoalescing = false,
                WorkloadAdapter = clipboard
            });

        var runTask = app.RunAsync(_testContext.CancellationToken);
        try
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(snapshot => snapshot.InAlternateScreen, TimeSpan.FromSeconds(10))
                .WaitUntil(
                    snapshot => snapshot.ContainsText(TerminalControlTestData.VisibleUserString),
                    TimeSpan.FromSeconds(10))
                .Key(Hex1bKey.Enter)
                .WaitUntil(_ => state!.StringsDetailContent is not null, TimeSpan.FromSeconds(10))
                .WaitUntil(
                    snapshot => snapshot.ContainsText(TerminalControlTestData.VisibleUserString),
                    TimeSpan.FromSeconds(10))
                .Build()
                .ApplyAsync(terminal, _testContext.CancellationToken);

            Assert.IsNotNull(state);
            Assert.Contains(
                entry => entry.Value == TerminalControlTestData.UserString,
                state.GetActiveStrings());
            Assert.AreEqual(TerminalControlTestData.UserString, state.StringsDetailContent);
            Assert.IsEmpty(clipboard.ClipboardWrites);
            Assert.AreNotEqual("type", terminal.WindowTitle);
        }
        finally
        {
            app.RequestStop();
            try
            {
                await runTask;
            }
            finally
            {
                state?.Dispose();
                app.Dispose();
                File.Delete(assemblyPath);
            }
        }
    }

    /// <summary>
    /// Verifies hostile TypeDef and MethodDef names remain raw in metadata state but are escaped
    /// before their table cells reach the terminal.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task PeMetadataView_ControlNames_RenderVisibleTextWithoutTerminalEffects()
    {
        var assemblyPath = CreateSyntheticAssembly();
        var workload = new Hex1bAppWorkloadAdapter();
        using var clipboard = new ClipboardCapturingWorkloadAdapter(workload);
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(140, 35)
            .Build();
        DotsiderState? state = null;
        DotsiderApp? dotsiderApp = null;
        Hex1bApp? app = null;
        app = new Hex1bApp(
            context =>
            {
                state ??= new DotsiderState(app!, assemblyPath)
                {
                    CurrentTab = TabId.PeMetadata,
                    PeSubTab = PeSubTabId.TypeDef
                };
                dotsiderApp ??= new DotsiderApp(state);
                return Task.FromResult<Hex1bWidget>(dotsiderApp.Build(context));
            },
            new Hex1bAppOptions
            {
                EnableInputCoalescing = false,
                WorkloadAdapter = clipboard
            });

        var runTask = app.RunAsync(_testContext.CancellationToken);
        try
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(snapshot => snapshot.InAlternateScreen, TimeSpan.FromSeconds(10))
                .WaitUntil(
                    snapshot => snapshot.ContainsText(TerminalControlTestData.VisibleTypeName),
                    TimeSpan.FromSeconds(10))
                .Key(Hex1bKey.RightArrow)
                .WaitUntil(_ => state!.PeSubTab == PeSubTabId.MethodDef, TimeSpan.FromSeconds(10))
                .WaitUntil(
                    snapshot => snapshot.ContainsText(TerminalControlTestData.VisibleMethodName),
                    TimeSpan.FromSeconds(10))
                .Build()
                .ApplyAsync(terminal, _testContext.CancellationToken);

            Assert.IsNotNull(state);
            Assert.Contains(
                type => type.Name == TerminalControlTestData.TypeName,
                state.MetadataAnalyzer.TypeDefs);
            Assert.Contains(
                method => method.Name == TerminalControlTestData.MethodName,
                state.MetadataAnalyzer.MethodDefs);
            Assert.IsEmpty(clipboard.ClipboardWrites);
            Assert.AreNotEqual("type", terminal.WindowTitle);
        }
        finally
        {
            app.RequestStop();
            try
            {
                await runTask;
            }
            finally
            {
                state?.Dispose();
                app.Dispose();
                File.Delete(assemblyPath);
            }
        }
    }

    private static string CreateSyntheticAssembly()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"dotsider-terminal-tui-{Guid.NewGuid():N}.dll");
        File.WriteAllBytes(
            path,
            SyntheticMetadataBuilder.BuildTerminalControlAssembly(
                TerminalControlTestData.TypeName,
                TerminalControlTestData.MethodName,
                TerminalControlTestData.FieldName,
                TerminalControlTestData.UserString));
        return path;
    }
}
