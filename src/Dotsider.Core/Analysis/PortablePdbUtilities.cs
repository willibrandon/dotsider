using System.IO.Compression;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

internal static class PortablePdbUtilities
{
    internal static readonly Guid EmbeddedSourceKind = new("0E8A571B-6926-466E-B4AD-8AB04611F5FE");
    internal static readonly Guid SourceLinkKind = new("CC110556-A091-4D38-9FEC-25AB9A351A6A");

    internal static SourceLinkInfo ReadSourceLink(MetadataReader? pdbReader)
    {
        if (pdbReader is null) return new SourceLinkInfo([]);

        foreach (var handle in pdbReader.CustomDebugInformation)
        {
            var info = pdbReader.GetCustomDebugInformation(handle);
            if (pdbReader.GetGuid(info.Kind) != SourceLinkKind)
                continue;

            try
            {
                var json = Encoding.UTF8.GetString(pdbReader.GetBlobBytes(info.Value));
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("documents", out var documents)
                    || documents.ValueKind != JsonValueKind.Object)
                    return new SourceLinkInfo([]);

                var mappings = new List<SourceLinkMapping>();
                foreach (var property in documents.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String
                        && property.Value.GetString() is { Length: > 0 } url)
                    {
                        mappings.Add(new SourceLinkMapping(property.Name, url));
                    }
                }

                return new SourceLinkInfo(mappings);
            }
            catch
            {
                return new SourceLinkInfo([]);
            }
        }

        return new SourceLinkInfo([]);
    }

    internal static string? ResolveSourceLinkUrl(SourceLinkInfo sourceLink, string? documentPath)
    {
        if (string.IsNullOrEmpty(documentPath) || sourceLink.Mappings.Count == 0)
            return null;

        var normalizedDocument = NormalizePath(documentPath);
        SourceLinkMapping? best = null;
        string? bestCapture = null;
        var bestSpecificity = -1;

        foreach (var mapping in sourceLink.Mappings)
        {
            if (!TryMatchSourceLinkPattern(
                    NormalizePath(mapping.DocumentPattern),
                    normalizedDocument,
                    out var capture,
                    out var specificity))
            {
                continue;
            }

            if (specificity > bestSpecificity)
            {
                best = mapping;
                bestCapture = capture;
                bestSpecificity = specificity;
            }
        }

        if (best is null) return null;
        return best.UrlTemplate.Contains('*', StringComparison.Ordinal)
            ? best.UrlTemplate.Replace("*", bestCapture ?? "", StringComparison.Ordinal)
            : best.UrlTemplate;
    }

    internal static EmbeddedSourceInfo? ReadEmbeddedSource(MetadataReader? pdbReader, string documentPath)
    {
        if (pdbReader is null) return null;

        foreach (var documentHandle in pdbReader.Documents)
        {
            var document = pdbReader.GetDocument(documentHandle);
            var currentPath = pdbReader.GetString(document.Name);
            if (!PathsEqual(currentPath, documentPath))
                continue;

            foreach (var customInfoHandle in pdbReader.GetCustomDebugInformation(documentHandle))
            {
                var customInfo = pdbReader.GetCustomDebugInformation(customInfoHandle);
                if (pdbReader.GetGuid(customInfo.Kind) != EmbeddedSourceKind)
                    continue;

                var blob = pdbReader.GetBlobBytes(customInfo.Value);
                var bytes = DecodeEmbeddedSourceBlob(blob);
                if (bytes is null) return null;
                return new EmbeddedSourceInfo(currentPath, DecodeSourceText(bytes), bytes);
            }
        }

        return null;
    }

    internal static bool HasEmbeddedSource(MetadataReader? pdbReader, string? documentPath)
    {
        if (pdbReader is null || string.IsNullOrEmpty(documentPath)) return false;

        foreach (var documentHandle in pdbReader.Documents)
        {
            var document = pdbReader.GetDocument(documentHandle);
            if (!PathsEqual(pdbReader.GetString(document.Name), documentPath))
                continue;

            foreach (var customInfoHandle in pdbReader.GetCustomDebugInformation(documentHandle))
            {
                var customInfo = pdbReader.GetCustomDebugInformation(customInfoHandle);
                if (pdbReader.GetGuid(customInfo.Kind) == EmbeddedSourceKind)
                    return true;
            }
        }

        return false;
    }

    internal static (Guid Guid, int Age)? TryReadPortablePdbId(MetadataReader pdbReader)
    {
        var header = pdbReader.DebugMetadataHeader;
        if (header is null || header.Id.Length < 20)
            return null;

        var id = header.Id;
        var guidBytes = new byte[16];
        for (var i = 0; i < guidBytes.Length; i++)
            guidBytes[i] = id[i];

        var age = BitConverter.ToInt32(id.AsSpan(16, 4));
        return (new Guid(guidBytes), age);
    }

    internal static bool PortablePdbIdMatches(MetadataReader pdbReader, Guid guid, int age)
    {
        var id = TryReadPortablePdbId(pdbReader);
        return id is not null && id.Value.Guid == guid && id.Value.Age == age;
    }

    private static byte[]? DecodeEmbeddedSourceBlob(byte[] blob)
    {
        if (blob.Length < 4) return null;

        var uncompressedSize = BitConverter.ToInt32(blob, 0);
        var payload = blob.AsSpan(4).ToArray();
        if (uncompressedSize == 0)
            return payload;

        if (uncompressedSize < 0) return null;

        using var input = new MemoryStream(payload);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(uncompressedSize);
        deflate.CopyTo(output);
        var decoded = output.ToArray();
        return decoded.Length == uncompressedSize ? decoded : null;
    }

    private static string DecodeSourceText(byte[] bytes)
    {
        if (bytes.Length >= 3
            && bytes[0] == 0xEF
            && bytes[1] == 0xBB
            && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static bool TryMatchSourceLinkPattern(
        string pattern,
        string document,
        out string capture,
        out int specificity)
    {
        capture = "";
        specificity = -1;

        var starIndex = pattern.IndexOf('*', StringComparison.Ordinal);
        if (starIndex < 0)
        {
            if (!string.Equals(pattern, document, StringComparison.OrdinalIgnoreCase))
                return false;

            specificity = pattern.Length;
            return true;
        }

        var prefix = pattern[..starIndex];
        var suffix = pattern[(starIndex + 1)..];
        if (!document.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !document.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            || document.Length < prefix.Length + suffix.Length)
        {
            return false;
        }

        capture = document[prefix.Length..(document.Length - suffix.Length)];
        specificity = prefix.Length + suffix.Length;
        return true;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);

    private static string NormalizePath(string path) => path.Replace('\\', '/');
}
