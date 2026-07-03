using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="IlcNameDemangler"/> — the join from ILC-mangled native symbol names back
/// to managed names, and the classification of compiler-generated symbols. Cases are pinned to
/// real symbol spellings observed in the NativeAotConsole fixture PDB.
/// </summary>
public class IlcNameDemanglerTests
{
    private static IlcNameDemangler Build(params RecoveredType[] types) => new(types);

    /// <summary>
    /// Verifies the top-level-statement entry point joins exactly: type <c>Program</c> in
    /// assembly <c>NativeAotConsole</c>, method <c>&lt;Main&gt;$</c> → symbol
    /// <c>NativeAotConsole_Program___Main__</c>.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Demangle_EntryPoint_JoinsExactly()
    {
        var d = Build(new RecoveredType("Program", ["<Main>$"], "NativeAotConsole"));

        var result = d.Demangle("NativeAotConsole_Program___Main__");

        Assert.Equal("Program.<Main>$", result.ManagedName);
        Assert.Equal(NativeSymbolKind.Function, result.Kind);
        Assert.True(result.IsExactMatch);
    }

    /// <summary>
    /// Verifies a framework method with the <c>S.P.</c> assembly abbreviation and a generic
    /// instantiation scope joins after the scope is stripped:
    /// <c>S_P_CoreLib_System_ReadOnlySpan_1&lt;Char&gt;__ToString</c>.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Demangle_GenericInstanceMethod_JoinsAfterScopeStrip()
    {
        var d = Build(new RecoveredType("System.ReadOnlySpan`1", ["ToString"], "System.Private.CoreLib"));

        var result = d.Demangle("S_P_CoreLib_System_ReadOnlySpan_1<Char>__ToString");

        Assert.Equal("System.ReadOnlySpan`1.ToString", result.ManagedName);
        Assert.True(result.IsExactMatch);
    }

    /// <summary>
    /// Verifies a Windows vtable symbol classifies as a MethodTable and names its type.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Demangle_WindowsVtable_ClassifiesAsMethodTable()
    {
        var d = Build(new RecoveredType("System.Collections.Generic.StringEqualityComparer", [], "System.Private.CoreLib"));

        var result = d.Demangle("??_7S_P_CoreLib_System_Collections_Generic_StringEqualityComparer@@6B@");

        Assert.Equal(NativeSymbolKind.MethodTable, result.Kind);
        Assert.Equal("System.Collections.Generic.StringEqualityComparer (MethodTable)", result.ManagedName);
        Assert.True(result.IsExactMatch);
    }

    /// <summary>
    /// Verifies a Unix vtable symbol strips its Itanium decimal length prefix and classifies as
    /// a MethodTable.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Demangle_UnixVtable_StripsLengthPrefix()
    {
        var d = Build(new RecoveredType("System.Object", [".ctor"], "System.Private.CoreLib"));

        var result = d.Demangle("_ZTV20S_P_CoreLib_System_Object");

        Assert.Equal(NativeSymbolKind.MethodTable, result.Kind);
        Assert.Equal("System.Object (MethodTable)", result.ManagedName);
    }

    /// <summary>
    /// Verifies the node-level data and stub prefixes classify without needing a metadata join.
    /// </summary>
    [Theory(Timeout = 30_000)]
    [InlineData("?__GCSTATICS@S_P_CoreLib_System_Text_EncoderReplacementFallback@@", NativeSymbolKind.Statics)]
    [InlineData("__NONGCSTATICS_SomeType", NativeSymbolKind.Statics)]
    [InlineData("__TypeThreadStaticIndex_SomeType", NativeSymbolKind.Statics)]
    [InlineData("__GenericDict_S_P_CoreLib_System_Array__Resize", NativeSymbolKind.GenericDictionary)]
    [InlineData("__writableDataString", NativeSymbolKind.Data)]
    [InlineData("__readonlydata_SomeMethod", NativeSymbolKind.Data)]
    [InlineData("_MyModule__Str_48656C6C6F", NativeSymbolKind.FrozenObject)]
    [InlineData("__unbox_SomeType", NativeSymbolKind.Stub)]
    public void Demangle_NodePrefixes_Classify(string symbol, NativeSymbolKind expected)
    {
        var result = Build().Demangle(symbol);

        Assert.Equal(expected, result.Kind);
    }

    /// <summary>
    /// Verifies a nested type (compiler <c>&lt;&gt;c</c> display class) is underscore-joined, not
    /// angle-bracketed: <c>&lt;&gt;c</c> sanitizes to <c>__c</c> and joins as a nested type.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Demangle_NestedDisplayClass_UnderscoreJoined()
    {
        var d = Build(new RecoveredType("System.Foo+<>c", ["<Bar>b__0_0"], "System.Private.CoreLib"));

        var result = d.Demangle("S_P_CoreLib_System_Foo___c___Bar_b__0_0");

        Assert.Equal("System.Foo+<>c.<Bar>b__0_0", result.ManagedName);
        Assert.True(result.IsExactMatch);
    }

    /// <summary>
    /// Verifies a known type whose method is absent from sparse metadata claims no managed
    /// name — heuristics never populate <c>ManagedName</c>; the raw name stays the display.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Demangle_KnownTypeUnknownMethod_ClaimsNoManagedName()
    {
        var d = Build(new RecoveredType("System.String", [], "System.Private.CoreLib"));

        var result = d.Demangle("S_P_CoreLib_System_String__SomeMissingMethod");

        Assert.Null(result.ManagedName);
        Assert.False(result.IsExactMatch);
    }

    /// <summary>
    /// Verifies overloads sharing a method name never yield an exact match for the unsuffixed
    /// symbol: the shared name is kept as the display, but no signature can say which overload
    /// the symbol is.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Demangle_DuplicateMethodName_SharedNameNeverExact()
    {
        var d = Build(new RecoveredType("System.Foo", ["Bar", "Bar"], "System.Private.CoreLib"));

        var result = d.Demangle("S_P_CoreLib_System_Foo__Bar");

        Assert.Equal("System.Foo.Bar", result.ManagedName);
        Assert.False(result.IsExactMatch);
    }

    /// <summary>
    /// Verifies an overload disambiguation suffix (<c>_0</c>) that metadata cannot distinguish
    /// resolves to the base method name, marked non-exact.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Demangle_OverloadSuffix_ResolvesNonExact()
    {
        var d = Build(new RecoveredType("System.Foo", ["Bar"], "System.Private.CoreLib"));

        var result = d.Demangle("S_P_CoreLib_System_Foo__Bar_0");

        Assert.Equal("System.Foo.Bar", result.ManagedName);
        Assert.False(result.IsExactMatch);
    }

    /// <summary>
    /// Verifies a name colliding after sanitization is not claimed as an exact match: two
    /// distinct methods that sanitize to the same key mark the key ambiguous.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Demangle_SanitizationCollision_NotExact()
    {
        // "op.Add" and "op+Add" both sanitize to "op_Add".
        var d = Build(new RecoveredType("T", ["op.Add", "op+Add"], "App"));

        var result = d.Demangle("App_T__op_Add");

        // Ambiguous key → no confident managed name via the exact path.
        Assert.False(result.IsExactMatch);
    }

    /// <summary>
    /// Verifies an unrecognized symbol yields no managed name but is not misclassified.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Demangle_Unknown_YieldsNoManagedName()
    {
        var result = Build().Demangle("Totally_Unknown_Symbol__Xyz");

        Assert.Null(result.ManagedName);
        Assert.False(result.IsExactMatch);
    }

    /// <summary>
    /// Verifies the sanitizer matches ILC's rules: letters/underscore pass, a leading digit is
    /// prefixed, and every other character (including a multibyte codepoint) becomes one <c>_</c>.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Sanitize_MatchesIlcRules()
    {
        Assert.Equal("Foo_Bar", IlcNameDemangler.Sanitize("Foo.Bar"));
        Assert.Equal("_1Type", IlcNameDemangler.Sanitize("1Type"));
        Assert.Equal("List_1", IlcNameDemangler.Sanitize("List`1"));
        Assert.Equal("_Main__", IlcNameDemangler.Sanitize("<Main>$"));
        Assert.Equal("a_b", IlcNameDemangler.Sanitize("aéb")); // é → one underscore
        Assert.Equal("x_y", IlcNameDemangler.Sanitize("x\U0001F600y")); // emoji (surrogate pair) → one underscore
    }
}
