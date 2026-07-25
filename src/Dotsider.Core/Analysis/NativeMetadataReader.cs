using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;
using System.Text;

namespace Dotsider.Core.Analysis;

/// <summary>
/// A clean-room reader for the <c>Internal.Metadata.NativeFormat</c> blob that ILC embeds
/// in a Native AOT binary (ReadyToRun section 313, or the reduced stack-trace metadata in
/// 326). It recovers namespace-qualified type names, method names (in metadata order), and the
/// defining assembly scope name — enough to describe a stripped binary that carries no
/// ECMA-335 metadata, and enough to demangle native symbols back to it. Only the name-bearing
/// records are decoded; signatures, attributes, and generics are stepped over. Ported clean-room from
/// the MIT-licensed reader and writer in dotnet/runtime; the name records are stable across
/// .NET 8 through 11. Malformed blobs yield the partial result gathered so far.
/// </summary>
internal sealed class NativeMetadataReader
{
    private const uint Signature = 0xDEAD_DFFD;

    internal const int MaxDepth = 256;
    internal const int MaxRecoveredTextCharacters = 1 << 24;
    internal const int MaxStringLength = 4096;
    internal const int MaxTraversalWork = 1 << 20;

    private static readonly UTF8Encoding s_utf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly ReadOnlyMemory<byte> _blob;
    private readonly HashSet<int> _methods = [];
    private readonly HashSet<int> _namespaces = [];
    private readonly HashSet<int> _scopes = [];
    private readonly Dictionary<int, string> _strings = [];
    private readonly HashSet<int> _typeDefinitions = [];
    private readonly List<RecoveredType> _types = [];
    private int _remainingTextCharacters = MaxRecoveredTextCharacters;
    private int _remainingTraversalWork = MaxTraversalWork;

    private NativeMetadataReader(ReadOnlyMemory<byte> blob) => _blob = blob;

    /// <summary>
    /// Recovers the type and method names from a binary's embedded NativeFormat metadata.
    /// </summary>
    /// <param name="image">The raw image bytes.</param>
    /// <param name="sections">The ReadyToRun section table.</param>
    /// <returns>The recovered types, or an empty list when no readable metadata is present.</returns>
    internal static IReadOnlyList<RecoveredType> ReadTypes(
        ReadOnlyMemory<byte> image, IReadOnlyList<RtrSection> sections)
    {
        var blob = FindMetadataBlob(image, sections);
        if (blob.Length < sizeof(uint)) return [];
        if (BinaryPrimitives.ReadUInt32LittleEndian(blob.Span) != Signature) return [];

        var reader = new NativeMetadataReader(blob);
        try
        {
            reader.ReadScopes();
        }
        catch (BadImageFormatException)
        {
            // Return whatever was recovered before the blob went out of shape.
        }

        return reader._types;
    }

    private static ReadOnlyMemory<byte> FindMetadataBlob(
        ReadOnlyMemory<byte> image, IReadOnlyList<RtrSection> sections)
    {
        // Prefer the full embedded metadata (313); fall back to stack-trace metadata (326).
        RtrSection? chosen = null;
        foreach (var section in sections)
        {
            if (section.SectionId == ReadyToRunReader.EmbeddedMetadata) { chosen = section; break; }
            if (section.SectionId == 326) chosen ??= section;
        }

        if (chosen is not { } section2 || ReadyToRunReader.FileRange(section2) is not var (offset, length))
            return default;
        if (!NativeImageRange.TryGet(image.Length, offset, length, out offset, out length))
            return default;

        return image.Slice(offset, length);
    }

    private void ReadScopes()
    {
        var span = _blob.Span;
        var p = 4; // past the signature
        var scopeCount = ReadHandleCount(span, ref p);
        for (var i = 0; i < scopeCount; i++)
        {
            var scopeOffset = DecodeOffset(span, ref p);
            ReadScope(scopeOffset);
        }
    }

    private void ReadScope(int offset)
    {
        EnsureRecordOffset(offset);
        if (!_scopes.Add(offset)) return;

        var span = _blob.Span;
        var p = offset;
        DecodeUnsigned(span, ref p);          // Flags
        var nameOffset = DecodeOffset(span, ref p);         // Name (the assembly simple name)
        DecodeUnsigned(span, ref p);          // HashAlgorithm
        DecodeUnsigned(span, ref p);          // MajorVersion
        DecodeUnsigned(span, ref p);          // MinorVersion
        DecodeUnsigned(span, ref p);          // BuildNumber
        DecodeUnsigned(span, ref p);          // RevisionNumber
        SkipByteCollection(span, ref p);      // PublicKey
        DecodeUnsigned(span, ref p);          // Culture
        var rootNamespace = DecodeOffset(span, ref p);

        var assemblyName = ReadString(nameOffset);
        ConsumeTraversalWork(1);
        ReadNamespace(rootNamespace, parentNamespace: "", assemblyName, depth: 0);
    }

    private void ReadNamespace(int offset, string parentNamespace, string? assemblyName, int depth)
    {
        if (depth > MaxDepth) return;
        EnsureRecordOffset(offset);
        if (!_namespaces.Add(offset)) return;

        var span = _blob.Span;
        var p = offset;
        DecodeUnsigned(span, ref p);          // ParentScopeOrNamespace (generic handle, discarded)
        var nameOffset = DecodeOffset(span, ref p);
        var typeDefs = ReadHandleList(span, ref p);
        SkipHandleList(span, ref p);          // TypeForwarders
        var childNamespaces = ReadHandleList(span, ref p);

        var name = ReadString(nameOffset);
        var fullNamespace = name.Length == 0
            ? parentNamespace
            : parentNamespace.Length == 0 ? name : ComposeName(parentNamespace, '.', name);

        if (typeDefs is not null)
        {
            foreach (var typeOffset in typeDefs)
                ReadType(typeOffset, fullNamespace, enclosingName: null, assemblyName, depth + 1);
        }

        if (childNamespaces is not null)
        {
            foreach (var childOffset in childNamespaces)
                ReadNamespace(childOffset, fullNamespace, assemblyName, depth + 1);
        }
    }

    private void ReadType(int offset, string namespaceName, string? enclosingName, string? assemblyName, int depth)
    {
        if (depth > MaxDepth) return;
        EnsureRecordOffset(offset);
        if (!_typeDefinitions.Add(offset)) return;

        var span = _blob.Span;
        var p = offset;
        DecodeUnsigned(span, ref p);          // Flags
        DecodeUnsigned(span, ref p);          // BaseType (generic handle, discarded)
        DecodeUnsigned(span, ref p);          // NamespaceDefinition (tracked via the walk)
        var nameOffset = DecodeOffset(span, ref p);
        DecodeUnsigned(span, ref p);          // Size
        DecodeUnsigned(span, ref p);          // PackingSize
        DecodeUnsigned(span, ref p);          // EnclosingType (tracked via the walk)
        var nestedTypes = ReadHandleList(span, ref p);
        var methodNames = ReadMethodNames(span, ref p);

        var name = ReadString(nameOffset);
        var fullName = enclosingName is not null
            ? ComposeName(enclosingName, '+', name)
            : namespaceName.Length == 0 ? name : ComposeName(namespaceName, '.', name);

        IReadOnlyList<string> recoveredMethods = methodNames ?? [];
        _types.Add(new RecoveredType(fullName, recoveredMethods, assemblyName));

        if (nestedTypes is not null)
        {
            foreach (var nestedOffset in nestedTypes)
                ReadType(nestedOffset, namespaceName, fullName, assemblyName, depth + 1);
        }
    }

    private string? ReadMethodName(int offset)
    {
        EnsureRecordOffset(offset);
        if (!_methods.Add(offset)) return null;

        var span = _blob.Span;
        var p = offset;
        DecodeUnsigned(span, ref p);          // Flags
        DecodeUnsigned(span, ref p);          // ImplFlags
        var nameOffset = DecodeOffset(span, ref p);
        return ReadString(nameOffset);
    }

    private List<string>? ReadMethodNames(ReadOnlySpan<byte> span, ref int p)
    {
        var count = ReadHandleCount(span, ref p);
        List<string>? names = null;
        for (var i = 0; i < count; i++)
        {
            var methodName = ReadMethodName(DecodeOffset(span, ref p));
            if (!string.IsNullOrEmpty(methodName))
            {
                names ??= new List<string>(Math.Min(count, 1024));
                names.Add(methodName);
            }
        }

        return names;
    }

    /// <summary>Reads a ConstantStringValue: a length-prefixed UTF-8 string at the offset.</summary>
    private string ReadString(int offset)
    {
        if (offset == 0) return "";
        EnsureRecordOffset(offset);
        if (_strings.TryGetValue(offset, out var cached)) return cached;

        var span = _blob.Span;
        var p = offset;
        var byteCount = DecodeCount(span, ref p, "NativeFormat string");
        if (byteCount > MaxStringLength)
            throw new BadImageFormatException("NativeFormat string exceeds the supported length.");
        EnsureRange(span, p, byteCount);

        var bytes = span.Slice(p, byteCount);
        string value;
        try
        {
            var characterCount = s_utf8.GetCharCount(bytes);
            ConsumeTextCharacters(characterCount);
            value = s_utf8.GetString(bytes);
        }
        catch (DecoderFallbackException ex)
        {
            throw new BadImageFormatException("NativeFormat string contains invalid UTF-8.", ex);
        }

        _strings.Add(offset, value);
        return value;
    }

    /// <summary>
    /// Reads a handle collection (count then that many typed offsets), returning null when empty.
    /// </summary>
    private List<int>? ReadHandleList(ReadOnlySpan<byte> span, ref int p)
    {
        var count = ReadHandleCount(span, ref p);
        if (count == 0) return null;

        var list = new List<int>(count);
        for (var i = 0; i < count; i++)
            list.Add(DecodeOffset(span, ref p));
        return list;
    }

    /// <summary>Steps over a handle collection without materializing it.</summary>
    private void SkipHandleList(ReadOnlySpan<byte> span, ref int p)
    {
        var count = ReadHandleCount(span, ref p);
        for (var i = 0; i < count; i++)
            DecodeUnsigned(span, ref p);
    }

    /// <summary>Steps over a byte collection (count then that many raw bytes).</summary>
    private static void SkipByteCollection(ReadOnlySpan<byte> span, ref int p)
    {
        var count = DecodeCount(span, ref p, "NativeFormat byte collection");
        EnsureRange(span, p, count);
        p += count;
    }

    private string ComposeName(string prefix, char separator, string name)
    {
        var characterCount = (long)prefix.Length + 1 + name.Length;
        if (characterCount > int.MaxValue)
            throw new BadImageFormatException("NativeFormat qualified name is too long.");

        ConsumeTextCharacters((int)characterCount);
        return string.Concat(prefix, separator, name);
    }

    private void ConsumeTextCharacters(int count)
    {
        if (count < 0 || count > _remainingTextCharacters)
            throw new BadImageFormatException("NativeFormat recovered text exceeds the supported limit.");

        _remainingTextCharacters -= count;
    }

    private void ConsumeTraversalWork(int count)
    {
        if (count < 0 || count > _remainingTraversalWork)
            throw new BadImageFormatException("NativeFormat traversal exceeds the supported work limit.");

        _remainingTraversalWork -= count;
    }

    private static int DecodeCount(ReadOnlySpan<byte> span, ref int p, string description)
    {
        var value = DecodeUnsigned(span, ref p);
        if (value > int.MaxValue)
            throw new BadImageFormatException($"{description} count is too large.");

        return (int)value;
    }

    private static int DecodeOffset(ReadOnlySpan<byte> span, ref int p)
    {
        var value = DecodeUnsigned(span, ref p);
        if (value > int.MaxValue)
            throw new BadImageFormatException("NativeFormat record offset is too large.");

        return (int)value;
    }

    private void EnsureRecordOffset(int offset)
    {
        if (offset <= 0 || offset >= _blob.Length)
            throw new BadImageFormatException("NativeFormat record offset lies outside the metadata blob.");
    }

    private static void EnsureRange(ReadOnlySpan<byte> span, int offset, int length)
    {
        if (!NativeImageRange.TryGet(span.Length, offset, length, out _, out _))
            throw new BadImageFormatException("NativeFormat data extends beyond the metadata blob.");
    }

    private int ReadHandleCount(ReadOnlySpan<byte> span, ref int p)
    {
        var count = DecodeCount(span, ref p, "NativeFormat handle collection");
        ConsumeTraversalWork(count);

        // Every encoded handle occupies at least one byte. Validate this before allocating
        // the materialized collections used to preserve metadata traversal order.
        EnsureRange(span, p, count);
        return count;
    }

    /// <summary>
    /// Decodes the NativeFormat variable-length unsigned integer at <paramref name="p"/> and
    /// advances it. The count of bytes is a unary prefix in the low bits of the first byte.
    /// </summary>
    private static uint DecodeUnsigned(ReadOnlySpan<byte> span, ref int p)
    {
        EnsureRange(span, p, 1);
        var b0 = span[p];
        if ((b0 & 1) == 0)
        {
            p += 1;
            return (uint)(b0 >> 1);
        }
        if ((b0 & 2) == 0)
        {
            EnsureRange(span, p, 2);
            var v = (uint)(b0 >> 2) | ((uint)span[p + 1] << 6);
            p += 2;
            return v;
        }
        if ((b0 & 4) == 0)
        {
            EnsureRange(span, p, 3);
            var v = (uint)(b0 >> 3) | ((uint)span[p + 1] << 5) | ((uint)span[p + 2] << 13);
            p += 3;
            return v;
        }
        if ((b0 & 8) == 0)
        {
            EnsureRange(span, p, 4);
            var v = (uint)(b0 >> 4) | ((uint)span[p + 1] << 4) | ((uint)span[p + 2] << 12)
                | ((uint)span[p + 3] << 20);
            p += 4;
            return v;
        }
        if ((b0 & 16) != 0)
            throw new BadImageFormatException("Invalid NativeFormat unsigned integer encoding.");

        EnsureRange(span, p, 5);
        var full = BinaryPrimitives.ReadUInt32LittleEndian(span[(p + 1)..]);
        p += 5;
        return full;
    }
}
