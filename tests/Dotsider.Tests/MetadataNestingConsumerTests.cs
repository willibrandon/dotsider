using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;

namespace Dotsider.Tests;

/// <summary>
/// Verifies the exact fail-closed fallback used by every metadata nesting-chain consumer.
/// </summary>
[TestClass]
public sealed class MetadataNestingConsumerTests
{
    /// <summary>
    /// Verifies AssemblyAnalyzer's TypeDef, TypeRef, and resolution-scope consumers use token and
    /// empty-identity fallbacks for cyclic chains.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AssemblyAnalyzer_CyclicChains_UseExactFallbacks()
    {
        using var analyzer = CreateCyclicAnalyzer();
        const int typeDefinitionToken = 0x02000002;
        const int typeReferenceToken = 0x01000001;

        Assert.AreEqual("0x02000002", analyzer.ResolveToken(typeDefinitionToken));
        Assert.AreEqual("0x01000001", analyzer.ResolveToken(typeReferenceToken));

        var typeDefinition = analyzer.TypeDefs.Single(type => type.Token == typeDefinitionToken);
        Assert.AreEqual("0x02000002", typeDefinition.FullName);

        var typeReference = analyzer.TypeRefs.Single(type => type.Token == typeReferenceToken);
        Assert.AreEqual("0x01000001", typeReference.FullName);
        Assert.AreEqual(string.Empty, typeReference.ResolutionScopeId);
    }

    /// <summary>
    /// Verifies ReadTypeDefs degrades an invalid TypeDef name handle to the exact token identity.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AssemblyAnalyzer_InvalidTypeDefinitionName_UsesTokenFallback()
    {
        using var analyzer = new AssemblyAnalyzer(
            MetadataNestingConsumerMetadata.BuildInvalidTypeDefinitionNameAssembly(),
            filePath: "InvalidTypeDefinitionName.dll");

        var definition = analyzer.TypeDefs.Single(type => type.Token == 0x02000001);

        Assert.AreEqual(string.Empty, definition.Namespace);
        Assert.AreEqual("0x02000001", definition.Name);
        Assert.AreEqual("0x02000001", definition.FullName);
        Assert.AreEqual(0, definition.MethodCount);
        Assert.AreEqual(0, definition.FieldCount);
    }

    /// <summary>
    /// Verifies the assembly signature provider uses deterministic token fallbacks for cyclic
    /// TypeDef and TypeRef chains.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AssemblySignatureTypeProvider_CyclicChains_UseTokenFallbacks()
    {
        using var analyzer = CreateCyclicAnalyzer();
        var reader = analyzer.GetMetadataReader();
        Assert.IsNotNull(reader);
        var provider = new AssemblySignatureTypeProvider();

        Assert.AreEqual(
            "0x02000002",
            provider.GetTypeFromDefinition(
                reader, MetadataTokens.TypeDefinitionHandle(2), rawTypeKind: 0));
        Assert.AreEqual(
            "0x01000001",
            provider.GetTypeFromReference(
                reader, MetadataTokens.TypeReferenceHandle(1), rawTypeKind: 0));
    }

    /// <summary>
    /// Verifies IL navigation exposes neither a partial declaring-type name nor a plausible assembly
    /// for a MemberRef whose TypeRef parent chain is cyclic.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IlNavigationResolver_CyclicTypeReference_UsesUnknownFallbacks()
    {
        using var analyzer = CreateCyclicAnalyzer();

        var target = IlNavigationResolver.Resolve(analyzer, 0x0A000001);

        var external = Assert.IsExactInstanceOfType<IlNavigationTarget.ExternalMethod>(target);
        Assert.AreEqual("Unknown", external.DeclaringType);
        Assert.AreEqual("Unknown", external.AssemblyName);
    }

    /// <summary>
    /// Verifies a structurally valid TypeSpec cannot turn a cyclic TypeRef provider fallback into
    /// a plausible local or external navigation target.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IlNavigationResolver_TypeSpecificationWithCyclicTypeReference_IsUnresolved()
    {
        using var analyzer = CreateCyclicAnalyzer();

        var typeTarget = IlNavigationResolver.Resolve(analyzer, 0x1B000001);
        var memberTarget = IlNavigationResolver.Resolve(analyzer, 0x0A000002);

        Assert.IsInstanceOfType<IlNavigationTarget.Unresolved>(typeTarget);
        Assert.IsInstanceOfType<IlNavigationTarget.Unresolved>(memberTarget);
    }

    /// <summary>
    /// Verifies mstat attribution returns the exact unknown attribution for a cyclic TypeRef chain.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void EntityResolver_CyclicTypeReference_UsesUnknownAttribution()
    {
        using var analyzer = CreateCyclicAnalyzer();
        var reader = analyzer.GetMetadataReader();
        Assert.IsNotNull(reader);
        var resolver = new EntityResolver(reader);

        var attribution = resolver.ResolveType(0x01000001);

        Assert.AreEqual(TypeAttribution.Unknown, attribution);
    }

    /// <summary>
    /// Verifies cyclic TypeDefs and ExportedTypes cannot block a later valid forwarder or produce a
    /// false local-ownership result.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImplementationAssemblyResolver_Cycles_DoNotBlockValidForwarder()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var suffix = Guid.NewGuid().ToString("N");
            var facadeName = "CyclicFacade" + suffix;
            var targetName = "ForwardTarget" + suffix;
            var facadePath = Path.Combine(directory, facadeName + ".dll");
            var targetPath = Path.Combine(directory, targetName + ".dll");
            File.WriteAllBytes(
                facadePath,
                MetadataNestingConsumerMetadata.BuildCyclicFacade(
                    facadeName, targetName, includeValidForwarder: true));
            File.WriteAllBytes(targetPath, MetadataNestingConsumerMetadata.BuildTargetAssembly(targetName));

            var resolved = ImplementationAssemblyResolver.Resolve(
                facadePath, facadeName, declaringType: "Synthetic.Target");

            var fromFile = Assert.IsExactInstanceOfType<ResolvedAssembly.FromFile>(resolved);
            Assert.AreEqual(Path.GetFullPath(targetPath), Path.GetFullPath(fromFile.Path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a cyclic ExportedType chain cannot be mistaken for a forwarder even when its unused
    /// AssemblyRef target is resolvable beside the facade.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImplementationAssemblyResolver_CyclicExportedType_HardMisses()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var suffix = Guid.NewGuid().ToString("N");
            var facadeName = "NonForwardFacade" + suffix;
            var targetName = "UnusedTarget" + suffix;
            var facadePath = Path.Combine(directory, facadeName + ".dll");
            var targetPath = Path.Combine(directory, targetName + ".dll");
            File.WriteAllBytes(
                facadePath,
                MetadataNestingConsumerMetadata.BuildCyclicFacade(
                    facadeName, targetName, includeValidForwarder: false));
            File.WriteAllBytes(targetPath, MetadataNestingConsumerMetadata.BuildTargetAssembly(targetName));

            var resolved = ImplementationAssemblyResolver.Resolve(
                facadePath, facadeName, declaringType: "Synthetic.CycleOuter/CycleInner");

            Assert.IsNull(resolved);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a facade cannot claim the same type through both a TypeDef and an ExportedType.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImplementationAssemblyResolver_TypeDefinitionAndExportedTypeConflict_HardMisses()
    {
        AssertAmbiguousFacadeHardMisses(includeTypeDefinition: true, exportedTypeCount: 1);
    }

    /// <summary>
    /// Verifies duplicate matching ExportedType rows cannot select an arbitrary forwarding owner.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImplementationAssemblyResolver_DuplicateExportedTypes_HardMisses()
    {
        AssertAmbiguousFacadeHardMisses(includeTypeDefinition: false, exportedTypeCount: 2);
    }

    /// <summary>
    /// Verifies an AssemblyFile terminal resolves only the authenticated sibling-module snapshot,
    /// and that the snapshot remains stable if the on-disk module is replaced afterward.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImplementationAssemblyResolver_AssemblyFile_ResolvesAuthenticatedModuleSnapshot()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var suffix = Guid.NewGuid().ToString("N");
            var assemblyName = "ModuleManifest" + suffix;
            const string moduleName = "Owned.netmodule";
            const string targetFramework = ".NETCoreApp,Version=v10.0";
            const string preferredRuntimePack = "Microsoft.NETCore.App";
            var manifestPath = Path.Combine(directory, assemblyName + ".dll");
            var modulePath = Path.Combine(directory, moduleName);
            var moduleBytes = MetadataNestingConsumerMetadata.BuildSiblingModule(moduleName);
            var manifestBytes = MetadataNestingConsumerMetadata.BuildSiblingModuleManifest(
                assemblyName,
                moduleName,
                moduleBytes,
                typeDefinitionId: int.MaxValue);
            File.WriteAllBytes(manifestPath, manifestBytes);
            File.WriteAllBytes(modulePath, moduleBytes);

            var resolved = ImplementationAssemblyResolver.Resolve(
                manifestPath,
                assemblyName,
                declaringType: "Synthetic.ModuleOwned",
                targetFramework: targetFramework,
                preferredRuntimePack: preferredRuntimePack);

            var module = Assert.IsExactInstanceOfType<ResolvedModule>(resolved);
            Assert.AreEqual(Path.GetFullPath(modulePath), Path.GetFullPath(module.Path));
            Assert.AreEqual(Path.GetFullPath(manifestPath), module.ManifestPath);
            Assert.AreSequenceEqual(moduleBytes, module.Bytes);
            Assert.AreEqual(targetFramework, module.TargetFramework);
            Assert.AreEqual(preferredRuntimePack, module.PreferredRuntimePack);

            var alternateContext = Assert.IsExactInstanceOfType<ResolvedModule>(
                ImplementationAssemblyResolver.Resolve(
                    manifestPath,
                    assemblyName,
                    declaringType: "Synthetic.ModuleOwned",
                    targetFramework: ".NETCoreApp,Version=v9.0",
                    preferredRuntimePack: "Alternate.Runtime.Pack"));
            Assert.AreEqual(".NETCoreApp,Version=v9.0", alternateContext.TargetFramework);
            Assert.AreEqual("Alternate.Runtime.Pack", alternateContext.PreferredRuntimePack);

            File.WriteAllBytes(
                modulePath,
                MetadataNestingConsumerMetadata.BuildSiblingModule(moduleName, typeName: "Replacement"));
            Assert.IsNull(ImplementationAssemblyResolver.Resolve(
                manifestPath,
                assemblyName,
                declaringType: "Synthetic.ModuleOwned",
                targetFramework: targetFramework,
                preferredRuntimePack: preferredRuntimePack));
            using var analyzer = new AssemblyAnalyzer(
                [.. module.Bytes],
                module.Path,
                sourceBundlePath: null,
                displayName: Path.GetFileName(module.Path),
                targetFrameworkOverride: module.TargetFramework,
                preferredRuntimePackOverride: module.PreferredRuntimePack);
            Assert.AreEqual(targetFramework, analyzer.TargetFramework);
            Assert.AreEqual(preferredRuntimePack, analyzer.PreferredRuntimePack);
            var type = Assert.ContainsSingle(
                analyzer.TypeDefs.Where(candidate => candidate.FullName == "Synthetic.ModuleOwned"));
            var method = Assert.ContainsSingle(
                analyzer.MethodDefs.Where(candidate =>
                    candidate.DeclaringType == type.FullName && candidate.Name == "Run"));
            var field = Assert.ContainsSingle(
                analyzer.FieldDefs.Where(candidate =>
                    candidate.DeclaringType == type.FullName && candidate.Name == "Value"));
            Assert.IsInstanceOfType<IlNavigationTarget.LocalType>(
                IlNavigationResolver.Resolve(analyzer, type.Token));
            Assert.IsInstanceOfType<IlNavigationTarget.LocalMethod>(
                IlNavigationResolver.Resolve(analyzer, method.Token));
            Assert.IsInstanceOfType<IlNavigationTarget.LocalField>(
                IlNavigationResolver.Resolve(analyzer, field.Token));

            var systemString = Assert.ContainsSingle(
                analyzer.TypeRefs.Where(typeReference => typeReference.FullName == "System.String"));
            var external = Assert.IsExactInstanceOfType<IlNavigationTarget.ExternalType>(
                IlNavigationResolver.Resolve(analyzer, systemString.Token));
            var secondHop = ImplementationAssemblyResolver.Resolve(
                analyzer.FilePath,
                external.AssemblyName,
                external.TypeRef.FullName,
                analyzer.TargetFramework,
                analyzer.PreferredRuntimePack);
            var secondHopFile = Assert.IsExactInstanceOfType<ResolvedAssembly.FromFile>(secondHop);
            Assert.AreEqual("System.Private.CoreLib.dll", Path.GetFileName(secondHopFile.Path));
        }
        finally
        {
            ImplementationAssemblyResolver.ClearCache();
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies nested exported-type and TypeDef chains resolve inside a sibling module.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImplementationAssemblyResolver_AssemblyFile_ResolvesNestedModuleType()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var suffix = Guid.NewGuid().ToString("N");
            var assemblyName = "NestedModuleManifest" + suffix;
            const string moduleName = "Nested.netmodule";
            var manifestPath = Path.Combine(directory, assemblyName + ".dll");
            var modulePath = Path.Combine(directory, moduleName);
            var moduleBytes = MetadataNestingConsumerMetadata.BuildNestedSiblingModule(moduleName);
            File.WriteAllBytes(
                manifestPath,
                MetadataNestingConsumerMetadata.BuildNestedSiblingModuleManifest(
                    assemblyName,
                    moduleName,
                    moduleBytes));
            File.WriteAllBytes(modulePath, moduleBytes);

            var resolved = ImplementationAssemblyResolver.Resolve(
                manifestPath,
                assemblyName,
                declaringType: "Synthetic.Outer/Inner");

            var module = Assert.IsExactInstanceOfType<ResolvedModule>(resolved);
            using var analyzer = new AssemblyAnalyzer([.. module.Bytes], module.Path);
            Assert.ContainsSingle(
                analyzer.TypeDefs.Where(type => type.FullName == "Synthetic.Outer/Inner"));
        }
        finally
        {
            ImplementationAssemblyResolver.ClearCache();
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Verifies missing, tampered, non-metadata, assembly, and mismatched module files hard-miss.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImplementationAssemblyResolver_AssemblyFile_InvalidSibling_HardMisses()
    {
        AssertInvalidSiblingModule(writeMode: "missing");
        AssertInvalidSiblingModule(writeMode: "tampered");
        AssertInvalidSiblingModule(writeMode: "contains-no-metadata");
        AssertInvalidSiblingModule(writeMode: "assembly");
        AssertInvalidSiblingModule(writeMode: "module-name-mismatch");
        AssertInvalidSiblingModule(writeMode: "type-name-mismatch");
        AssertInvalidSiblingModule(writeMode: "not-public-export");
        AssertInvalidSiblingModule(writeMode: "forwarder-file");
        AssertInvalidSiblingModule(writeMode: "duplicate-type");
        AssertInvalidSiblingModule(writeMode: "private-type");
    }

    /// <summary>
    /// Verifies AssemblyFile names containing traversal, rooted, UNC, device, or separator syntax
    /// never cause probing outside the manifest directory.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImplementationAssemblyResolver_AssemblyFile_UnsafeNamesHardMiss()
    {
        string[] unsafeNames =
        [
            "../Owned.netmodule",
            "..\\Owned.netmodule",
            "/tmp/Owned.netmodule",
            "C:\\Owned.netmodule",
            "\\\\server\\share\\Owned.netmodule",
            "\\\\?\\C:\\Owned.netmodule",
            "Owned.",
            "Owned.netmodule ",
            "CON.netmodule",
            "CON.extra.netmodule",
            "CON .netmodule",
            "NUL.netmodule",
            "COM1.netmodule",
            "COM¹.netmodule",
            "COM².netmodule",
            "COM³.netmodule",
            "LPT9.netmodule",
            "LPT¹.netmodule",
            "LPT².netmodule",
            "LPT³.netmodule",
            "Owned?.netmodule",
        ];

        foreach (var unsafeName in unsafeNames)
        {
            var directory = CreateTemporaryDirectory();
            try
            {
                var suffix = Guid.NewGuid().ToString("N");
                var assemblyName = "UnsafeModuleManifest" + suffix;
                var manifestPath = Path.Combine(directory, assemblyName + ".dll");
                var moduleBytes = MetadataNestingConsumerMetadata.BuildSiblingModule("Owned.netmodule");
                File.WriteAllBytes(
                    manifestPath,
                    MetadataNestingConsumerMetadata.BuildSiblingModuleManifest(
                        assemblyName,
                        unsafeName,
                        moduleBytes));

                var resolved = ImplementationAssemblyResolver.Resolve(
                    manifestPath,
                    assemblyName,
                    declaringType: "Synthetic.ModuleOwned");

                Assert.IsNull(resolved, unsafeName);
            }
            finally
            {
                ImplementationAssemblyResolver.ClearCache();
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies safe File-row names do not require a conventional module extension.
    /// </summary>
    /// <param name="moduleName">The safe sibling-module filename.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("OwnedModule")]
    [DataRow(".Owned.netmodule")]
    public void ImplementationAssemblyResolver_AssemblyFile_SafeUnconventionalName_Resolves(
        string moduleName)
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var suffix = Guid.NewGuid().ToString("N");
            var assemblyName = "UnconventionalModuleManifest" + suffix;
            var manifestPath = Path.Combine(directory, assemblyName + ".dll");
            var modulePath = Path.Combine(directory, moduleName);
            var moduleBytes = MetadataNestingConsumerMetadata.BuildSiblingModule(moduleName);
            File.WriteAllBytes(
                manifestPath,
                MetadataNestingConsumerMetadata.BuildSiblingModuleManifest(
                    assemblyName,
                    moduleName,
                    moduleBytes));
            File.WriteAllBytes(modulePath, moduleBytes);

            var resolved = ImplementationAssemblyResolver.Resolve(
                manifestPath,
                assemblyName,
                declaringType: "Synthetic.ModuleOwned");

            var module = Assert.IsExactInstanceOfType<ResolvedModule>(resolved);
            Assert.AreEqual(Path.GetFullPath(modulePath), Path.GetFullPath(module.Path));
        }
        finally
        {
            ImplementationAssemblyResolver.ClearCache();
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Verifies an AssemblyFile sibling that is a symbolic link is never followed, even when its
    /// target bytes and metadata exactly match the manifest's authenticated module entry.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImplementationAssemblyResolver_AssemblyFile_SymbolicLink_HardMisses()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var manifestDirectory = Path.Combine(directory, "manifest");
            var externalDirectory = Path.Combine(directory, "external");
            Directory.CreateDirectory(manifestDirectory);
            Directory.CreateDirectory(externalDirectory);

            var suffix = Guid.NewGuid().ToString("N");
            var assemblyName = "LinkedModuleManifest" + suffix;
            const string moduleName = "Owned.netmodule";
            var manifestPath = Path.Combine(manifestDirectory, assemblyName + ".dll");
            var linkedModulePath = Path.Combine(manifestDirectory, moduleName);
            var targetModulePath = Path.Combine(externalDirectory, moduleName);
            var moduleBytes = MetadataNestingConsumerMetadata.BuildSiblingModule(moduleName);
            File.WriteAllBytes(
                manifestPath,
                MetadataNestingConsumerMetadata.BuildSiblingModuleManifest(
                    assemblyName,
                    moduleName,
                    moduleBytes));
            File.WriteAllBytes(targetModulePath, moduleBytes);
            File.CreateSymbolicLink(linkedModulePath, targetModulePath);

            var resolved = ImplementationAssemblyResolver.Resolve(
                manifestPath,
                assemblyName,
                declaringType: "Synthetic.ModuleOwned");

            Assert.IsNull(resolved);
        }
        finally
        {
            ImplementationAssemblyResolver.ClearCache();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertAmbiguousFacadeHardMisses(
        bool includeTypeDefinition,
        int exportedTypeCount)
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var suffix = Guid.NewGuid().ToString("N");
            var facadeName = "AmbiguousFacade" + suffix;
            var targetName = "AmbiguousTarget" + suffix;
            var facadePath = Path.Combine(directory, facadeName + ".dll");
            File.WriteAllBytes(
                facadePath,
                MetadataNestingConsumerMetadata.BuildAmbiguousOwnershipFacade(
                    facadeName,
                    targetName,
                    includeTypeDefinition,
                    exportedTypeCount));
            File.WriteAllBytes(
                Path.Combine(directory, targetName + ".dll"),
                MetadataNestingConsumerMetadata.BuildTargetAssembly(targetName));

            var resolved = ImplementationAssemblyResolver.Resolve(
                facadePath,
                facadeName,
                declaringType: "Synthetic.Target");

            Assert.IsNull(resolved);
        }
        finally
        {
            ImplementationAssemblyResolver.ClearCache();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertInvalidSiblingModule(string writeMode)
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var suffix = Guid.NewGuid().ToString("N");
            var assemblyName = "InvalidModuleManifest" + suffix;
            const string moduleName = "Owned.netmodule";
            var manifestPath = Path.Combine(directory, assemblyName + ".dll");
            var modulePath = Path.Combine(directory, moduleName);
            var moduleBytes = writeMode switch
            {
                "assembly" => MetadataNestingConsumerMetadata.BuildSiblingModule(
                    moduleName,
                    includeAssemblyDefinition: true),
                "module-name-mismatch" => MetadataNestingConsumerMetadata.BuildSiblingModule(
                    "Different.netmodule"),
                "duplicate-type" => MetadataNestingConsumerMetadata.BuildSiblingModule(
                    moduleName,
                    duplicateType: true),
                "private-type" => MetadataNestingConsumerMetadata.BuildSiblingModule(
                    moduleName,
                    typeAttributes: TypeAttributes.NotPublic),
                _ => MetadataNestingConsumerMetadata.BuildSiblingModule(moduleName),
            };
            var containsMetadata = writeMode != "contains-no-metadata";
            var exportedTypeName = writeMode == "type-name-mismatch" ? "Missing" : "ModuleOwned";
            var exportedAttributes = writeMode switch
            {
                "not-public-export" => TypeAttributes.NotPublic,
                "forwarder-file" => TypeAttributes.Public | (TypeAttributes)0x0020_0000,
                _ => TypeAttributes.Public,
            };
            File.WriteAllBytes(
                manifestPath,
                MetadataNestingConsumerMetadata.BuildSiblingModuleManifest(
                    assemblyName,
                    moduleName,
                    moduleBytes,
                    containsMetadata,
                    exportedTypeName,
                    exportedAttributes: exportedAttributes));

            if (writeMode != "missing")
            {
                var onDiskBytes = moduleBytes.ToArray();
                if (writeMode == "tampered")
                {
                    onDiskBytes[^1] ^= 0xFF;
                }
                File.WriteAllBytes(modulePath, onDiskBytes);
            }

            var resolved = ImplementationAssemblyResolver.Resolve(
                manifestPath,
                assemblyName,
                declaringType: $"Synthetic.{exportedTypeName}");

            Assert.IsNull(resolved, writeMode);
            if (writeMode == "tampered")
            {
                File.WriteAllBytes(modulePath, moduleBytes);
                Assert.IsInstanceOfType<ResolvedModule>(ImplementationAssemblyResolver.Resolve(
                    manifestPath,
                    assemblyName,
                    declaringType: "Synthetic.ModuleOwned"));
            }
        }
        finally
        {
            ImplementationAssemblyResolver.ClearCache();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static AssemblyAnalyzer CreateCyclicAnalyzer() =>
        new(
            MetadataNestingConsumerMetadata.BuildCyclicConsumerAssembly(),
            filePath: "CyclicConsumers.dll");

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "dotsider-metadata-consumers-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
