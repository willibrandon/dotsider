using Hex1b;

namespace Dotsider.Infrastructure;

/// <summary>
/// Wraps a presentation adapter to implement escape key timeout detection.
/// </summary>
/// <remarks>
/// <para>
/// Terminal input delivers a standalone <c>\x1b</c> byte for the Escape key,
/// which is ambiguous — it could also be the first byte of a multi-byte escape
/// sequence (CSI, SS3, APC, etc.) that was split across reads.  The terminal
/// engine correctly buffers such incomplete sequences until more data arrives.
/// </para>
/// <para>
/// This adapter implements the classic terminal escape-timeout approach: when a
/// read ends with <c>\x1b</c>, it races the next read against a short timer.
/// If the read wins, the bytes are combined and the escape sequence passes
/// through intact.  If the timer wins, the trailing <c>\x1b</c> was a
/// standalone Escape keypress and is injected via
/// <see cref="Hex1bTerminal.SendInputAsync"/>.
/// </para>
/// <para>
/// Unlike a <c>CancelAfter</c> approach, the inner read is never cancelled on
/// timeout — it is saved and resumed on the next call.  This avoids discarding
/// continuation bytes that arrive slightly after the deadline (common on SSH
/// and WebSocket sessions where escape sequences are routinely split across
/// reads by more than the configured timeout).
/// </para>
/// <para>
/// <b>Known limitation:</b> If escape sequence continuation bytes arrive after
/// the configured timeout (e.g., on high-latency SSH/WebSocket connections),
/// the sequence is degraded: a standalone Escape event is injected and the
/// continuation bytes are processed as literal characters. The bytes are
/// preserved but their escape-sequence semantics are not.
/// Use <c>--escape-timeout</c> to increase the timeout for slow connections.
/// </para>
/// <para>
/// When the read that triggered the timeout also contained bytes before the
/// trailing <c>\x1b</c> (e.g. the OS coalesced <c>i</c> + <c>Esc</c> into one
/// read), those prefix bytes are returned first.  The Escape injection is
/// deferred to the next <see cref="ReadInputAsync"/> call so that the terminal
/// engine processes the prefix before seeing the Escape event.
/// </para>
/// </remarks>
/// <remarks>
/// Creates a new escape-timeout presentation adapter.
/// </remarks>
/// <param name="inner">The real presentation adapter to wrap.</param>
/// <param name="escapeTimeout">
/// How long to wait for follow-up bytes after a trailing <c>\x1b</c>.
/// Defaults to 100 ms — long enough for most SSH round-trips, short
/// enough to feel near-instantaneous on a local keypress.
/// Configurable via <c>--escape-timeout</c>.
/// </param>
public sealed class EscapeTimeoutPresentationAdapter(
    IHex1bTerminalPresentationAdapter inner,
    TimeSpan? escapeTimeout = null) : IHex1bTerminalPresentationAdapter
{
    private readonly IHex1bTerminalPresentationAdapter _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly TimeSpan _escapeTimeout = escapeTimeout ?? TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// When true, the next <see cref="ReadInputAsync"/> call injects a
    /// standalone Escape event before reading new data.  This ensures
    /// preceding bytes (returned on the previous call) are processed first.
    /// </summary>
    private bool _pendingEscapeInjection;

    /// <summary>
    /// A still-running inner read saved from a previous timeout.  Awaited
    /// on the next call so late-arriving continuation bytes are not lost.
    /// </summary>
    private Task<ReadOnlyMemory<byte>>? _pendingInnerRead;

    /// <summary>
    /// The terminal instance used to inject Escape key events.
    /// Must be set after <see cref="Hex1bTerminal.CreateBuilder"/> returns.
    /// </summary>
    /// <summary>
    /// The terminal instance used to inject standalone Escape events.
    /// Must be set after the terminal is created.
    /// </summary>
    public Hex1bTerminal? Terminal { get; set; }

    /// <inheritdoc />
    public int Width => _inner.Width;

    /// <inheritdoc />
    public int Height => _inner.Height;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => _inner.Capabilities;

    /// <inheritdoc />
    public event Action<int, int>? Resized
    {
        add => _inner.Resized += value;
        remove => _inner.Resized -= value;
    }

    /// <inheritdoc />
    public event Action? Disconnected
    {
        add => _inner.Disconnected += value;
        remove => _inner.Disconnected -= value;
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        while (true)
        {
            // Deliver deferred standalone Escape from a previous read.
            // The preceding bytes have already been returned and processed
            // by the terminal engine, so ordering is preserved.
            // Return the escape byte directly instead of using SendInputAsync
            // to guarantee it is processed after the prefix bytes.
            if (_pendingEscapeInjection)
            {
                _pendingEscapeInjection = false;
                return new byte[] { 0x1b };
            }

            // Resume a still-running inner read saved from a previous timeout,
            // or start a fresh one.
            ReadOnlyMemory<byte> data;
            if (_pendingInnerRead is { } pendingRead)
            {
                _pendingInnerRead = null;
                data = await pendingRead;
            }
            else
            {
                data = await _inner.ReadInputAsync(ct);
            }

            if (data.IsEmpty)
                return data;

            // Fast path: last byte is not ESC — pass through unchanged.
            if (data.Span[data.Length - 1] != 0x1b)
                return data;

            // Trailing \x1b detected.  Race the next inner read against a
            // short timer.  The inner read is never cancelled — if the timer
            // wins, the read is saved so late-arriving continuation bytes
            // (common on SSH / WebSocket) are picked up on the next call.
            var withoutTrailingEsc = data.Length > 1
                ? data[..^1]
                : ReadOnlyMemory<byte>.Empty;

            var nextReadTask = _inner.ReadInputAsync(ct).AsTask();
            var timeoutTask = Task.Delay(_escapeTimeout, CancellationToken.None);

            var winner = await Task.WhenAny(nextReadTask, timeoutTask);

            if (winner == nextReadTask || nextReadTask.IsCompleted)
            {
                // More data arrived in time — combine so the escape sequence
                // passes through the terminal's tokenizer intact.
                var more = await nextReadTask;
                if (!more.IsEmpty)
                {
                    // If the continuation starts with \x1b it is a new,
                    // independent sequence (e.g. an SGR mouse event
                    // \x1b[<...M) — not a continuation of the trailing
                    // \x1b from the previous read.  Keep the standalone
                    // Escape separate so the terminal engine sees it.
                    if (more.Span[0] == 0x1b)
                    {
                        _pendingInnerRead = Task.FromResult(more);

                        if (!withoutTrailingEsc.IsEmpty)
                        {
                            _pendingEscapeInjection = true;
                            return withoutTrailingEsc;
                        }

                        var terminal = Terminal;
                        if (terminal != null)
                            await terminal.SendInputAsync([0x1b], ct);
                        continue;
                    }

                    var combined = new byte[data.Length + more.Length];
                    data.CopyTo(combined);
                    more.CopyTo(combined.AsMemory(data.Length));
                    return combined;
                }

                // Empty result: real EOF / outer cancellation.
                return ReadOnlyMemory<byte>.Empty;
            }

            // Timeout: standalone Escape keypress.
            // Save the still-running inner read for the next call.
            _pendingInnerRead = nextReadTask;

            if (!withoutTrailingEsc.IsEmpty)
            {
                // Return prefix bytes first.  The Escape injection is deferred
                // to the next ReadInputAsync call so the terminal engine
                // processes the prefix before seeing the Escape event.
                _pendingEscapeInjection = true;
                return withoutTrailingEsc;
            }

            // The \x1b was the only byte — inject directly and loop to
            // read the next real input.
            var t = Terminal;
            if (t != null)
                await t.SendInputAsync([0x1b], ct);
        }
    }

    /// <inheritdoc />
    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => _inner.WriteOutputAsync(data, ct);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken ct = default)
        => _inner.FlushAsync(ct);

    /// <inheritdoc />
    public ValueTask EnterRawModeAsync(CancellationToken ct = default)
        => _inner.EnterRawModeAsync(ct);

    /// <inheritdoc />
    public ValueTask ExitRawModeAsync(CancellationToken ct = default)
        => _inner.ExitRawModeAsync(ct);

    /// <inheritdoc />
    public (int Row, int Column) GetCursorPosition()
        => _inner.GetCursorPosition();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
        => await _inner.DisposeAsync();
}
