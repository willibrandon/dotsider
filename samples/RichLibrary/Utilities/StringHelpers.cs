using System.Security.Cryptography;
using System.Text;

namespace RichLibrary.Utilities;

/// <summary>
/// String utility methods showcasing various IL patterns.
/// </summary>
public static class StringHelpers
{
    /// <summary>
    /// Truncates a string to the specified max length, appending "..." if truncated.
    /// </summary>
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 3), "...");
    }

    /// <summary>
    /// Computes the SHA256 hash of a string.
    /// </summary>
    public static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Converts a string to title case using simple rules.
    /// </summary>
    public static string ToTitleCase(this string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;

        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = string.Concat(
                    char.ToUpperInvariant(words[i][0]).ToString(),
                    words[i].AsSpan(1).ToString().ToLowerInvariant());
            }
        }
        return string.Join(' ', words);
    }
}
