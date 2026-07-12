using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Tests for assembly resolution logic including app-local, shared framework,
/// bundle-backed, and type-forwarder resolution paths.
/// </summary>
[TestClass]
public sealed class AssemblyResolutionTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>Clears resolution caches after each test.</summary>
    public void Dispose()
    {
        ImplementationAssemblyResolver.ClearCache();
        DotNetRuntimeLocator.ClearCache();
    }

    /// <summary>Verifies that an app-local assembly resolves as FromFile before other probes.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ResolveAssembly_AppLocal_StillPreferred()
    {
        // HelloWorld.dll sits next to HelloWorld.exe — resolving "HelloWorld" from
        // the exe's directory should find the .dll app-locally
        var resolved = AssemblyAnalyzer.ResolveAssembly(
            Samples.HelloWorldExe, "HelloWorld");
        Assert.IsNotNull(resolved);
        var fromFile = Assert.IsExactInstanceOfType<ResolvedAssembly.FromFile>(resolved);
        Assert.EndsWith("HelloWorld.dll", fromFile.Path, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that System.Runtime resolves from the shared framework.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ResolveAssembly_FromSharedFramework_ReturnsFromFile()
    {
        // System.Runtime should be found in the shared framework
        var resolved = AssemblyAnalyzer.ResolveAssembly(
            Samples.RichLibraryDll, "System.Runtime",
            ".NETCoreApp,Version=v10.0", "Microsoft.NETCore.App");
        Assert.IsNotNull(resolved);
        Assert.IsExactInstanceOfType<ResolvedAssembly.FromFile>(resolved);
    }

    /// <summary>
    /// Verifies that System.Runtime resolves successfully when the referencing assembly
    /// has bundle context set. Under dotnet test the runtime dir probe (step 2) may
    /// succeed first; in a real single-file host the bundle probe (step 3) would win.
    /// Either path is correct — the key invariant is that resolution succeeds.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ResolveAssembly_WithBundleContext_FindsSystemRuntime()
    {
        Assert.IsNotNull(Samples.SelfContainedConsoleExe);
        var resolved = AssemblyAnalyzer.ResolveAssembly(
            "SelfContainedConsole.dll", "System.Runtime",
            sourceBundlePath: Samples.SelfContainedConsoleExe);
        Assert.IsNotNull(resolved);
    }

    /// <summary>Verifies that mscorlib type forwarders resolve correctly through a bundle.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImplementationAssemblyResolver_WithBundle_ResolvesTypeForwarders()
    {
        Assert.IsNotNull(Samples.SelfContainedConsoleExe);
        // mscorlib type forwarders should work through bundle-backed resolution
        var resolved = ImplementationAssemblyResolver.Resolve(
            "SelfContainedConsole.dll", "mscorlib", "System.Console",
            ".NETCoreApp,Version=v10.0", "Microsoft.NETCore.App",
            Samples.SelfContainedConsoleExe);
        Assert.IsNotNull(resolved);
    }

    /// <summary>Verifies that target framework and preferred pack are threaded through resolution.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ResolveAssembly_PreferredRuntimePack_ThreadedThrough()
    {
        // Verify that target framework and preferred pack reach the locator
        var resolved = AssemblyAnalyzer.ResolveAssembly(
            Samples.RichLibraryDll, "System.Runtime",
            ".NETCoreApp,Version=v10.0", "Microsoft.NETCore.App");
        Assert.IsNotNull(resolved);
    }

    /// <summary>
    /// System.Collections.dll is a partial facade: it ships real IL for some types
    /// (LinkedList`1, BitArray, …) and forwards others to System.Private.CoreLib.
    /// Resolving a forwarded type must follow the ExportedType to the implementation,
    /// not stop at the facade just because it happens to carry usable metadata.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImplementationAssemblyResolver_PartialFacade_ForwardedType_LandsInImplementationAssembly()
    {
        var resolved = ImplementationAssemblyResolver.Resolve(
            Samples.HelloWorldDll, "System.Collections",
            "System.Collections.Generic.List`1",
            ".NETCoreApp,Version=v10.0", "Microsoft.NETCore.App");
        var fromFile = Assert.IsExactInstanceOfType<ResolvedAssembly.FromFile>(resolved);
        Assert.AreEqual("System.Private.CoreLib.dll", Path.GetFileName(fromFile.Path));
    }

    /// <summary>
    /// Verifies the runtime's nested ExportedType chain is followed to its implementation assembly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImplementationAssemblyResolver_RuntimeNestedForwarder_LandsInImplementationAssembly()
    {
        var resolved = ImplementationAssemblyResolver.Resolve(
            Samples.HelloWorldDll,
            "mscorlib",
            "System.Collections.Generic.List`1/Enumerator",
            ".NETCoreApp,Version=v10.0",
            "Microsoft.NETCore.App");

        var fromFile = Assert.IsExactInstanceOfType<ResolvedAssembly.FromFile>(resolved);
        Assert.AreEqual("System.Private.CoreLib.dll", Path.GetFileName(fromFile.Path));
    }

    /// <summary>
    /// Guardrail for the same partial facade: a type the facade actually owns as a
    /// TypeDef must stay in the facade, not be over-chased into CoreLib.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImplementationAssemblyResolver_PartialFacade_LocallyOwnedType_StaysInFacade()
    {
        var resolved = ImplementationAssemblyResolver.Resolve(
            Samples.HelloWorldDll, "System.Collections",
            "System.Collections.Generic.LinkedList`1",
            ".NETCoreApp,Version=v10.0", "Microsoft.NETCore.App");
        var fromFile = Assert.IsExactInstanceOfType<ResolvedAssembly.FromFile>(resolved);
        Assert.AreEqual("System.Collections.dll", Path.GetFileName(fromFile.Path));
    }

    /// <summary>
    /// When a forwarder names the declaring type but the chain cannot be completed
    /// (target assembly absent, cycle, or chain ends without an owning TypeDef),
    /// the resolver must return null rather than falling back to the facade itself.
    /// Handing a callers a non-owning assembly recreates the "method not found"
    /// failure downstream — the whole point of the type-aware probe is to avoid it.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImplementationAssemblyResolver_ForwarderChaseBroken_ReturnsNullNotFacade()
    {
        // Build a synthetic facade that forwards "Sample.Forwarded" to an assembly
        // that does not exist anywhere on the probe path. Place it in an isolated
        // temp directory so AssemblyAnalyzer.ResolveAssembly finds it app-local
        // (step 1 of probing) but cannot find "NonExistent.Target".
        var dir = Path.Combine(Path.GetTempPath(), "dotsider-chase-broken-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var referencingPath = Path.Combine(dir, "Ref.dll");
            var facadePath = Path.Combine(dir, "SyntheticFacade.dll");
            // The referencing path only needs to exist on disk for Path.GetDirectoryName;
            // its contents are never read by this code path.
            File.WriteAllBytes(referencingPath, []);
            File.WriteAllBytes(facadePath, BuildSyntheticFacade(
                moduleName: "SyntheticFacade",
                forwarderTypeFullName: "Sample.Forwarded",
                targetAssemblyName: "NonExistent.Target"));

            var result = ImplementationAssemblyResolver.Resolve(
                referencingPath, "SyntheticFacade", "Sample.Forwarded");

            Assert.IsNull(result);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// Verifies an AssemblyRef terminal without IsTypeForwarder cannot be treated as a forwarder,
    /// even when the referenced assembly exists and owns the requested type.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImplementationAssemblyResolver_AssemblyReferenceWithoutForwarderFlag_HardMisses()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "dotsider-invalid-forwarder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var suffix = Guid.NewGuid().ToString("N");
            var facadeName = "InvalidForwarder" + suffix;
            var targetName = "InvalidForwarderTarget" + suffix;
            var facadePath = Path.Combine(directory, facadeName + ".dll");
            File.WriteAllBytes(
                facadePath,
                BuildSyntheticFacade(
                    facadeName,
                    "Synthetic.Target",
                    targetName,
                    TypeAttributes.Public));
            File.WriteAllBytes(
                Path.Combine(directory, targetName + ".dll"),
                MetadataNestingConsumerMetadata.BuildTargetAssembly(targetName));

            var result = ImplementationAssemblyResolver.Resolve(
                facadePath,
                facadeName,
                declaringType: "Synthetic.Target");

            Assert.IsNull(result);
        }
        finally
        {
            ImplementationAssemblyResolver.ClearCache();
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Emits a minimal PE with exactly one ExportedType forwarder pointing at the
    /// named target assembly. Used by the chase-broken regression to construct a
    /// forwarder whose target cannot be resolved.
    /// </summary>
    /// <param name="moduleName">The facade assembly and module name.</param>
    /// <param name="forwarderTypeFullName">The exported type's full name.</param>
    /// <param name="targetAssemblyName">The AssemblyRef target name.</param>
    /// <param name="attributes">The ExportedType attributes.</param>
    /// <returns>The serialized managed PE image.</returns>
    private static byte[] BuildSyntheticFacade(
        string moduleName,
        string forwarderTypeFullName,
        string targetAssemblyName,
        TypeAttributes attributes = TypeAttributes.Public | (TypeAttributes)0x0020_0000)
    {
        var metadata = new MetadataBuilder();
        metadata.AddAssembly(
            metadata.GetOrAddString(moduleName),
            new Version(1, 0, 0, 0),
            default, default,
            0, AssemblyHashAlgorithm.None);
        metadata.AddModule(
            0,
            metadata.GetOrAddString(moduleName + ".dll"),
            metadata.GetOrAddGuid(Guid.NewGuid()),
            default, default);
        metadata.AddTypeDefinition(
            default, default,
            metadata.GetOrAddString("<Module>"),
            baseType: default,
            fieldList: MetadataTokens.FieldDefinitionHandle(1),
            methodList: MetadataTokens.MethodDefinitionHandle(1));

        var asmRef = metadata.AddAssemblyReference(
            metadata.GetOrAddString(targetAssemblyName),
            new Version(1, 0, 0, 0),
            default, default, 0, default);

        var dot = forwarderTypeFullName.LastIndexOf('.');
        var ns = dot >= 0 ? forwarderTypeFullName[..dot] : string.Empty;
        var name = dot >= 0 ? forwarderTypeFullName[(dot + 1)..] : forwarderTypeFullName;

        metadata.AddExportedType(
            attributes,
            metadata.GetOrAddString(ns),
            metadata.GetOrAddString(name),
            implementation: asmRef,
            typeDefinitionId: 0);

        var pe = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll),
            new MetadataRootBuilder(metadata),
            ilStream: new BlobBuilder());
        var blob = new BlobBuilder();
        pe.Serialize(blob);
        return blob.ToArray();
    }

    /// <summary>
    /// Partial-facade forwarder resolution must also work through the bundle context
    /// path. Drives TryResolveFromBundle explicitly by passing sourceBundlePath.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImplementationAssemblyResolver_PartialFacade_Bundle_ForwardedType_LandsInImplementationAssembly()
    {
        Assert.IsNotNull(Samples.SelfContainedConsoleExe);
        var resolved = ImplementationAssemblyResolver.Resolve(
            "SelfContainedConsole.dll",
            "System.Collections",
            "System.Collections.Generic.List`1",
            ".NETCoreApp,Version=v10.0", "Microsoft.NETCore.App",
            Samples.SelfContainedConsoleExe);
        Assert.IsNotNull(resolved);
        var name = resolved switch
        {
            ResolvedAssembly.FromFile f => Path.GetFileName(f.Path),
            ResolvedAssembly.FromBundle b => b.Name,
            _ => null
        };
        Assert.AreEqual("System.Private.CoreLib.dll", name);
    }
}
