using Hex1b;
using Hex1b.Input;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;

namespace Dotsider.Tests;

/// <summary>
/// Wraps a <see cref="Hex1bAppWorkloadAdapter"/> and captures all OSC 52 clipboard
/// sequences emitted via <see cref="Write(string)"/>. Use <see cref="ClipboardWrites"/>
/// to assert that the app actually called <c>CopyToClipboard</c> with the expected text.
/// Pass this to <see cref="Hex1bAppOptions.WorkloadAdapter"/> and pass the inner adapter
/// to <c>WithWorkload</c> on the terminal builder.
/// </summary>
internal sealed class ClipboardCapturingWorkloadAdapter : IHex1bAppTerminalWorkloadAdapter, IDisposable
{
    private readonly Hex1bAppWorkloadAdapter _inner;

    /// <summary>All decoded clipboard texts captured from OSC 52 sequences, in order.</summary>
    internal ConcurrentQueue<string> ClipboardWrites { get; } = new();

    internal ClipboardCapturingWorkloadAdapter(Hex1bAppWorkloadAdapter inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public void Write(string text)
    {
        // Intercept OSC 52 clipboard sequences: ESC ] 52 ; c ; <base64> BEL
        if (text.StartsWith("\x1b]52;c;") && text.EndsWith('\x07'))
        {
            var base64 = text["\x1b]52;c;".Length..^1];
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            ClipboardWrites.Enqueue(decoded);
        }

        _inner.Write(text);
    }

    /// <inheritdoc />
    public void Write(ReadOnlySpan<byte> data) => _inner.Write(data);

    /// <inheritdoc />
    public void Write(ReadOnlyMemory<byte> data) => _inner.Write(data);

    /// <inheritdoc />
    public void Flush() => _inner.Flush();

    /// <inheritdoc />
    public ChannelReader<Hex1bEvent> InputEvents => _inner.InputEvents;

    /// <inheritdoc />
    public int Width => _inner.Width;

    /// <inheritdoc />
    public int Height => _inner.Height;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => _inner.Capabilities;

    /// <inheritdoc />
    public void EnterTuiMode() => _inner.EnterTuiMode();

    /// <inheritdoc />
    public void ExitTuiMode() => _inner.ExitTuiMode();

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    public void SetCursorPosition(int left, int top) => _inner.SetCursorPosition(left, top);

    /// <inheritdoc />
    public int OutputQueueDepth => _inner.OutputQueueDepth;

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        => ((IHex1bTerminalWorkloadAdapter)_inner).ReadOutputAsync(ct);

    /// <inheritdoc />
    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => ((IHex1bTerminalWorkloadAdapter)_inner).WriteInputAsync(data, ct);

    /// <inheritdoc />
    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
        => ((IHex1bTerminalWorkloadAdapter)_inner).ResizeAsync(width, height, ct);

    /// <inheritdoc />
    public event Action? Disconnected
    {
        add => ((IHex1bTerminalWorkloadAdapter)_inner).Disconnected += value;
        remove => ((IHex1bTerminalWorkloadAdapter)_inner).Disconnected -= value;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ((IAsyncDisposable)_inner).DisposeAsync();

    /// <inheritdoc />
    public void Dispose() => _inner.Dispose();
}
