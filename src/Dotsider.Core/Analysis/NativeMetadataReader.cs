using System.Buffers.Binary;
using System.Text;
using Dotsider.Core.Analysis.Models;

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
    private const int MaxRecords = 1 << 20;
    private const int MaxDepth = 256;
    private const int MaxStringLength = 4096;

    private readonly ReadOnlyMemory<byte> _blob;
    private readonly List<RecoveredType> _types = [];
    private int _recordBudget = MaxRecords;

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
        if (blob.IsEmpty) return [];
        if (BinaryPrimitives.ReadUInt32LittleEndian(blob.Span) != Signature) return [];

        var reader = new NativeMetadataReader(blob);
        try
        {
            reader.ReadScopes();
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
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
        if (offset + length > image.Length) return default;

        return image.Slice(offset, length);
    }

    private void ReadScopes()
    {
        var span = _blob.Span;
        var p = 4; // past the signature
        var scopeCount = DecodeUnsigned(span, ref p);
        for (var i = 0; i < scopeCount && _recordBudget > 0; i++)
        {
            var scopeOffset = (int)DecodeUnsigned(span, ref p);
            ReadScope(scopeOffset);
        }
    }

    private void ReadScope(int offset)
    {
        var span = _blob.Span;
        var p = offset;
        DecodeUnsigned(span, ref p);          // Flags
        var nameOffset = (int)DecodeUnsigned(span, ref p);  // Name (the assembly simple name)
        DecodeUnsigned(span, ref p);          // HashAlgorithm
        DecodeUnsigned(span, ref p);          // MajorVersion
        DecodeUnsigned(span, ref p);          // MinorVersion
        DecodeUnsigned(span, ref p);          // BuildNumber
        DecodeUnsigned(span, ref p);          // RevisionNumber
        SkipByteCollection(span, ref p);      // PublicKey
        DecodeUnsigned(span, ref p);          // Culture
        var rootNamespace = (int)DecodeUnsigned(span, ref p);

        var assemblyName = ReadString(nameOffset);
        ReadNamespace(rootNamespace, parentNamespace: "", assemblyName, depth: 0);
    }

    private void ReadNamespace(int offset, string parentNamespace, string? assemblyName, int depth)
    {
        if (depth > MaxDepth || _recordBudget-- <= 0) return;

        var span = _blob.Span;
        var p = offset;
        DecodeUnsigned(span, ref p);          // ParentScopeOrNamespace (generic handle, discarded)
        var nameOffset = (int)DecodeUnsigned(span, ref p);
        var typeDefs = ReadHandleList(span, ref p);
        SkipHandleList(span, ref p);          // TypeForwarders
        var childNamespaces = ReadHandleList(span, ref p);

        var name = ReadString(nameOffset);
        var fullNamespace = name.Length == 0
            ? parentNamespace
            : parentNamespace.Length == 0 ? name : $"{parentNamespace}.{name}";

        foreach (var typeOffset in typeDefs)
            ReadType(typeOffset, fullNamespace, enclosingName: null, assemblyName, depth + 1);

        foreach (var childOffset in childNamespaces)
            ReadNamespace(childOffset, fullNamespace, assemblyName, depth + 1);
    }

    private void ReadType(int offset, string namespaceName, string? enclosingName, string? assemblyName, int depth)
    {
        if (depth > MaxDepth || _recordBudget-- <= 0) return;

        var span = _blob.Span;
        var p = offset;
        DecodeUnsigned(span, ref p);          // Flags
        DecodeUnsigned(span, ref p);          // BaseType (generic handle, discarded)
        DecodeUnsigned(span, ref p);          // NamespaceDefinition (tracked via the walk)
        var nameOffset = (int)DecodeUnsigned(span, ref p);
        DecodeUnsigned(span, ref p);          // Size
        DecodeUnsigned(span, ref p);          // PackingSize
        DecodeUnsigned(span, ref p);          // EnclosingType (tracked via the walk)
        var nestedTypes = ReadHandleList(span, ref p);
        var methods = ReadHandleList(span, ref p);

        var name = ReadString(nameOffset);
        var fullName = enclosingName is not null
            ? $"{enclosingName}+{name}"
            : namespaceName.Length == 0 ? name : $"{namespaceName}.{name}";

        var methodNames = new List<string>(methods.Count);
        foreach (var methodOffset in methods)
        {
            var methodName = ReadMethodName(methodOffset);
            if (methodName.Length > 0) methodNames.Add(methodName);
        }

        _types.Add(new RecoveredType(fullName, methodNames, assemblyName));

        foreach (var nestedOffset in nestedTypes)
            ReadType(nestedOffset, namespaceName, fullName, assemblyName, depth + 1);
    }

    private string ReadMethodName(int offset)
    {
        var span = _blob.Span;
        var p = offset;
        DecodeUnsigned(span, ref p);          // Flags
        DecodeUnsigned(span, ref p);          // ImplFlags
        var nameOffset = (int)DecodeUnsigned(span, ref p);
        return ReadString(nameOffset);
    }

    /// <summary>Reads a ConstantStringValue: a length-prefixed UTF-8 string at the offset.</summary>
    private string ReadString(int offset)
    {
        if (offset <= 0 || offset >= _blob.Length) return "";
        var span = _blob.Span;
        var p = offset;
        var byteCount = (int)DecodeUnsigned(span, ref p);
        if (byteCount is <= 0 or > MaxStringLength || p + byteCount > span.Length) return "";
        return Encoding.UTF8.GetString(span.Slice(p, byteCount));
    }

    /// <summary>Reads a handle collection (count then that many typed offsets) into a list.</summary>
    private static List<int> ReadHandleList(ReadOnlySpan<byte> span, ref int p)
    {
        var count = (int)DecodeUnsigned(span, ref p);
        if (count is < 0 or > MaxRecords) return [];
        var list = new List<int>(Math.Min(count, 1024));
        for (var i = 0; i < count; i++)
            list.Add((int)DecodeUnsigned(span, ref p));
        return list;
    }

    /// <summary>Steps over a handle collection without materializing it.</summary>
    private static void SkipHandleList(ReadOnlySpan<byte> span, ref int p)
    {
        var count = (int)DecodeUnsigned(span, ref p);
        for (var i = 0; i < count; i++)
            DecodeUnsigned(span, ref p);
    }

    /// <summary>Steps over a byte collection (count then that many raw bytes).</summary>
    private static void SkipByteCollection(ReadOnlySpan<byte> span, ref int p)
    {
        var count = (int)DecodeUnsigned(span, ref p);
        p += count;
    }

    /// <summary>
    /// Decodes the NativeFormat variable-length unsigned integer at <paramref name="p"/> and
    /// advances it. The count of bytes is a unary prefix in the low bits of the first byte.
    /// </summary>
    private static uint DecodeUnsigned(ReadOnlySpan<byte> span, ref int p)
    {
        var b0 = span[p];
        if ((b0 & 1) == 0)
        {
            p += 1;
            return (uint)(b0 >> 1);
        }
        if ((b0 & 2) == 0)
        {
            var v = (uint)(b0 >> 2) | ((uint)span[p + 1] << 6);
            p += 2;
            return v;
        }
        if ((b0 & 4) == 0)
        {
            var v = (uint)(b0 >> 3) | ((uint)span[p + 1] << 5) | ((uint)span[p + 2] << 13);
            p += 3;
            return v;
        }
        if ((b0 & 8) == 0)
        {
            var v = (uint)(b0 >> 4) | ((uint)span[p + 1] << 4) | ((uint)span[p + 2] << 12)
                | ((uint)span[p + 3] << 20);
            p += 4;
            return v;
        }

        var full = BinaryPrimitives.ReadUInt32LittleEndian(span[(p + 1)..]);
        p += 5;
        return full;
    }
}
