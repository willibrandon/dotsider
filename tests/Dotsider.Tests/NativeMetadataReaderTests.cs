using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for NativeFormat metadata name recovery, exercised through
/// <see cref="AssemblyAnalyzer.RecoveredTypes"/>. The embedded metadata is file-backed on
/// every platform, so these run on all CI runners regardless of image format.
/// </summary>
[Collection("SampleAssemblies")]
public class NativeMetadataReaderTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Verifies the sample's own type and its entry-point method are recovered from the
    /// embedded metadata. Top-level statements compile to a <c>Program</c> type whose
    /// entry point is named <c>&lt;Main&gt;$</c>.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RecoveredTypes_NativeAotExe_NamesOwnProgramType()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);

        var types = analyzer.RecoveredTypes;

        Assert.NotEmpty(types);
        var program = Assert.Single(types, t => t.FullName == "Program");
        Assert.Contains("<Main>$", program.MethodNames);
    }

    /// <summary>
    /// Verifies framework types are recovered with namespace qualification, nested types
    /// use <c>+</c>, and every recovered name is well-formed.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RecoveredTypes_NativeAotExe_IncludesNamespaceQualifiedFrameworkTypes()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);

        var types = analyzer.RecoveredTypes;

        var systemObject = Assert.Single(types, t => t.FullName == "System.Object");
        Assert.Contains(".ctor", systemObject.MethodNames);
        Assert.Contains(types, t => t.FullName == "System.String");
        Assert.Contains(types, t => t.FullName.Contains('+', StringComparison.Ordinal)); // a nested type

        // Every recovered name is non-empty and every method name is non-empty.
        Assert.All(types, t =>
        {
            Assert.False(string.IsNullOrEmpty(t.FullName));
            Assert.All(t.MethodNames, m => Assert.False(string.IsNullOrEmpty(m)));
        });
    }

    /// <summary>
    /// Verifies a managed assembly recovers no NativeFormat types (it has no AOT metadata).
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RecoveredTypes_ManagedDll_Empty()
    {
        using var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);

        Assert.Empty(analyzer.RecoveredTypes);
    }

    /// <summary>
    /// Verifies recovered types carry their defining assembly scope name — framework types
    /// resolve to <c>System.Private.CoreLib</c> and the app's own type to the app assembly —
    /// which native symbol demangling joins against.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RecoveredTypes_NativeAotExe_CarryAssemblyScopeNames()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);

        var types = analyzer.RecoveredTypes;

        var systemObject = Assert.Single(types, t => t.FullName == "System.Object");
        Assert.Equal("System.Private.CoreLib", systemObject.AssemblyName);
        var program = Assert.Single(types, t => t.FullName == "Program");
        Assert.Equal("NativeAotConsole", program.AssemblyName);
    }

    /// <summary>
    /// Verifies the explicit two-value deconstruction still compiles and yields the type name
    /// and its methods, unaffected by the added assembly-scope parameter.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RecoveredType_TwoValueDeconstruction_Preserved()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);

        var (fullName, methodNames) = analyzer.RecoveredTypes.First(t => t.FullName == "Program");

        Assert.Equal("Program", fullName);
        Assert.Contains("<Main>$", methodNames);
    }
}
