using System.Collections.Concurrent;
using System.Threading.Channels;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class EscapeTimeoutPresentationAdapterTests(SampleAssemblyFixture samples)
{
    // ========================================
    // Test infrastructure
    // ========================================

    /// <summary>
    /// Channel-based presentation adapter for deterministic input control.
    /// Follows hex1b's own test pattern (Hex1bTerminalTests.cs:13-63).
    /// </summary>
    private sealed class QueuedPresentationAdapter : IHex1bTerminalPresentationAdapter
    {
        private readonly Channel<ReadOnlyMemory<byte>> _input = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private readonly ConcurrentQueue<byte[]> _outputChunks = new();

        public int Width { get; set; } = 120;
        public int Height { get; set; } = 30;
        public TerminalCapabilities Capabilities => new()
        {
            SupportsMouse = true,
            Supports256Colors = true,
            SupportsTrueColor = true
        };

        public event Action<int, int>? Resized
        {
            add { }
            remove { }
        }

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        /// <summary>
        /// Enqueues raw bytes for the adapter to read.
        /// </summary>
        public void EnqueueInput(params byte[] data)
            => _input.Writer.TryWrite(data);

        public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            while (await _input.Reader.WaitToReadAsync(ct))
            {
                if (_input.Reader.TryRead(out var data))
                    return data;
            }

            return ReadOnlyMemory<byte>.Empty;
        }

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            _outputChunks.Enqueue(data.ToArray());
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Returns true if the captured output contains the given byte sequence.
        /// </summary>
        internal bool OutputContains(byte[] sequence)
        {
            var chunks = _outputChunks.ToArray();
            var totalLength = 0;
            foreach (var chunk in chunks)
                totalLength += chunk.Length;

            if (totalLength < sequence.Length)
                return false;

            var buffer = new byte[totalLength];
            var offset = 0;
            foreach (var chunk in chunks)
            {
                chunk.CopyTo(buffer, offset);
                offset += chunk.Length;
            }

            return buffer.AsSpan().IndexOf(sequence.AsSpan()) >= 0;
        }

        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public (int Row, int Column) GetCursorPosition() => (0, 0);

        public ValueTask DisposeAsync()
        {
            _input.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    // ========================================
    // Adapter integration tests
    // ========================================

    [Fact(Timeout = 30_000)]
    public async Task StandaloneEscape_ProducesEscapeEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var queued = new QueuedPresentationAdapter();
        var escAdapter = new EscapeTimeoutPresentationAdapter(queued, TimeSpan.FromMilliseconds(50));
        var events = new ConcurrentQueue<string>();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(escAdapter)
            .WithHex1bApp((app, options) =>
            {
                return ctx =>
                {
                    return ctx.TextBox()
                        .OnTextChanged(args => events.Enqueue($"text:{args.NewText}"))
                        .WithInputBindings(bindings =>
                        {
                            bindings.Key(Hex1bKey.Escape).Global().Action(_ => events.Enqueue("escape"), "Esc");
                            bindings.Key(Hex1bKey.UpArrow).Global().Action(_ => events.Enqueue("up"), "Up");
                        });
                };
            })
            .Build();

        escAdapter.Terminal = terminal;
        var runTask = terminal.RunAsync(ct);
        await TestHelpers.WaitUntilAsync(
            () => terminal.CreateSnapshot().InAlternateScreen,
            TimeSpan.FromSeconds(5),
            interval: TimeSpan.FromMilliseconds(25));

        queued.EnqueueInput(0x1b);

        await TestHelpers.WaitUntilAsync(
            () => events.Contains("escape"),
            TimeSpan.FromSeconds(10));

        Assert.Contains("escape", events);
    }

    [Fact(Timeout = 30_000)]
    public async Task SplitEscapeSequence_WithinTimeout_ProducesUpArrow()
    {
        var ct = TestContext.Current.CancellationToken;
        var queued = new QueuedPresentationAdapter();
        var escAdapter = new EscapeTimeoutPresentationAdapter(queued, TimeSpan.FromMilliseconds(50));
        var events = new ConcurrentQueue<string>();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(escAdapter)
            .WithHex1bApp((app, options) =>
            {
                return ctx =>
                {
                    return ctx.TextBox()
                        .OnTextChanged(args => events.Enqueue($"text:{args.NewText}"))
                        .WithInputBindings(bindings =>
                        {
                            bindings.Key(Hex1bKey.Escape).Global().Action(_ => events.Enqueue("escape"), "Esc");
                            bindings.Key(Hex1bKey.UpArrow).Global().Action(_ => events.Enqueue("up"), "Up");
                        });
                };
            })
            .Build();

        escAdapter.Terminal = terminal;
        var runTask = terminal.RunAsync(ct);
        await TestHelpers.WaitUntilAsync(
            () => terminal.CreateSnapshot().InAlternateScreen,
            TimeSpan.FromSeconds(5),
            interval: TimeSpan.FromMilliseconds(25));

        //Send ESC then immediately the CSI continuation
        queued.EnqueueInput(0x1b);
        queued.EnqueueInput("[A"u8.ToArray());

        await TestHelpers.WaitUntilAsync(
            () => events.Contains("up"),
            TimeSpan.FromSeconds(10));

        Assert.Contains("up", events);
        Assert.DoesNotContain("escape", events);
    }

    [Fact(Timeout = 30_000)]
    public async Task CompleteEscapeSequence_SingleRead_ProducesUpArrow()
    {
        var ct = TestContext.Current.CancellationToken;
        var queued = new QueuedPresentationAdapter();
        var escAdapter = new EscapeTimeoutPresentationAdapter(queued, TimeSpan.FromMilliseconds(50));
        var events = new ConcurrentQueue<string>();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(escAdapter)
            .WithHex1bApp((app, options) =>
            {
                return ctx =>
                {
                    return ctx.TextBox()
                        .OnTextChanged(args => events.Enqueue($"text:{args.NewText}"))
                        .WithInputBindings(bindings =>
                        {
                            bindings.Key(Hex1bKey.Escape).Global().Action(_ => events.Enqueue("escape"), "Esc");
                            bindings.Key(Hex1bKey.UpArrow).Global().Action(_ => events.Enqueue("up"), "Up");
                        });
                };
            })
            .Build();

        escAdapter.Terminal = terminal;
        var runTask = terminal.RunAsync(ct);
        await TestHelpers.WaitUntilAsync(
            () => terminal.CreateSnapshot().InAlternateScreen,
            TimeSpan.FromSeconds(5),
            interval: TimeSpan.FromMilliseconds(25));

        //Complete CSI Up Arrow in one chunk
        queued.EnqueueInput(0x1b, (byte)'[', (byte)'A');

        await TestHelpers.WaitUntilAsync(
            () => events.Contains("up"),
            TimeSpan.FromSeconds(10));

        Assert.Contains("up", events);
        Assert.DoesNotContain("escape", events);
    }

    [Fact(Timeout = 30_000)]
    public async Task CoalescedPrefixAndEsc_IBeforeEscape()
    {
        var ct = TestContext.Current.CancellationToken;
        var queued = new QueuedPresentationAdapter();
        var escAdapter = new EscapeTimeoutPresentationAdapter(queued, TimeSpan.FromMilliseconds(50));
        var events = new ConcurrentQueue<string>();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(escAdapter)
            .WithHex1bApp((app, options) =>
            {
                return ctx =>
                {
                    return ctx.TextBox()
                        .OnTextChanged(args => events.Enqueue($"text:{args.NewText}"))
                        .WithInputBindings(bindings =>
                        {
                            bindings.Key(Hex1bKey.Escape).Global().Action(_ => events.Enqueue("escape"), "Esc");
                            bindings.Key(Hex1bKey.I).Global().Action(_ => events.Enqueue("i"), "I");
                            bindings.Key(Hex1bKey.UpArrow).Global().Action(_ => events.Enqueue("up"), "Up");
                        });
                };
            })
            .Build();

        escAdapter.Terminal = terminal;
        var runTask = terminal.RunAsync(ct);
        await TestHelpers.WaitUntilAsync(
            () => terminal.CreateSnapshot().InAlternateScreen,
            TimeSpan.FromSeconds(5),
            interval: TimeSpan.FromMilliseconds(25));

        //'i' coalesced with trailing ESC in one read
        queued.EnqueueInput((byte)'i', 0x1b);

        await TestHelpers.WaitUntilAsync(
            () => events.Contains("i") && events.Contains("escape"),
            TimeSpan.FromSeconds(10));

        // 'i' should appear before 'escape' in the event queue
        var eventList = events.ToArray();
        var iIndex = Array.IndexOf(eventList, "i");
        var escIndex = Array.IndexOf(eventList, "escape");
        Assert.True(iIndex < escIndex, $"Expected 'i' before 'escape', got i={iIndex} esc={escIndex}");
    }

    [Fact(Timeout = 30_000)]
    public async Task StandaloneEsc_ThenLiteralBracket_NotCombinedIntoCsi()
    {
        var ct = TestContext.Current.CancellationToken;
        var queued = new QueuedPresentationAdapter();
        var escAdapter = new EscapeTimeoutPresentationAdapter(queued, TimeSpan.FromMilliseconds(50));
        var events = new ConcurrentQueue<string>();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(escAdapter)
            .WithHex1bApp((app, options) =>
            {
                return ctx =>
                {
                    return ctx.TextBox()
                        .OnTextChanged(args => events.Enqueue($"text:{args.NewText}"))
                        .WithInputBindings(bindings =>
                        {
                            bindings.Key(Hex1bKey.Escape).Global().Action(_ => events.Enqueue("escape"), "Esc");
                            bindings.Key(Hex1bKey.UpArrow).Global().Action(_ => events.Enqueue("up"), "Up");
                        });
                };
            })
            .Build();

        escAdapter.Terminal = terminal;
        var runTask = terminal.RunAsync(ct);
        await TestHelpers.WaitUntilAsync(
            () => terminal.CreateSnapshot().InAlternateScreen,
            TimeSpan.FromSeconds(5),
            interval: TimeSpan.FromMilliseconds(25));

        // Send ESC, wait for the escape timeout to fire (50ms adapter timeout),
        // then send literal '['. We must wait for the escape event before sending
        // the bracket to avoid the two being combined into a CSI sequence.
        queued.EnqueueInput(0x1b);
        await TestHelpers.WaitUntilAsync(
            () => events.Contains("escape"),
            TimeSpan.FromSeconds(10));
        queued.EnqueueInput("["u8.ToArray());

        await TestHelpers.WaitUntilAsync(
            () => events.Any(e => e.StartsWith("text:")),
            TimeSpan.FromSeconds(10));

        Assert.Contains("escape", events);
        // TextBox captured the literal bracket (ParseKeyInput('[') → Hex1bKey.None → falls through to TextBox)
        Assert.Contains("text:[", events);
        // No false CSI combination
        Assert.DoesNotContain("up", events);
    }

    [Fact(Timeout = 30_000)]
    public async Task StandaloneEscape_FollowedByMouseEvent_ProducesEscapeEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var queued = new QueuedPresentationAdapter();
        var escAdapter = new EscapeTimeoutPresentationAdapter(queued, TimeSpan.FromMilliseconds(50));
        var events = new ConcurrentQueue<string>();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(escAdapter)
            .WithMouse()
            .WithHex1bApp((app, options) =>
            {
                return ctx =>
                {
                    return ctx.TextBox()
                        .OnTextChanged(args => events.Enqueue($"text:{args.NewText}"))
                        .WithInputBindings(bindings =>
                        {
                            bindings.Key(Hex1bKey.Escape).Global().Action(_ => events.Enqueue("escape"), "Esc");
                            bindings.Key(Hex1bKey.UpArrow).Global().Action(_ => events.Enqueue("up"), "Up");
                            bindings.Mouse(MouseButton.Left).Action(_ => events.Enqueue("mouse-click"), "Click");
                        });
                };
            })
            .Build();

        escAdapter.Terminal = terminal;
        var runTask = terminal.RunAsync(ct);
        await TestHelpers.WaitUntilAsync(
            () => terminal.CreateSnapshot().InAlternateScreen,
            TimeSpan.FromSeconds(5),
            interval: TimeSpan.FromMilliseconds(25));

        //Send standalone ESC, then immediately a complete SGR mouse event,
        // then an Up Arrow.  Without the fix, the adapter concatenates
        // \x1b + \x1b[<0;10;5M, swallowing the standalone Escape.
        queued.EnqueueInput(0x1b);
        queued.EnqueueInput(0x1b, (byte)'[', (byte)'<', (byte)'0', (byte)';',
            (byte)'1', (byte)'0', (byte)';', (byte)'5', (byte)'M');
        queued.EnqueueInput(0x1b, (byte)'[', (byte)'A');

        await TestHelpers.WaitUntilAsync(
            () => events.Contains("escape") && events.Contains("mouse-click") && events.Contains("up"),
            TimeSpan.FromSeconds(10));

        // All three events fire in order: standalone ESC, mouse click, Up Arrow.
        var eventList = events.ToArray();
        var escIndex = Array.IndexOf(eventList, "escape");
        var mouseIndex = Array.IndexOf(eventList, "mouse-click");
        var upIndex = Array.IndexOf(eventList, "up");
        Assert.True(escIndex >= 0, "Expected escape event");
        Assert.True(mouseIndex >= 0, "Expected mouse-click event (mouse packet not silently dropped)");
        Assert.True(upIndex >= 0, "Expected up event");
        Assert.True(escIndex < mouseIndex, $"Expected escape before mouse-click, got esc={escIndex} mouse={mouseIndex}");
        Assert.True(mouseIndex < upIndex, $"Expected mouse-click before up, got mouse={mouseIndex} up={upIndex}");
    }

    /// <summary>
    /// Verifies that <c>.WithMouse()</c> on the builder enables the mouse cursor
    /// overlay rendering path in <c>Hex1bApp.RenderCursor</c>.  The builder's
    /// <c>_enableMouse</c> is snapshotted into <c>Hex1bAppOptions.EnableMouse</c>
    /// during <c>Build()</c>; setting <c>options.EnableMouse</c> in the configure
    /// callback is too late because <c>Hex1bApp</c> reads the value at
    /// construction time.  Without <c>.WithMouse()</c>, <c>_mouseEnabled</c> is
    /// <c>false</c> and <c>RenderCursor</c> returns early — the cursor stays
    /// hidden and the pointer is invisible.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Detection strategy: <c>EnterTuiMode</c> hides the hardware cursor with
    /// <c>\x1b[?25l</c>.  The only code that re-shows it (<c>\x1b[?25h</c>) for
    /// a non-<c>TerminalNode</c> widget (such as <c>TextBox</c>) is the mouse
    /// cursor overlay at <c>Hex1bApp.cs:1254</c>, which is gated by
    /// <c>_mouseEnabled</c>.  Seeing <c>\x1b[?25h</c> in the output after a
    /// mouse event therefore proves the overlay is active.
    /// </para>
    /// </remarks>
    [Fact(Timeout = 30_000)]
    public async Task WithMouse_EnablesMouseCursorOverlay()
    {
        var ct = TestContext.Current.CancellationToken;
        var queued = new QueuedPresentationAdapter();
        var escAdapter = new EscapeTimeoutPresentationAdapter(queued, TimeSpan.FromMilliseconds(50));

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(escAdapter)
            .WithMouse()
            .WithHex1bApp((app, options) =>
            {
                return ctx => ctx.TextBox();
            })
            .Build();

        escAdapter.Terminal = terminal;
        var runTask = terminal.RunAsync(ct);
        await TestHelpers.WaitUntilAsync(
            () => terminal.CreateSnapshot().InAlternateScreen,
            TimeSpan.FromSeconds(5),
            interval: TimeSpan.FromMilliseconds(25));

        //Send SGR mouse left-click at column 10, row 5.
        // This sets _mouseX/_mouseY in Hex1bApp, triggering cursor overlay
        // rendering on the next render cycle when _mouseEnabled is true.
        queued.EnqueueInput(0x1b, (byte)'[', (byte)'<', (byte)'0', (byte)';',
            (byte)'1', (byte)'0', (byte)';', (byte)'5', (byte)'M');

        // \x1b[?25h = DECTCEM show-cursor, emitted only by the mouse cursor
        // overlay path for non-TerminalNode widgets.
        byte[] cursorShow = [0x1b, (byte)'[', (byte)'?', (byte)'2', (byte)'5', (byte)'h'];

        await TestHelpers.WaitUntilAsync(
            () => queued.OutputContains(cursorShow),
            TimeSpan.FromSeconds(10));

        Assert.True(queued.OutputContains(cursorShow),
            "Expected cursor-show sequence (\\x1b[?25h) from mouse cursor overlay");
    }

    /// <summary>
    /// Known P2 limitation: when escape sequence continuation bytes arrive after the
    /// timeout, the sequence is degraded. A standalone Escape event is injected and the
    /// continuation bytes are processed as literal characters. This is inherent to
    /// timeout-based escape detection (same tradeoff in xterm, vim, tmux, neovim).
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task LateSplitSequence_KnownLimitation_DegradedBehavior()
    {
        var ct = TestContext.Current.CancellationToken;
        var queued = new QueuedPresentationAdapter();
        var escAdapter = new EscapeTimeoutPresentationAdapter(queued, TimeSpan.FromMilliseconds(50));
        var events = new ConcurrentQueue<string>();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(escAdapter)
            .WithHex1bApp((app, options) =>
            {
                return ctx =>
                {
                    return ctx.TextBox()
                        .OnTextChanged(args => events.Enqueue($"text:{args.NewText}"))
                        .WithInputBindings(bindings =>
                        {
                            bindings.Key(Hex1bKey.Escape).Global().Action(_ => events.Enqueue("escape"), "Esc");
                            bindings.Key(Hex1bKey.UpArrow).Global().Action(_ => events.Enqueue("up"), "Up");
                        });
                };
            })
            .Build();

        escAdapter.Terminal = terminal;
        var runTask = terminal.RunAsync(ct);
        await TestHelpers.WaitUntilAsync(
            () => terminal.CreateSnapshot().InAlternateScreen,
            TimeSpan.FromSeconds(5),
            interval: TimeSpan.FromMilliseconds(25));

        //Send ESC, wait well past timeout, then send what would have been a CSI Up Arrow
        queued.EnqueueInput(0x1b);
        await Task.Delay(500, ct);
        queued.EnqueueInput("[A"u8.ToArray());

        await TestHelpers.WaitUntilAsync(
            () => events.Contains("escape") && events.Any(e => e.StartsWith("text:")),
            TimeSpan.FromSeconds(10));

        // Spurious Escape injection (standalone Escape was detected)
        Assert.Contains("escape", events);
        // [A delivered as literal text (Hex1bKeyEvent.FromText("[A") → Hex1bKey.None → TextBox)
        Assert.Contains("text:[A", events);
        // NOT interpreted as Up Arrow
        Assert.DoesNotContain("up", events);
    }

    // ========================================
    // Raw-Escape user-path tests with real DotsiderApp
    // ========================================

    [Fact(Timeout = 30_000)]
    public async Task RawEscape_ExitsHexInsertMode()
    {
        var ct = TestContext.Current.CancellationToken;
        var queued = new QueuedPresentationAdapter();
        var escAdapter = new EscapeTimeoutPresentationAdapter(queued, TimeSpan.FromMilliseconds(50));
        DotsiderState? state = null;
        DotsiderApp? dotsiderApp = null;

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(escAdapter)
            .WithDimensions(120, 30)
            .WithHex1bApp((app, options) =>
            {
                options.Theme = DotsiderTheme.Create();
                options.EnableMouse = true;
                return ctx =>
                {
                    state ??= new DotsiderState(app, samples.HelloWorldDll);
                    dotsiderApp ??= new DotsiderApp(state);
                    return dotsiderApp.Build(ctx);
                };
            })
            .Build();

        escAdapter.Terminal = terminal;
        var runTask = terminal.RunAsync(ct);

        // Navigate to hex tab and enter insert mode via builder (bypasses adapter)
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5) // Hex Dump tab
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.I) // Enter insert mode
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(state);
        Assert.Equal(HexEditMode.Insert, state!.HexMode);

        // Send raw ESC through the adapter path
        queued.EnqueueInput(0x1b);

        // Wait for the async state transition
        await TestHelpers.WaitUntilAsync(
            () => state.HexMode == HexEditMode.Normal,
            TimeSpan.FromSeconds(10));

        Assert.Equal(HexEditMode.Normal, state.HexMode);
        Assert.True(state.HexEditorState.IsReadOnly);
    }

    [Fact(Timeout = 30_000)]
    public async Task RawEscape_DismissesEditingSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var queued = new QueuedPresentationAdapter();
        var escAdapter = new EscapeTimeoutPresentationAdapter(queued, TimeSpan.FromMilliseconds(50));
        DotsiderState? state = null;
        DotsiderApp? dotsiderApp = null;

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(escAdapter)
            .WithDimensions(120, 30)
            .WithHex1bApp((app, options) =>
            {
                options.Theme = DotsiderTheme.Create();
                options.EnableMouse = true;
                return ctx =>
                {
                    state ??= new DotsiderState(app, samples.HelloWorldDll);
                    dotsiderApp ??= new DotsiderApp(state);
                    return dotsiderApp.Build(ctx);
                };
            })
            .Build();

        escAdapter.Terminal = terminal;
        var runTask = terminal.RunAsync(ct);

        // Activate search (editing mode, not confirmed)
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.OemQuestion) // '/' activates search
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(state);
        await TestHelpers.WaitUntilAsync(
            () => state!.Search[state.CurrentTab].IsActive && !state.Search[state.CurrentTab].IsConfirmed,
            TimeSpan.FromSeconds(10));

        // Send raw ESC through the adapter path
        queued.EnqueueInput(0x1b);

        // Wait for the async state transition
        await TestHelpers.WaitUntilAsync(
            () => !state!.Search[state.CurrentTab].IsActive,
            TimeSpan.FromSeconds(10));

        Assert.False(state.Search[state.CurrentTab].IsActive);
    }

    [Fact(Timeout = 30_000)]
    public async Task RawEscape_DismissesConfirmedSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var queued = new QueuedPresentationAdapter();
        var escAdapter = new EscapeTimeoutPresentationAdapter(queued, TimeSpan.FromMilliseconds(50));
        DotsiderState? state = null;
        DotsiderApp? dotsiderApp = null;

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithPresentation(escAdapter)
            .WithDimensions(120, 30)
            .WithHex1bApp((app, options) =>
            {
                options.Theme = DotsiderTheme.Create();
                options.EnableMouse = true;
                return ctx =>
                {
                    state ??= new DotsiderState(app, samples.HelloWorldDll);
                    dotsiderApp ??= new DotsiderApp(state);
                    return dotsiderApp.Build(ctx);
                };
            })
            .Build();

        escAdapter.Terminal = terminal;
        var runTask = terminal.RunAsync(ct);

        // Activate search, type query, confirm with Enter
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.OemQuestion) // '/' activates search
            .WaitUntil(s => s.ContainsText("/ ["), TimeSpan.FromSeconds(10)) // search bar visible
            .Key(Hex1bKey.S).Key(Hex1bKey.Y).Key(Hex1bKey.S) // "sys"
            .Key(Hex1bKey.Enter) // Confirm search
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(state);
        await TestHelpers.WaitUntilAsync(
            () => state!.Search[state.CurrentTab].IsConfirmed,
            TimeSpan.FromSeconds(10));

        // Send raw ESC through the adapter path
        queued.EnqueueInput(0x1b);

        // Wait for the async state transition
        await TestHelpers.WaitUntilAsync(
            () => !state!.Search[state.CurrentTab].IsActive,
            TimeSpan.FromSeconds(10));

        Assert.False(state!.Search[state.CurrentTab].IsActive);
    }
}
