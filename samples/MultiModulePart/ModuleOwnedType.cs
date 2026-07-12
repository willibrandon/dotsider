namespace MultiModuleFixture;

/// <summary>
/// Provides members emitted into a compiler-built metadata module.
/// </summary>
public sealed class ModuleOwnedType
{
    /// <summary>
    /// Identifies the compiler-built module fixture.
    /// </summary>
    public const string Kind = "CompilerBuilt";

    /// <summary>
    /// Adds two values.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The sum of <paramref name="left"/> and <paramref name="right"/>.</returns>
    public static int Add(int left, int right) => left + right;
}
