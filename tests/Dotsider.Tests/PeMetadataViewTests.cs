using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class PeMetadataViewTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;

    private (Hex1bTerminal terminal, Hex1bApp app) CreateDotsiderApp()
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
                _state ??= new DotsiderState(_hex1bApp!, samples.RichLibraryDll)
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

    [Fact(Timeout = 10_000)]
    public async Task PeMetadata_ShowsPeHeaders()
    {
        var (terminal, app) = CreateDotsiderApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("PE Headers") && s.ContainsText("CLR Header"),
                TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 10_000)]
    public async Task PeMetadata_ShowsSectionsTable()
    {
        var (terminal, app) = CreateDotsiderApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Sections") && s.ContainsText(".text"),
                TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(PeSubTabId.Sections, _state!.PeSubTab);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 10_000)]
    public async Task PeMetadata_NavigateToTypeDef()
    {
        var (terminal, app) = CreateDotsiderApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.TypeDef, TimeSpan.FromSeconds(2))
            .WaitUntil(s => s.ContainsText("TypeDef"), TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(PeSubTabId.TypeDef, _state!.PeSubTab);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 10_000)]
    public async Task PeMetadata_NavigateToMethodDef()
    {
        var (terminal, app) = CreateDotsiderApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.TypeDef, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.MethodDef, TimeSpan.FromSeconds(2))
            .WaitUntil(s => s.ContainsText("MethodDef"), TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(PeSubTabId.MethodDef, _state!.PeSubTab);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 10_000)]
    public async Task PeMetadata_NavigateToTypeRef()
    {
        var (terminal, app) = CreateDotsiderApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(5))
            // Navigate right 3 times: Sections -> TypeDef -> MethodDef -> TypeRef
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 1, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 2, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.TypeRef, TimeSpan.FromSeconds(2))
            .WaitUntil(s => s.ContainsText("TypeRef"), TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(PeSubTabId.TypeRef, _state!.PeSubTab);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 10_000)]
    public async Task PeMetadata_NavigateToMemberRef()
    {
        var (terminal, app) = CreateDotsiderApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(5))
            // Navigate right 4 times to MemberRef
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 1, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 2, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 3, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.MemberRef, TimeSpan.FromSeconds(2))
            .WaitUntil(s => s.ContainsText("MemberRef"), TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(PeSubTabId.MemberRef, _state!.PeSubTab);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 10_000)]
    public async Task PeMetadata_NavigateToAttributes()
    {
        var (terminal, app) = CreateDotsiderApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(5))
            // Navigate right 5 times to Attributes
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 1, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 2, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 3, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 4, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.Attributes, TimeSpan.FromSeconds(2))
            .WaitUntil(s => s.ContainsText("Attributes"), TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(PeSubTabId.Attributes, _state!.PeSubTab);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 10_000)]
    public async Task PeMetadata_NavigateToResources()
    {
        var (terminal, app) = CreateDotsiderApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(5))
            // Navigate right 6 times to Resources
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 1, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 2, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 3, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 4, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == 5, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PeSubTab == PeSubTabId.Resources, TimeSpan.FromSeconds(2))
            .WaitUntil(s => s.ContainsText("Resources"), TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(PeSubTabId.Resources, _state!.PeSubTab);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 10_000)]
    public async Task PeMetadata_ShowsClrHeaderFields()
    {
        var (terminal, app) = CreateDotsiderApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s =>
                s.ContainsText("Runtime Version") && s.ContainsText("Metadata RVA"),
                TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 10_000)]
    public async Task PeMetadata_ShowsPeHeaderFields()
    {
        var (terminal, app) = CreateDotsiderApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s =>
                s.ContainsText("Machine") && s.ContainsText("Entry Point RVA"),
                TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 10_000)]
    public async Task PeMetadata_LeftArrowDoesNotGoBelowZero()
    {
        var (terminal, app) = CreateDotsiderApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Sections"), TimeSpan.FromSeconds(5))
            // Already on sub-tab 0 (Sections), press left — should stay at 0
            .Key(Hex1bKey.LeftArrow)
            .Key(Hex1bKey.LeftArrow)
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(PeSubTabId.Sections, _state!.PeSubTab);

        await runTask.ContinueWith(_ => { }, ct);
    }

    public void Dispose()
    {
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        GC.SuppressFinalize(this);
    }
}
