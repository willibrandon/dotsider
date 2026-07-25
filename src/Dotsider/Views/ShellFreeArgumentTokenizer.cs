using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Dotsider.Views;

internal static class ShellFreeArgumentTokenizer
{
    private static readonly SearchValues<char> s_forbiddenControlCharacters =
        SearchValues.Create(['\0', '\r', '\n']);

    private static readonly SearchValues<char> s_unsupportedShellOperators =
        SearchValues.Create(['&', ';', '<', '>', '`', '|']);

    /// <summary>
    /// Formats literal arguments using the tokenizer's shell-free quote and escape rules.
    /// </summary>
    /// <param name="arguments">The arguments to format.</param>
    /// <returns>Text that round-trips through <see cref="TryTokenize"/>.</returns>
    internal static string Format(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var builder = new StringBuilder();
        for (var argumentIndex = 0; argumentIndex < arguments.Count; argumentIndex++)
        {
            var argument = arguments[argumentIndex]
                ?? throw new ArgumentException(
                    "Arguments cannot contain null values.",
                    nameof(arguments));
            if (argumentIndex > 0)
            {
                builder.Append(' ');
            }

            var requiresQuotes = argument.Length == 0
                || argument.Any(static character =>
                    char.IsWhiteSpace(character)
                    || character is '\'' or '"' or '\\');
            if (!requiresQuotes)
            {
                builder.Append(argument);
                continue;
            }

            builder.Append('"');
            foreach (var character in argument)
            {
                if (character is '"' or '\\')
                {
                    builder.Append('\\');
                }

                builder.Append(character);
            }

            builder.Append('"');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Tokenizes a command line without invoking or emulating a command shell.
    /// </summary>
    /// <param name="value">The text to tokenize.</param>
    /// <param name="rejectShellOperators">
    /// Whether unquoted shell operators are rejected as configuration mistakes.
    /// </param>
    /// <param name="arguments">The literal argument tokens when parsing succeeds.</param>
    /// <returns><see langword="true"/> when the text is well-formed.</returns>
    internal static bool TryTokenize(
        string? value,
        bool rejectShellOperators,
        [NotNullWhen(true)] out string[]? arguments)
    {
        arguments = null;
        if (value is null || value.AsSpan().ContainsAny(s_forbiddenControlCharacters))
        {
            return false;
        }

        var tokens = new List<string>();
        var builder = new StringBuilder();
        var quote = '\0';
        var tokenStarted = false;

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (quote == '\0' && char.IsWhiteSpace(character))
            {
                AddToken(tokens, builder, ref tokenStarted);
                continue;
            }

            if (character is '\'' or '"')
            {
                if (quote == '\0')
                {
                    quote = character;
                    tokenStarted = true;
                    continue;
                }

                if (quote == character)
                {
                    quote = '\0';
                    continue;
                }
            }

            if (character == '\\')
            {
                if (++index >= value.Length)
                {
                    return false;
                }

                var escaped = value[index];
                var escapesQuote = quote == '\0'
                    ? escaped is '\'' or '"'
                    : escaped == quote;
                if (char.IsWhiteSpace(escaped)
                    || escaped == '\\'
                    || escapesQuote)
                {
                    builder.Append(escaped);
                }
                else
                {
                    builder.Append('\\');
                    builder.Append(escaped);
                }

                tokenStarted = true;
                continue;
            }

            if (rejectShellOperators
                && quote == '\0'
                && s_unsupportedShellOperators.Contains(character))
            {
                return false;
            }

            if (rejectShellOperators
                && quote == '\0'
                && character == '$'
                && index + 1 < value.Length
                && value[index + 1] is '(' or '{')
            {
                return false;
            }

            builder.Append(character);
            tokenStarted = true;
        }

        if (quote != '\0')
        {
            return false;
        }

        AddToken(tokens, builder, ref tokenStarted);
        arguments = [.. tokens];
        return true;
    }

    private static void AddToken(
        List<string> tokens,
        StringBuilder builder,
        ref bool tokenStarted)
    {
        if (!tokenStarted)
        {
            return;
        }

        tokens.Add(builder.ToString());
        builder.Clear();
        tokenStarted = false;
    }
}
