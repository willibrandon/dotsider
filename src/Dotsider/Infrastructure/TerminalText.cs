using System.Globalization;
using System.Text;

namespace Dotsider.Infrastructure;

/// <summary>
/// Converts untrusted text to a printable terminal representation without changing the source
/// value used for identity or application behavior.
/// </summary>
internal static class TerminalText
{
    /// <summary>
    /// Replaces terminal controls, Unicode formatting controls, and invalid surrogate code units
    /// with visible text while preserving ordinary Unicode characters.
    /// </summary>
    /// <param name="value">The untrusted text.</param>
    /// <returns>The original string when it is printable; otherwise, a printable representation.</returns>
    internal static string Escape(string value) => EscapeCore(value, allowLineFeeds: false);

    /// <summary>
    /// Replaces unsafe characters while preserving logical line boundaries as line feeds.
    /// Carriage-return and carriage-return/line-feed boundaries are normalized to line feeds.
    /// </summary>
    /// <param name="value">The untrusted multiline text.</param>
    /// <returns>The terminal-safe multiline representation.</returns>
    internal static string EscapeMultiline(string value) => EscapeCore(value, allowLineFeeds: true);

    /// <summary>
    /// Truncates printable text without splitting a UTF-16 surrogate pair and appends an ellipsis
    /// when truncation is required.
    /// </summary>
    /// <param name="value">The printable text.</param>
    /// <param name="maximumLength">The maximum UTF-16 length, including the ellipsis.</param>
    /// <returns>The original text or its safely truncated representation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maximumLength"/> is less than the length of the ellipsis.
    /// </exception>
    internal static string TruncateWithEllipsis(string value, int maximumLength)
    {
        const string Ellipsis = "...";
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumLength, Ellipsis.Length);

        if (value.Length <= maximumLength)
        {
            return value;
        }

        var prefixLength = maximumLength - Ellipsis.Length;
        if (prefixLength > 0 &&
            char.IsHighSurrogate(value[prefixLength - 1]) &&
            char.IsLowSurrogate(value[prefixLength]))
        {
            prefixLength--;
        }

        return string.Concat(value.AsSpan(0, prefixLength), Ellipsis);
    }

    private static void AppendEscapedRune(StringBuilder builder, Rune rune)
    {
        if (rune.Value <= 0x1F)
        {
            builder.Append((char)(0x2400 + rune.Value));
        }
        else if (rune.Value == 0x7F)
        {
            builder.Append('\u2421');
        }
        else
        {
            AppendUnicodeEscape(builder, rune.Value);
        }
    }

    private static void AppendUnicodeEscape(StringBuilder builder, int value)
    {
        if (value <= ushort.MaxValue)
        {
            builder.Append("\\u");
            builder.Append(value.ToString("X4", CultureInfo.InvariantCulture));
        }
        else
        {
            builder.Append("\\U");
            builder.Append(value.ToString("X8", CultureInfo.InvariantCulture));
        }
    }

    private static StringBuilder CreateBuilder(string value, int safePrefixLength)
    {
        var builder = new StringBuilder(value.Length + 8);
        builder.Append(value.AsSpan(0, safePrefixLength));
        return builder;
    }

    private static string EscapeCore(string value, bool allowLineFeeds)
    {
        StringBuilder? builder = null;
        var index = 0;
        while (index < value.Length)
        {
            if (allowLineFeeds && value[index] == '\n')
            {
                builder?.Append('\n');
                index++;
                continue;
            }

            if (allowLineFeeds && value[index] == '\r')
            {
                builder ??= CreateBuilder(value, index);
                builder.Append('\n');
                index += index + 1 < value.Length && value[index + 1] == '\n' ? 2 : 1;
                continue;
            }

            var characterCount = 1;
            Rune rune;
            if (char.IsHighSurrogate(value[index]) &&
                index + 1 < value.Length &&
                char.IsLowSurrogate(value[index + 1]))
            {
                rune = new Rune(value[index], value[index + 1]);
                characterCount = 2;
            }
            else if (char.IsSurrogate(value[index]))
            {
                builder ??= CreateBuilder(value, index);
                AppendUnicodeEscape(builder, value[index]);
                index++;
                continue;
            }
            else
            {
                rune = new Rune(value[index]);
            }

            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or
                UnicodeCategory.Format or
                UnicodeCategory.LineSeparator or
                UnicodeCategory.ParagraphSeparator)
            {
                builder ??= CreateBuilder(value, index);
                AppendEscapedRune(builder, rune);
            }
            else if (builder is not null)
            {
                builder.Append(value.AsSpan(index, characterCount));
            }

            index += characterCount;
        }

        return builder?.ToString() ?? value;
    }
}
