using Dotsider.Core.Analysis.Disasm;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="NativeSymbolName.Parse"/>: splitting a recovered managed name into
/// namespace, declaring type, and member — signature-aware and generic-aware.
/// </summary>
public class NativeSymbolNameTests
{
    /// <summary>Verifies namespace/type/member splitting across the common managed-name shapes.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData("System.Text.StringBuilder.Append(char)", "System.Text", "StringBuilder", "Append(char)")]
    [InlineData("Program.<Main>$(System.String[])", "", "Program", "<Main>$(System.String[])")]
    [InlineData("A.B", "", "A", "B")]
    [InlineData("Foo", "", "", "Foo")]
    [InlineData("N.T.M", "N", "T", "M")]
    public void Parse_SplitsNamespaceTypeMember(string input, string ns, string type, string member)
    {
        var parsed = NativeSymbolName.Parse(input);
        Assert.Equal(ns, parsed.Namespace);
        Assert.Equal(type, parsed.TypeName);
        Assert.Equal(member, parsed.MemberName);
    }
}
