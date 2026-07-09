using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for NativeFormat metadata name recovery, exercised through
/// <see cref="AssemblyAnalyzer.RecoveredTypes"/>. The embedded metadata is file-backed on
/// every platform, so these run on all CI runners regardless of image format.
/// </summary>
[TestClass]
public class NativeMetadataReaderTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies the sample's own type and its entry-point method are recovered from the
    /// embedded metadata. Top-level statements compile to a <c>Program</c> type whose
    /// entry point is named <c>&lt;Main&gt;$</c>.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RecoveredTypes_NativeAotExe_NamesOwnProgramType()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);

        var types = analyzer.RecoveredTypes;

        Assert.IsNotEmpty(types);
        var program = Assert.ContainsSingle(t => t.FullName == "Program", types);
        Assert.Contains("<Main>$", program.MethodNames);
    }

    /// <summary>
    /// Verifies framework types are recovered with namespace qualification, nested types
    /// use <c>+</c>, and every recovered name is well-formed.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RecoveredTypes_NativeAotExe_IncludesNamespaceQualifiedFrameworkTypes()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);

        var types = analyzer.RecoveredTypes;

        var systemObject = Assert.ContainsSingle(t => t.FullName == "System.Object", types);
        Assert.Contains(".ctor", systemObject.MethodNames);
        Assert.Contains(t => t.FullName == "System.String", types);
        Assert.Contains(t => t.FullName.Contains('+', StringComparison.Ordinal), types); // a nested type

        // Every recovered name is non-empty and every method name is non-empty.
        TestAssert.All(types, t =>
        {
            Assert.IsFalse(string.IsNullOrEmpty(t.FullName));
            TestAssert.All(t.MethodNames, m => Assert.IsFalse(string.IsNullOrEmpty(m)));
        });
    }

    /// <summary>
    /// Verifies a managed assembly recovers no NativeFormat types (it has no AOT metadata).
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RecoveredTypes_ManagedDll_Empty()
    {
        using var analyzer = new AssemblyAnalyzer(Samples.RichLibraryDll);

        Assert.IsEmpty(analyzer.RecoveredTypes);
    }

    /// <summary>
    /// Verifies recovered types carry their defining assembly scope name — framework types
    /// resolve to <c>System.Private.CoreLib</c> and the app's own type to the app assembly —
    /// which native symbol demangling joins against.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RecoveredTypes_NativeAotExe_CarryAssemblyScopeNames()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);

        var types = analyzer.RecoveredTypes;

        var systemObject = Assert.ContainsSingle(t => t.FullName == "System.Object", types);
        Assert.AreEqual("System.Private.CoreLib", systemObject.AssemblyName);
        var program = Assert.ContainsSingle(t => t.FullName == "Program", types);
        Assert.AreEqual("NativeAotConsole", program.AssemblyName);
    }

    /// <summary>
    /// Verifies the explicit two-value deconstruction still compiles and yields the type name
    /// and its methods, unaffected by the added assembly-scope parameter.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RecoveredType_TwoValueDeconstruction_Preserved()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);

        var (fullName, methodNames) = analyzer.RecoveredTypes.First(t => t.FullName == "Program");

        Assert.AreEqual("Program", fullName);
        Assert.Contains("<Main>$", methodNames);
    }
}
