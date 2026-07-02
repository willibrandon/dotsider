using System.Runtime.InteropServices;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Pe Metadata View.
/// </summary>
[Collection("SampleAssemblies")]
public class PeMetadataViewTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;

    private (Hex1bTerminal terminal, Hex1bApp app) CreateDotsiderApp(string? assemblyPath = null)
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
                _state ??= new DotsiderState(_hex1bApp!, assemblyPath ?? samples.RichLibraryDll)
                {
                    CurrentTab = TabId.PeMetadata
                };
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
    /// Verifies pe metadata shows pe headers.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_ShowsPeHeaders()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("PE Headers") && s.ContainsText("CLR Header"),
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata shows sections table.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_ShowsSectionsTable()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections") && s.ContainsText(".text"),
                TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(PeSubTabId.Sections, _state!.PeSubTab);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata navigate to type def.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_NavigateToTypeDef()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.TypeDef, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("TypeDef"), TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(PeSubTabId.TypeDef, _state!.PeSubTab);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata navigate to method def.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_NavigateToMethodDef()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.TypeDef, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.MethodDef, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("MethodDef"), TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(PeSubTabId.MethodDef, _state!.PeSubTab);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata navigate to type ref.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_NavigateToTypeRef()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            // Navigate right 3 times: Sections -> TypeDef -> MethodDef -> TypeRef
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 1, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 2, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.TypeRef, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("TypeRef"), TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(PeSubTabId.TypeRef, _state!.PeSubTab);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata navigate to member ref.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_NavigateToMemberRef()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            // Navigate right 4 times to MemberRef
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 1, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 2, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 3, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.MemberRef, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("MemberRef"), TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(PeSubTabId.MemberRef, _state!.PeSubTab);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata navigate to attributes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_NavigateToAttributes()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            // Navigate right 5 times to Attributes
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 1, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 2, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 3, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 4, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.Attributes, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Attributes"), TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(PeSubTabId.Attributes, _state!.PeSubTab);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata navigate to resources.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_NavigateToResources()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            // Navigate right 6 times to Resources
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 1, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 2, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 3, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 4, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 5, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.Resources, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Resources"), TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(PeSubTabId.Resources, _state!.PeSubTab);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata navigate to debug directory.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_NavigateToDebugDirectory()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await auto.WaitUntilAsync(
            s => s.InAlternateScreen && s.ContainsText("Sections"),
            description: "PE metadata renders");

        for (var expected = PeSubTabId.TypeDef; expected <= PeSubTabId.DebugDirectory; expected++)
        {
            await auto.KeyAsync(Hex1bKey.RightArrow, ct: cts.Token);
            var expectedSubTab = expected;
            await auto.WaitUntilAsync(
                _ => _state!.PeSubTab == expectedSubTab,
                description: $"PE sub-tab {expectedSubTab} selected");
        }

        await auto.WaitUntilAsync(
            s => s.ContainsText("Debug Directory") && s.ContainsText("CodeView"),
            description: "Debug Directory table renders");
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilAsync(
            _ => _state!.PeDetailContent?.Contains("Debug Directory", StringComparison.Ordinal) == true,
            description: "Debug Directory detail opens");

        Assert.Equal(PeSubTabId.DebugDirectory, _state!.PeSubTab);
        Assert.Contains("Payload:", _state.PeDetailContent);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata shows clr header fields.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_ShowsClrHeaderFields()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s =>
                s.ContainsText("Runtime Version") && s.ContainsText("Metadata RVA"),
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata shows pe header fields.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_ShowsPeHeaderFields()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s =>
                s.ContainsText("Machine") && s.ContainsText("Entry Point RVA"),
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata enter opens detail popup.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_EnterOpensDetailPopup()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections") && s.ContainsText(".text"),
                TimeSpan.FromSeconds(10))
            // Activate the focused row to open the detail popup
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Detail"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.NotNull(_state!.PeDetailContent);
        Assert.Contains("Section:", _state.PeDetailContent);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata escape closes detail popup.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_EscapeClosesDetailPopup()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections") && s.ContainsText(".text"),
                TimeSpan.FromSeconds(10))
            // Open detail popup
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Detail"), TimeSpan.FromSeconds(10))
            // Dismiss with Escape
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state!.PeDetailContent is null, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Null(_state!.PeDetailContent);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata type def detail popup.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_TypeDefDetailPopup()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            // Navigate to TypeDef sub-tab
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.TypeDef, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("TypeDef"), TimeSpan.FromSeconds(10))
            // Open detail popup
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Detail"), TimeSpan.FromSeconds(10))
            // Dismiss with Escape
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state!.PeDetailContent is null, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Null(_state!.PeDetailContent);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata escape during search does not crash.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_EscapeDuringSearchDoesNotCrash()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            // Activate search
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.Search[TabId.PeMetadata].IsActive, TimeSpan.FromSeconds(10))
            // Press Escape to dismiss search — must not crash
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => !_state!.Search[TabId.PeMetadata].IsActive, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.False(_state!.Search[TabId.PeMetadata].IsActive);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata arrow and enter work after detail dismissed.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_ArrowAndEnterWorkAfterDetailDismissed()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections") && s.ContainsText(".text"),
                TimeSpan.FromSeconds(10))
            // Open detail popup then dismiss it
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state!.PeDetailContent is null, TimeSpan.FromSeconds(10))
            // Arrow down then Enter should open detail again
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.NotNull(_state!.PeDetailContent);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata enter works after search dismissed.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_EnterWorksAfterSearchDismissed()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections") && s.ContainsText(".text"),
                TimeSpan.FromSeconds(10))
            // Activate search, then dismiss without typing
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.Search[TabId.PeMetadata].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => !_state!.Search[TabId.PeMetadata].IsActive, TimeSpan.FromSeconds(10))
            // Enter should activate the focused row
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.NotNull(_state!.PeDetailContent);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata enter works after search with results.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_EnterWorksAfterSearchWithResults()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections") && s.ContainsText(".text"),
                TimeSpan.FromSeconds(10))
            // Search for ".text", cycle with n, then dismiss
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.Search[TabId.PeMetadata].IsActive, TimeSpan.FromSeconds(10))
            .Type(".text")
            .Key(Hex1bKey.Enter)
            .Key(Hex1bKey.N)
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => !_state!.Search[TabId.PeMetadata].IsActive, TimeSpan.FromSeconds(10))
            // Enter should activate the focused row
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.NotNull(_state!.PeDetailContent);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata left arrow does not go below zero.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_LeftArrowDoesNotGoBelowZero()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            // Already on sub-tab 0 (Sections), press left — should stay at 0
            .Key(Hex1bKey.LeftArrow)
            .Key(Hex1bKey.LeftArrow)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(PeSubTabId.Sections, _state!.PeSubTab);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata section detail popup shows colored labels and hex values.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_SectionDetailPopup_ShowsColoredLabelsAndHexValues()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections") && s.ContainsText(".text"),
                TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Detail"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Verify the detail content has the expected label:value structure
        var content = _state!.PeDetailContent!;
        Assert.Contains("Section:", content);
        Assert.Contains("Virtual Address:", content);
        Assert.Contains("0x", content); // Hex values present

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata type def detail popup shows token and attributes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_TypeDefDetailPopup_ShowsTokenAndAttributes()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.TypeDef, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Detail"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var content = _state!.PeDetailContent!;
        Assert.Contains("TypeDef:", content);
        Assert.Contains("Token: 0x", content);
        Assert.Contains("Attributes:", content);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies pe metadata method def detail popup shows signature and rva.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_MethodDefDetailPopup_ShowsSignatureAndRva()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.TypeDef, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.MethodDef, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Detail"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var content = _state!.PeDetailContent!;
        Assert.Contains("MethodDef:", content);
        Assert.Contains("Token: 0x", content);
        Assert.Contains("Signature:", content);
        Assert.Contains("RVA: 0x", content);

        cts.Cancel();
        await runTask;
    }

    /// <summary>The core system library name shown in the import table of the running OS.</summary>
    private static string CoreImportLibrary =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "kernel32"
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "libSystem"
        : "libc";

    /// <summary>
    /// Verifies the Imports sub-tab shows the native import table for a Native AOT
    /// executable — PE imports on Windows, ELF needed libraries on Linux, Mach-O
    /// dylibs on macOS.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_NativeAot_ImportsTab_ShowsModules()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        var (terminal, app) = CreateDotsiderApp(samples.NativeAotConsoleExe);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10));
        for (var target = 1; target <= PeSubTabId.Imports; target++)
        {
            var expected = target;
            builder = builder
                .Key(Hex1bKey.RightArrow)
                .WaitUntil(_ => _state!.PeSubTab == expected, TimeSpan.FromSeconds(10));
        }

        // Wait for the first module's name to render (casing varies by platform, so
        // drive the on-screen match from the actual module name).
        await builder
            .WaitUntil(_ => _state!.Analyzer.Imports.Count > 0, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText(FirstModulePrefix()), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(PeSubTabId.Imports, _state!.PeSubTab);
        Assert.NotEmpty(_state.Analyzer.Imports);
        Assert.Contains(_state.Analyzer.Imports, m =>
            m.ModuleName.Contains(CoreImportLibrary, StringComparison.OrdinalIgnoreCase));

        cts.Cancel();
        await runTask;
    }

    private string FirstModulePrefix()
    {
        var name = _state!.Analyzer.Imports[0].ModuleName;
        // The Module column is 24 cells wide; match a prefix that fits without truncation.
        return name.Length <= 20 ? name : name[..20];
    }

    /// <summary>
    /// Verifies the Imports detail popup opens on Enter for a Native AOT executable.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_NativeAot_ImportsDetailPopup_OpensOnEnter()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        var (terminal, app) = CreateDotsiderApp(samples.NativeAotConsoleExe);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10));
        for (var target = 1; target <= PeSubTabId.Imports; target++)
        {
            var expected = target;
            builder = builder
                .Key(Hex1bKey.RightArrow)
                .WaitUntil(_ => _state!.PeSubTab == expected, TimeSpan.FromSeconds(10));
        }

        await builder
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Contains("Imported Function", _state!.PeDetailContent!);
        Assert.Contains("Module:", _state.PeDetailContent!);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies the Load Config sub-tab shows parsed fields for a Native AOT executable.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_NativeAot_LoadConfigTab_ShowsFields()
    {
        Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            "the load configuration directory is a PE-only structure");
        Assert.SkipWhen(samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        var (terminal, app) = CreateDotsiderApp(samples.NativeAotConsoleExe);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10));
        for (var target = 1; target <= PeSubTabId.LoadConfig; target++)
        {
            var expected = target;
            builder = builder
                .Key(Hex1bKey.RightArrow)
                .WaitUntil(_ => _state!.PeSubTab == expected, TimeSpan.FromSeconds(10));
        }

        await builder
            .WaitUntil(s => s.ContainsText("Security Cookie"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(PeSubTabId.LoadConfig, _state!.PeSubTab);
        Assert.NotNull(_state.Analyzer.LoadConfig);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies a managed DLL can navigate through the Imports, Exports, and Load
    /// Config sub-tabs without crashing even when they are empty.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_ManagedDll_NewSubTabs_NoCrash()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10));
        for (var target = 1; target <= PeSubTabId.LoadConfig; target++)
        {
            var expected = target;
            builder = builder
                .Key(Hex1bKey.RightArrow)
                .WaitUntil(_ => _state!.PeSubTab == expected, TimeSpan.FromSeconds(10));
        }

        await builder
            .WaitUntil(s => s.ContainsText("Load Config"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(PeSubTabId.LoadConfig, _state!.PeSubTab);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies the R2R Sections sub-tab shows the ReadyToRun section table for a Native
    /// AOT binary on every platform.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_NativeAot_RtrSectionsTab_ShowsSections()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var (terminal, app) = CreateDotsiderApp(samples.NativeAotConsoleExe);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10));
        for (var target = 1; target <= PeSubTabId.RtrSections; target++)
        {
            var expected = target;
            builder = builder
                .Key(Hex1bKey.RightArrow)
                .WaitUntil(_ => _state!.PeSubTab == expected, TimeSpan.FromSeconds(10));
        }

        await builder
            .WaitUntil(s => s.ContainsText("FrozenObjectRegion"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(PeSubTabId.RtrSections, _state!.PeSubTab);
        Assert.NotEmpty(_state.Analyzer.ReadyToRunSections);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies the AOT Types sub-tab shows recovered types, and the detail popup lists a
    /// type's methods.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task PeMetadata_NativeAot_AotTypesTab_ShowsTypesAndMethods()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var (terminal, app) = CreateDotsiderApp(samples.NativeAotConsoleExe);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(10));
        for (var target = 1; target <= PeSubTabId.AotTypes; target++)
        {
            var expected = target;
            builder = builder
                .Key(Hex1bKey.RightArrow)
                .WaitUntil(_ => _state!.PeSubTab == expected, TimeSpan.FromSeconds(10));
        }

        await builder
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.PeDetailContent is not null, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(PeSubTabId.AotTypes, _state!.PeSubTab);
        Assert.NotEmpty(_state.Analyzer.RecoveredTypes);
        Assert.Contains("Methods", _state.PeDetailContent!);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        GC.SuppressFinalize(this);
    }
}
