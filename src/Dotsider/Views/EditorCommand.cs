using System.Diagnostics.CodeAnalysis;

namespace Dotsider.Views;

/// <summary>
/// Represents a configured editor command parsed without shell evaluation.
/// </summary>
internal sealed class EditorCommand
{
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
            || !ShellFreeArgumentTokenizer.TryTokenize(
                value,
                rejectShellOperators: true,
                out var tokens))
        {
            return false;
        }

        if (tokens.Length == 0 || string.IsNullOrWhiteSpace(tokens[0]))
            return false;

        var arguments = new string[tokens.Length - 1];
        for (var index = 1; index < tokens.Length; index++)
            arguments[index - 1] = tokens[index];

        command = new EditorCommand(tokens[0], Array.AsReadOnly(arguments));
        return true;
    }
}
