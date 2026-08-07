namespace Dotsider.Core.Analysis;

/// <summary>
/// Exposes at most a fixed number of readable bytes from an underlying stream.
/// </summary>
internal sealed class BoundedReadStream : Stream
{
    private readonly bool leaveOpen;
    private readonly long length;
    private long remaining;
    private readonly Stream source;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoundedReadStream"/> class.
    /// </summary>
    /// <param name="source">The readable source stream.</param>
    /// <param name="length">The maximum number of bytes that may be read.</param>
    /// <param name="leaveOpen"><c>true</c> to leave <paramref name="source"/> open when this stream is disposed.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    internal BoundedReadStream(Stream source, long length, bool leaveOpen)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
            throw new ArgumentException("The source stream must be readable.", nameof(source));
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        this.source = source;
        this.length = length;
        remaining = length;
        this.leaveOpen = leaveOpen;
    }

    /// <summary>
    /// Gets a value indicating whether the stream supports reading.
    /// </summary>
    public override bool CanRead => source.CanRead;

    /// <summary>
    /// Gets a value indicating whether the stream supports seeking.
    /// </summary>
    public override bool CanSeek => false;

    /// <summary>
    /// Gets a value indicating whether the stream supports writing.
    /// </summary>
    public override bool CanWrite => false;

    /// <summary>
    /// Gets the maximum readable length of this stream.
    /// </summary>
    public override long Length => length;

    /// <summary>
    /// Gets the number of bytes already read from this stream.
    /// </summary>
    public override long Position
    {
        get => length - remaining;
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Flushes the underlying stream.
    /// </summary>
    public override void Flush()
    {
        source.Flush();
    }

    /// <summary>
    /// Reads up to the remaining bounded bytes into <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">The buffer to receive bytes.</param>
    /// <param name="offset">The offset at which to begin writing.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <returns>The number of bytes read, or zero when the bounded range is exhausted.</returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ValidateBufferArguments(buffer, offset, count);

        var bytesToRead = GetReadCount(count);
        if (bytesToRead == 0)
            return 0;

        var bytesRead = source.Read(buffer, offset, bytesToRead);
        remaining -= bytesRead;
        return bytesRead;
    }

    /// <summary>
    /// Reads up to the remaining bounded bytes into <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">The buffer to receive bytes.</param>
    /// <returns>The number of bytes read, or zero when the bounded range is exhausted.</returns>
    public override int Read(Span<byte> buffer)
    {
        var bytesToRead = GetReadCount(buffer.Length);
        if (bytesToRead == 0)
            return 0;

        var bytesRead = source.Read(buffer[..bytesToRead]);
        remaining -= bytesRead;
        return bytesRead;
    }

    /// <summary>
    /// Asynchronously reads up to the remaining bounded bytes into <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">The buffer to receive bytes.</param>
    /// <param name="offset">The offset at which to begin writing.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation.</returns>
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ValidateBufferArguments(buffer, offset, count);

        var bytesToRead = GetReadCount(count);
        return bytesToRead == 0
            ? Task.FromResult(0)
            : ReadAsyncCore(buffer, offset, bytesToRead, cancellationToken);
    }

    /// <summary>
    /// Asynchronously reads up to the remaining bounded bytes into <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">The buffer to receive bytes.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A value task that represents the asynchronous read operation.</returns>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var bytesToRead = GetReadCount(buffer.Length);
        return bytesToRead == 0
            ? ValueTask.FromResult(0)
            : ReadAsyncCore(buffer[..bytesToRead], cancellationToken);
    }

    /// <summary>
    /// Reads the next byte from the bounded range.
    /// </summary>
    /// <returns>The next byte, or <c>-1</c> when the bounded range is exhausted.</returns>
    public override int ReadByte()
    {
        if (remaining == 0)
            return -1;

        var value = source.ReadByte();
        if (value >= 0)
            remaining--;

        return value;
    }

    /// <summary>
    /// Sets the position within the stream.
    /// </summary>
    /// <param name="offset">The byte offset relative to <paramref name="origin"/>.</param>
    /// <param name="origin">The reference point used to obtain the new position.</param>
    /// <returns>Never returns because seeking is not supported.</returns>
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Sets the length of the stream.
    /// </summary>
    /// <param name="value">The requested length.</param>
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Writes bytes to the stream.
    /// </summary>
    /// <param name="buffer">The bytes to write.</param>
    /// <param name="offset">The offset at which to begin reading from <paramref name="buffer"/>.</param>
    /// <param name="count">The number of bytes to write.</param>
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Releases the stream and optionally the underlying source stream.
    /// </summary>
    /// <param name="disposing"><c>true</c> when called during disposal; otherwise <c>false</c>.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !leaveOpen)
            source.Dispose();

        base.Dispose(disposing);
    }

    private int GetReadCount(int requestedCount)
    {
        return (int)Math.Min(remaining, requestedCount);
    }

    private async Task<int> ReadAsyncCore(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var bytesRead = await source.ReadAsync(
            buffer.AsMemory(offset, count),
            cancellationToken).ConfigureAwait(false);
        remaining -= bytesRead;
        return bytesRead;
    }

    private async ValueTask<int> ReadAsyncCore(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        remaining -= bytesRead;
        return bytesRead;
    }
}
