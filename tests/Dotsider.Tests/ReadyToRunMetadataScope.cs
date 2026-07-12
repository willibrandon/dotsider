using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Owns an in-memory metadata image used by ReadyToRun signature scope-transition tests.
/// </summary>
internal sealed class ReadyToRunMetadataScope : IDisposable
{
    private readonly MemoryStream _stream;
    private readonly PEReader _peReader;

    internal ReadyToRunMetadataScope(byte[] assemblyBytes)
    {
        _stream = new MemoryStream(assemblyBytes, writable: false);
        _peReader = new PEReader(_stream);
        Reader = _peReader.GetMetadataReader();
    }

    /// <summary>Gets the metadata reader for the image.</summary>
    public MetadataReader Reader { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _peReader.Dispose();
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }
}
