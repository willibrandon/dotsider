using Dotsider.Core.Analysis.Disasm;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="NativeSymbolName.Parse"/>: splitting a recovered managed name into
/// namespace, declaring type, and member — signature-aware and generic-aware.
/// </summary>
[TestClass]
public class NativeSymbolNameTests
{
    /// <summary>Verifies namespace/type/member splitting across the common managed-name shapes.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("System.Text.StringBuilder.Append(char)", "System.Text", "StringBuilder", "Append(char)")]
    [DataRow("Program.<Main>$(System.String[])", "", "Program", "<Main>$(System.String[])")]
    [DataRow("A.B", "", "A", "B")]
    [DataRow("Foo", "", "", "Foo")]
    [DataRow("N.T.M", "N", "T", "M")]
    public void Parse_SplitsNamespaceTypeMember(string input, string ns, string type, string member)
    {
        var parsed = NativeSymbolName.Parse(input);
        Assert.AreEqual(ns, parsed.Namespace);
        Assert.AreEqual(type, parsed.TypeName);
        Assert.AreEqual(member, parsed.MemberName);
    }
}
