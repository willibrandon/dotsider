/// <summary>
/// Provides a compiler-produced LINQ call whose operand is a MethodSpec metadata token.
/// </summary>
internal static class MethodSpecReproFixture
{
    /// <summary>The fixture type name as it appears in assembly metadata.</summary>
    internal const string TypeName = nameof(MethodSpecReproFixture);

    /// <summary>The fixture method name as it appears in assembly metadata.</summary>
    internal const string MethodName = nameof(FilterLetters);

    /// <summary>The caller that instantiates a generic method defined in this assembly.</summary>
    internal const string MethodDefCallerName = nameof(CallLocalGeneric);

    /// <summary>The caller that instantiates a generic method on a constructed generic type.</summary>
    internal const string TypeSpecParentCallerName = nameof(ConvertAll);

    /// <summary>The expected human-readable displays for the method's three generic LINQ calls.</summary>
    internal static IReadOnlyList<string> ExpectedDisplays { get; } =
    [
        "System.Linq.Enumerable::Where<char>",
        "System.Linq.Enumerable::Select<char, char>",
        "System.Linq.Enumerable::OrderBy<char, char>"
    ];

    /// <summary>The expected display for the MethodDef-backed instantiation.</summary>
    internal const string MethodDefExpectedDisplay =
        "MethodSpecReproFixture::Identity<System.Collections.Generic.List`1<int>>";

    /// <summary>The expected display for the MemberRef whose parent is a TypeSpec.</summary>
    internal const string TypeSpecParentExpectedDisplay =
        "System.Collections.Generic.List`1<int>::ConvertAll<string>";

    /// <summary>Produces three real LINQ MethodSpec calls beginning with <c>Where&lt;char&gt;</c>.</summary>
    /// <param name="input">Characters to filter.</param>
    /// <returns>Normalized letter characters from <paramref name="input"/> in sorted order.</returns>
    internal static IEnumerable<char> FilterLetters(string input) => input
        .Where(char.IsLetter)
        .Select(static value => char.ToUpperInvariant(value))
        .OrderBy(static value => value);

    /// <summary>Instantiates a local generic method with a constructed generic argument.</summary>
    /// <param name="input">The value passed through the generic method.</param>
    /// <returns><paramref name="input"/> unchanged.</returns>
    internal static List<int> CallLocalGeneric(List<int> input) => Identity(input);

    /// <summary>Instantiates a generic method whose declaring type is <c>List&lt;int&gt;</c>.</summary>
    /// <param name="input">Values to convert.</param>
    /// <returns>The decimal representation of each input value.</returns>
    internal static List<string> ConvertAll(List<int> input) =>
        input.ConvertAll(static value => value.ToString());

    private static T Identity<T>(T value) => value;
}
