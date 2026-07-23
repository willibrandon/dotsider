using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Dotsider.Views;

/// <summary>
/// Represents a configured editor command parsed without shell evaluation.
/// </summary>
internal sealed class EditorCommand
{
    private static readonly SearchValues<char> ForbiddenControlCharacters =
        SearchValues.Create(['\0', '\r', '\n']);

    private static readonly SearchValues<char> UnsupportedShellOperators =
        SearchValues.Create(['&', ';', '<', '>', '`', '|']);

    private EditorCommand(string executable, IReadOnlyList<string> arguments)
    {
        Executable = executable;
        Arguments = arguments;
    }

    /// <summary>
    /// Gets the configured executable token.
    /// </summary>
    internal string Executable { get; }

    /// <summary>
    /// Gets the configured literal arguments.
    /// </summary>
    internal IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Parses a shell-free editor command using documented quote and escape rules.
    /// </summary>
    /// <param name="value">The configured editor command.</param>
    /// <param name="command">The parsed command when successful.</param>
    /// <returns><see langword="true"/> when the command is well-formed.</returns>
    internal static bool TryParse(
        string? value,
        [NotNullWhen(true)] out EditorCommand? command)
    {
        command = null;
        if (string.IsNullOrWhiteSpace(value)
            || value.AsSpan().ContainsAny(ForbiddenControlCharacters))
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
                    return false;

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

            if (quote == '\0' && UnsupportedShellOperators.Contains(character))
                return false;
            if (quote == '\0'
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
            return false;

        AddToken(tokens, builder, ref tokenStarted);
        if (tokens.Count == 0 || string.IsNullOrWhiteSpace(tokens[0]))
            return false;

        var arguments = new string[tokens.Count - 1];
        for (var index = 1; index < tokens.Count; index++)
            arguments[index - 1] = tokens[index];

        command = new EditorCommand(tokens[0], Array.AsReadOnly(arguments));
        return true;
    }

    private static void AddToken(
        List<string> tokens,
        StringBuilder builder,
        ref bool tokenStarted)
    {
        if (!tokenStarted)
            return;

        tokens.Add(builder.ToString());
        builder.Clear();
        tokenStarted = false;
    }
}
