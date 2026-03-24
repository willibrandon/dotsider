using System.Reflection.Metadata.Ecma335;
using System.Text;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Extracts strings from .NET assemblies across three sources:
/// the #US heap (user string literals), the #Strings heap (metadata identifiers),
/// and raw printable character sequences from the binary.
/// </summary>
public sealed class StringExtractor(AssemblyAnalyzer analyzer)
{

    /// <summary>Number of malformed entries skipped during the last <see cref="ExtractUserStrings"/> call.</summary>
    public int SkippedUserStringCount { get; private set; }

    /// <summary>Number of malformed entries skipped during the last <see cref="ExtractMetadataStrings"/> call.</summary>
    public int SkippedMetadataStringCount { get; private set; }

    /// <summary>
    /// Extracts all user string literals from the #US metadata heap.
    /// These are the string constants used in IL code via <c>ldstr</c>.
    /// </summary>
    /// <returns>A list of string entries from the user strings heap.</returns>
    public IReadOnlyList<StringEntry> ExtractUserStrings()
    {
        var reader = analyzer.GetMetadataReader();
        if (reader is null) return [];

        SkippedUserStringCount = 0;
        var results = new List<StringEntry>();

        if (reader.GetHeapSize(HeapIndex.UserString) == 0) return results;

        var handle = MetadataTokens.UserStringHandle(1);

        while (!handle.IsNil)
        {
            var offset = MetadataTokens.GetHeapOffset(handle);
            try
            {
                var value = reader.GetUserString(handle);
                if (!string.IsNullOrEmpty(value))
                {
                    results.Add(new StringEntry(offset, value, StringSource.UserStrings));
                }
            }
            catch
            {
                SkippedUserStringCount++;
            }

            try
            {
                handle = reader.GetNextHandle(handle);
            }
            catch (BadImageFormatException)
            {
                break;
            }
        }

        return results;
    }

    /// <summary>
    /// Extracts all identifier strings from the #Strings metadata heap.
    /// These are type names, method names, namespace names, and other metadata identifiers.
    /// </summary>
    /// <returns>A list of string entries from the metadata strings heap.</returns>
    public IReadOnlyList<StringEntry> ExtractMetadataStrings()
    {
        var reader = analyzer.GetMetadataReader();
        if (reader is null) return [];

        SkippedMetadataStringCount = 0;
        var results = new List<StringEntry>();

        if (reader.GetHeapSize(HeapIndex.String) == 0) return results;

        var handle = MetadataTokens.StringHandle(1);

        while (!handle.IsNil)
        {
            var offset = MetadataTokens.GetHeapOffset(handle);
            try
            {
                var value = reader.GetString(handle);
                if (!string.IsNullOrEmpty(value))
                {
                    results.Add(new StringEntry(offset, value, StringSource.MetadataStrings));
                }
            }
            catch
            {
                SkippedMetadataStringCount++;
            }

            try
            {
                handle = reader.GetNextHandle(handle);
            }
            catch (BadImageFormatException)
            {
                break;
            }
        }

        return results;
    }

    /// <summary>
    /// Extracts raw printable character sequences from the binary file.
    /// Scans for consecutive ASCII printable characters (0x20-0x7E) of at least <paramref name="minLength"/> bytes.
    /// </summary>
    /// <param name="minLength">The minimum number of consecutive printable characters to consider a string.</param>
    /// <returns>A list of string entries extracted from the raw binary.</returns>
    public IReadOnlyList<StringEntry> ExtractRawStrings(int minLength = 4)
    {
        var bytes = analyzer.RawBytes.Span;
        var results = new List<StringEntry>();
        var sb = new StringBuilder();
        var startOffset = -1;

        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            if (b is >= 0x20 and <= 0x7E)
            {
                if (startOffset < 0) startOffset = i;
                sb.Append((char)b);
            }
            else
            {
                if (sb.Length >= minLength)
                {
                    results.Add(new StringEntry(startOffset, sb.ToString(), StringSource.RawBinary));
                }

                sb.Clear();
                startOffset = -1;
            }
        }

        // Handle trailing string
        if (sb.Length >= minLength)
        {
            results.Add(new StringEntry(startOffset, sb.ToString(), StringSource.RawBinary));
        }

        return results;
    }
}
