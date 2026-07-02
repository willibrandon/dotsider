using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Strings View.
/// </summary>
[Collection("SampleAssemblies")]
public class StringsViewTests(SampleAssemblyFixture samples) : IDisposable
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
                    CurrentTab = TabId.Strings
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
    /// Verifies strings enter opens detail popup.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Strings_EnterOpensDetailPopup()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("strings"), TimeSpan.FromSeconds(10))
            // Activate the focused row to open the detail popup
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.StringsDetailContent is not null, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("String Detail"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.NotNull(_state!.StringsDetailContent);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies strings escape closes detail popup.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Strings_EscapeClosesDetailPopup()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("strings"), TimeSpan.FromSeconds(10))
            // Open detail popup
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.StringsDetailContent is not null, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("String Detail"), TimeSpan.FromSeconds(10))
            // Dismiss with Escape
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state!.StringsDetailContent is null, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Null(_state!.StringsDetailContent);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies strings detail popup shows length.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Strings_DetailPopupShowsLength()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("strings"), TimeSpan.FromSeconds(10))
            // Open detail popup
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.StringsDetailContent is not null, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Length:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.NotNull(_state!.StringsDetailContent);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies strings arrow and enter work after detail dismissed.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Strings_ArrowAndEnterWorkAfterDetailDismissed()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("strings"), TimeSpan.FromSeconds(10))
            // Open detail popup then dismiss it
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.StringsDetailContent is not null, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state!.StringsDetailContent is null, TimeSpan.FromSeconds(10))
            // Arrow down then Enter should open detail again
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.StringsDetailContent is not null, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.NotNull(_state!.StringsDetailContent);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies strings enter works after search dismissed.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Strings_EnterWorksAfterSearchDismissed()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("strings"), TimeSpan.FromSeconds(10))
            // Activate search, then dismiss without typing
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.Search[TabId.Strings].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => !_state!.Search[TabId.Strings].IsActive, TimeSpan.FromSeconds(10))
            // Enter should activate the focused row
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.StringsDetailContent is not null, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.NotNull(_state!.StringsDetailContent);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies strings detail popup shows content.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Strings_DetailPopupShowsContent()
    {
        // Use a larger terminal so the popup has room to render content
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(160, 50)
            .Build();
        DotsiderApp? dotsiderApp = null;
        _hex1bApp = new Hex1bApp(
            ctx =>
            {
                _state ??= new DotsiderState(_hex1bApp!, samples.RichLibraryDll)
                {
                    CurrentTab = TabId.Strings
                };
                dotsiderApp ??= new DotsiderApp(_state);
                return Task.FromResult<Hex1bWidget>(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
            });
        var terminal = _terminal;
        var app = _hex1bApp;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("strings"), TimeSpan.FromSeconds(10))
            // Navigate to second row (has actual content) and open detail popup
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.StringsDetailContent is not null, TimeSpan.FromSeconds(10))
            // The popup must render both the title AND actual content text
            .WaitUntil(s => s.ContainsText("String Detail") && s.ContainsText("Length:"),
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.NotNull(_state!.StringsDetailContent);
        Assert.Contains("Length:", _state.StringsDetailContent is not null
            ? $"Length: {_state.StringsDetailContent.Length}" : "");

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies strings escape during search does not crash.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Strings_EscapeDuringSearchDoesNotCrash()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("strings"), TimeSpan.FromSeconds(10))
            // Activate search
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.Search[TabId.Strings].IsActive, TimeSpan.FromSeconds(10))
            // Press Escape to dismiss search — must not crash
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => !_state!.Search[TabId.Strings].IsActive, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.False(_state!.Search[TabId.Strings].IsActive);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies strings detail popup shows string content.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Strings_DetailPopupShowsStringContent()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("strings"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.StringsDetailContent is not null, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("String Detail") && s.ContainsText("Length:"),
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // The detail popup should show both the Length label and the string value
        Assert.NotNull(_state!.StringsDetailContent);
        Assert.True(_state.StringsDetailContent.Length > 0);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies the fourth sub-tab (Raw UTF-16) renders and becomes active.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Strings_NavigateToRawUtf16_Renders()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("User Strings"), TimeSpan.FromSeconds(10));
        for (var target = 1; target <= StringsSubTabId.RawBinaryUtf16; target++)
        {
            var expected = target;
            builder = builder
                .Key(Hex1bKey.RightArrow)
                .WaitUntil(_ => _state!.StringsSourceTab == expected, TimeSpan.FromSeconds(10));
        }

        await builder
            .WaitUntil(s => s.ContainsText("Raw (UTF-16)"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(StringsSubTabId.RawBinaryUtf16, _state!.StringsSourceTab);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies changing min length on the Raw UTF-16 sub-tab invalidates and
    /// rebuilds its cache with the new minimum.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Strings_MinLengthChange_InvalidatesUtf16Cache()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("User Strings"), TimeSpan.FromSeconds(10));
        for (var target = 1; target <= StringsSubTabId.RawBinaryUtf16; target++)
        {
            var expected = target;
            builder = builder
                .Key(Hex1bKey.RightArrow)
                .WaitUntil(_ => _state!.StringsSourceTab == expected, TimeSpan.FromSeconds(10));
        }

        await builder
            .WaitUntil(_ => _state!.CachedRawUtf16StringsMinLength == _state.StringsMinLength,
                TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.OemPlus)
            .WaitUntil(_ => _state!.StringsMinLength == 5
                && _state.CachedRawUtf16StringsMinLength == 5,
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(5, _state!.StringsMinLength);
        Assert.NotNull(_state.CachedRawUtf16Strings);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Verifies the fifth sub-tab (Frozen AOT) renders and becomes active for a Native AOT
    /// binary. The frozen strings are file-backed on Windows and macOS; on Linux the region
    /// is filled at startup so the sub-tab is present but empty.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Strings_NativeAot_FrozenSubTab_Renders()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var (terminal, app) = CreateDotsiderApp(samples.NativeAotConsoleExe);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("User Strings"), TimeSpan.FromSeconds(10));
        for (var target = 1; target <= StringsSubTabId.FrozenObject; target++)
        {
            var expected = target;
            builder = builder
                .Key(Hex1bKey.RightArrow)
                .WaitUntil(_ => _state!.StringsSourceTab == expected, TimeSpan.FromSeconds(10));
        }

        await builder
            .WaitUntil(s => s.ContainsText("Frozen (AOT)"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(StringsSubTabId.FrozenObject, _state!.StringsSourceTab);

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
