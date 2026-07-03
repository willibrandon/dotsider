namespace Dotsider.Core.Analysis.Disasm;

/// <summary>
/// Splits a recovered managed name (as joined by the native symbol reader, e.g.
/// <c>System.Text.StringBuilder.Append(char)</c>) into its namespace, declaring type, and member,
/// so the native IL-inspector tree can bucket functions the same namespace → type → method way the
/// managed tree does. The parse is signature-aware (it ignores the parameter list) and handles
/// nested types (<c>+</c>) and generic arity markers.
/// </summary>
/// <param name="Namespace">The namespace, or empty for the global namespace.</param>
/// <param name="TypeName">The declaring type (with any nested-type chain), or empty when absent.</param>
/// <param name="MemberName">The member name with its signature, or the whole name when it has no type qualifier.</param>
public readonly record struct NativeSymbolName(string Namespace, string TypeName, string MemberName)
{
    /// <summary>Parses a managed name into namespace, type, and member.</summary>
    /// <param name="managedName">The joined managed name.</param>
    public static NativeSymbolName Parse(string managedName)
    {
        if (string.IsNullOrEmpty(managedName))
            return new NativeSymbolName(string.Empty, string.Empty, string.Empty);

        // Separate the signature (parameter list) from the qualified member path so dots inside
        // parameter types do not confuse the split.
        var paren = IndexOfSignature(managedName);
        var head = paren < 0 ? managedName : managedName[..paren];
        var signature = paren < 0 ? string.Empty : managedName[paren..];

        var lastDot = LastTopLevelDot(head);
        if (lastDot < 0)
            return new NativeSymbolName(string.Empty, string.Empty, managedName);

        var member = head[(lastDot + 1)..] + signature;
        var typePath = head[..lastDot];

        var typeDot = LastTopLevelDot(typePath);
        if (typeDot < 0)
            return new NativeSymbolName(string.Empty, typePath, member);

        return new NativeSymbolName(typePath[..typeDot], typePath[(typeDot + 1)..], member);
    }

    private static int IndexOfSignature(string name)
    {
        var depth = 0;
        for (var i = 0; i < name.Length; i++)
        {
            switch (name[i])
            {
                case '<' or '[': depth++; break;
                case '>' or ']': depth--; break;
                case '(' when depth == 0: return i;
            }
        }

        return -1;
    }

    // The last '.' that is not nested inside generic brackets — the boundary before the member/type.
    private static int LastTopLevelDot(string name)
    {
        var depth = 0;
        for (var i = name.Length - 1; i >= 0; i--)
        {
            switch (name[i])
            {
                case '>' or ']': depth++; break;
                case '<' or '[': depth--; break;
                case '.' when depth == 0: return i;
            }
        }

        return -1;
    }
}
