using System.Buffers.Binary;
using System.Text;

namespace Dotsider.Tests;

/// <summary>
/// Builds deterministic NativeFormat metadata graphs with patchable absolute record handles.
/// </summary>
internal sealed class SyntheticNativeMetadataBuilder
{
    private const byte FullUnsignedMarker = 0x0F;
    private const uint Signature = 0xDEAD_DFFD;

    private readonly List<byte> _bytes = [];
    private readonly int[] _scopeSlots;

    /// <summary>Initializes a metadata blob with the requested scope-list capacity.</summary>
    /// <param name="scopeCount">The number of patchable scope handles.</param>
    internal SyntheticNativeMetadataBuilder(int scopeCount = 1)
    {
        Span<byte> signature = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(signature, Signature);
        AppendBytes(signature);
        WriteUnsigned(checked((uint)scopeCount));
        _scopeSlots = ReserveHandles(scopeCount);
    }

    /// <summary>Gets the current blob length.</summary>
    internal int Length => _bytes.Count;

    /// <summary>Adds a length-prefixed UTF-8 string and returns its absolute offset.</summary>
    /// <param name="value">The string value.</param>
    internal int AddString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var offset = _bytes.Count;
        WriteUnsigned(checked((uint)bytes.Length));
        AppendBytes(bytes);
        return offset;
    }

    /// <summary>Adds a string declaration with independently controlled content bytes.</summary>
    /// <param name="declaredByteCount">The encoded string byte count.</param>
    /// <param name="content">The bytes physically present after the declaration.</param>
    internal int AddString(uint declaredByteCount, ReadOnlySpan<byte> content)
    {
        var offset = _bytes.Count;
        WriteUnsigned(declaredByteCount);
        AppendBytes(content);
        return offset;
    }

    /// <summary>Adds one method record and returns its absolute offset.</summary>
    /// <param name="nameOffset">The method-name string handle.</param>
    internal int AddMethod(int nameOffset)
    {
        var offset = _bytes.Count;
        WriteUnsigned(0);
        WriteUnsigned(0);
        WriteUnsigned(checked((uint)nameOffset));
        return offset;
    }

    /// <summary>
    /// Adds a type record with patchable nested-type and method handle collections.
    /// </summary>
    /// <param name="nameOffset">The type-name string handle.</param>
    /// <param name="nestedTypeCount">The nested-type handle count.</param>
    /// <param name="methodCount">The method handle count.</param>
    /// <returns>The record offset and its patchable collection slots.</returns>
    internal (int Offset, int[] NestedTypeSlots, int[] MethodSlots) AddType(
        int nameOffset,
        int nestedTypeCount = 0,
        int methodCount = 0)
    {
        var offset = _bytes.Count;
        WriteUnsigned(0);
        WriteUnsigned(0);
        WriteUnsigned(0);
        WriteUnsigned(checked((uint)nameOffset));
        WriteUnsigned(0);
        WriteUnsigned(0);
        WriteUnsigned(0);
        WriteUnsigned(checked((uint)nestedTypeCount));
        var nestedTypeSlots = ReserveHandles(nestedTypeCount);
        WriteUnsigned(checked((uint)methodCount));
        var methodSlots = ReserveHandles(methodCount);
        return (offset, nestedTypeSlots, methodSlots);
    }

    /// <summary>
    /// Adds a namespace record with patchable type, forwarder, and child-namespace collections.
    /// </summary>
    /// <param name="nameOffset">The namespace-name string handle, or zero for the root.</param>
    /// <param name="typeCount">The type-definition handle count.</param>
    /// <param name="forwarderCount">The skipped type-forwarder handle count.</param>
    /// <param name="childNamespaceCount">The child-namespace handle count.</param>
    /// <returns>The record offset and its patchable collection slots.</returns>
    internal (
        int Offset,
        int[] TypeSlots,
        int[] ForwarderSlots,
        int[] ChildNamespaceSlots) AddNamespace(
            int nameOffset,
            int typeCount = 0,
            int forwarderCount = 0,
            int childNamespaceCount = 0)
    {
        var offset = _bytes.Count;
        WriteUnsigned(0);
        WriteUnsigned(checked((uint)nameOffset));
        WriteUnsigned(checked((uint)typeCount));
        var typeSlots = ReserveHandles(typeCount);
        WriteUnsigned(checked((uint)forwarderCount));
        var forwarderSlots = ReserveHandles(forwarderCount);
        WriteUnsigned(checked((uint)childNamespaceCount));
        var childNamespaceSlots = ReserveHandles(childNamespaceCount);
        return (offset, typeSlots, forwarderSlots, childNamespaceSlots);
    }

    /// <summary>
    /// Adds a namespace whose forwarder collection repeats one handle without retaining patch
    /// slots proportional to the collection size.
    /// </summary>
    /// <param name="nameOffset">The namespace-name string handle.</param>
    /// <param name="typeOffset">The single type-definition handle.</param>
    /// <param name="forwarderCount">The repeated forwarder handle count.</param>
    internal int AddNamespaceWithRepeatedForwarders(
        int nameOffset,
        int typeOffset,
        int forwarderCount)
    {
        var offset = _bytes.Count;
        WriteUnsigned(0);
        WriteUnsigned(checked((uint)nameOffset));
        WriteUnsigned(1);
        WriteUnsigned(checked((uint)typeOffset));
        WriteUnsigned(checked((uint)forwarderCount));
        for (var i = 0; i < forwarderCount; i++)
        {
            WriteUnsigned(0);
        }

        WriteUnsigned(0);
        return offset;
    }

    /// <summary>Adds an assembly scope record and returns its absolute offset.</summary>
    /// <param name="nameOffset">The assembly-name string handle.</param>
    /// <param name="rootNamespaceOffset">The root namespace handle.</param>
    internal int AddScope(int nameOffset, int rootNamespaceOffset)
    {
        var offset = _bytes.Count;
        WriteUnsigned(0);
        WriteUnsigned(checked((uint)nameOffset));
        WriteUnsigned(0);
        WriteUnsigned(0);
        WriteUnsigned(0);
        WriteUnsigned(0);
        WriteUnsigned(0);
        WriteUnsigned(0);
        WriteUnsigned(0);
        WriteUnsigned(checked((uint)rootNamespaceOffset));
        return offset;
    }

    /// <summary>Sets one scope-list entry.</summary>
    /// <param name="index">The zero-based scope slot.</param>
    /// <param name="scopeOffset">The scope record offset.</param>
    internal void SetScope(int index, int scopeOffset) =>
        PatchHandle(_scopeSlots[index], scopeOffset);

    /// <summary>Patches a reserved handle slot with an absolute record offset.</summary>
    /// <param name="slot">The byte offset of the fixed-width handle encoding.</param>
    /// <param name="recordOffset">The target record offset.</param>
    internal void PatchHandle(int slot, int recordOffset) =>
        PatchUnsigned(slot, checked((uint)recordOffset));

    /// <summary>Appends raw bytes and returns their starting offset.</summary>
    /// <param name="bytes">The bytes to append.</param>
    internal int AppendRaw(ReadOnlySpan<byte> bytes)
    {
        var offset = _bytes.Count;
        AppendBytes(bytes);
        return offset;
    }

    /// <summary>Appends a full-width unsigned value and returns its starting offset.</summary>
    /// <param name="value">The encoded value.</param>
    internal int AppendUnsigned(uint value)
    {
        var offset = _bytes.Count;
        WriteUnsigned(value);
        return offset;
    }

    /// <summary>Materializes the metadata blob.</summary>
    internal byte[] Build() => [.. _bytes];

    private void AppendBytes(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            _bytes.Add(value);
        }
    }

    private void PatchUnsigned(int offset, uint value)
    {
        _bytes[offset] = FullUnsignedMarker;
        Span<byte> encoded = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        for (var i = 0; i < encoded.Length; i++)
        {
            _bytes[offset + 1 + i] = encoded[i];
        }
    }

    private int[] ReserveHandles(int count)
    {
        var slots = new int[count];
        for (var i = 0; i < count; i++)
        {
            slots[i] = _bytes.Count;
            WriteUnsigned(0);
        }

        return slots;
    }

    private void WriteUnsigned(uint value)
    {
        _bytes.Add(FullUnsignedMarker);
        Span<byte> encoded = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        AppendBytes(encoded);
    }
}
