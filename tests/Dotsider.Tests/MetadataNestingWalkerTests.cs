using Dotsider.Core.Analysis;
using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Verifies bounded metadata nesting-chain traversal and formatting.
/// </summary>
[TestClass]
public sealed class MetadataNestingWalkerTests
{
    /// <summary>Verifies declaring-type order, formatting, and the exact depth boundary.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DeclaringTypeChain_ReportsOrderNameAndDepthBoundary()
    {
        using (var stream = new MemoryStream(BuildTypeDefinitionChain(3)))
        using (var peReader = new PEReader(stream))
        {
            var reader = peReader.GetMetadataReader();
            var result = MetadataNestingWalker.DeclaringTypeChain(
                reader, MetadataTokens.TypeDefinitionHandle(1));

            Assert.AreEqual(ChainTermination.Complete, result.Termination);
            Assert.AreEqual(MetadataTokens.TypeDefinitionHandle(1), result.First);
            Assert.IsTrue(result.Terminal.IsNil);
            Assert.IsNotNull(result.Rest);
            Assert.AreSequenceEqual(
                [MetadataTokens.TypeDefinitionHandle(2), MetadataTokens.TypeDefinitionHandle(3)],
                result.Rest);
            Assert.AreEqual(string.Empty, result.FirstNamespace);
            Assert.AreEqual("Type0", result.FirstName);
            Assert.IsNotNull(result.RestNames);
            Assert.AreSequenceEqual(["Type1", "Type2"], result.RestNames);
            Assert.AreEqual("Synthetic", result.OutermostNamespace);
            Assert.IsTrue(MetadataNestingWalker.TryFormatTypeDefinitionName(result, out var name));
            Assert.AreEqual("Synthetic.Type2/Type1/Type0", name);
        }

        AssertTypeDefinitionTermination(1, null, ChainTermination.Complete);
        AssertTypeDefinitionTermination(129, null, ChainTermination.Complete);
        AssertTypeDefinitionTermination(130, null, ChainTermination.DepthExceeded);
    }

    /// <summary>
    /// Verifies a top-level name without a namespace reuses the walker's decoded string and does
    /// not allocate nested-chain storage.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DeclaringTypeChain_TopLevelName_ReusesRetainedString()
    {
        using var stream = new MemoryStream(BuildTypeDefinitionChain(1, includeNamespace: false));
        using var peReader = new PEReader(stream);
        var result = MetadataNestingWalker.DeclaringTypeChain(
            peReader.GetMetadataReader(), MetadataTokens.TypeDefinitionHandle(1));

        Assert.AreEqual(ChainTermination.Complete, result.Termination);
        Assert.AreEqual(string.Empty, result.FirstNamespace);
        Assert.AreEqual("Type0", result.FirstName);
        Assert.IsNull(result.Rest);
        Assert.IsNull(result.RestNames);
        Assert.IsTrue(MetadataNestingWalker.TryFormatTypeDefinitionName(result, out var fullName));
        Assert.AreSame(result.FirstName, fullName);
    }

    /// <summary>Verifies declaring-type cycle and invalid-parent classification.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DeclaringTypeChain_ClassifiesCyclesAndInvalidParents()
    {
        AssertTypeDefinitionTermination(1, MetadataTokens.TypeDefinitionHandle(1), ChainTermination.Cycle);
        AssertTypeDefinitionTermination(2, MetadataTokens.TypeDefinitionHandle(1), ChainTermination.Cycle);
        AssertTypeDefinitionTermination(1, MetadataTokens.TypeDefinitionHandle(2), ChainTermination.InvalidMetadata);
        AssertTypeDefinitionInvalidName();
        AssertTypeDefinitionInvalidNamespace();
        AssertTypeDefinitionNestedNamespace();
        AssertTypeDefinitionVisibilityMismatch();
    }

    /// <summary>Verifies resolution-scope order, formatting, legal terminals, and depth boundary.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ResolutionScopeChain_ReportsOrderNameTerminalsAndDepthBoundary()
    {
        using (var stream = new MemoryStream(BuildTypeReferenceChain(3, "assembly")))
        using (var peReader = new PEReader(stream))
        {
            var reader = peReader.GetMetadataReader();
            var result = MetadataNestingWalker.ResolutionScopeChain(
                reader, MetadataTokens.TypeReferenceHandle(1));

            Assert.AreEqual(ChainTermination.Complete, result.Termination);
            Assert.AreEqual(MetadataTokens.TypeReferenceHandle(1), result.First);
            Assert.AreEqual((EntityHandle)MetadataTokens.AssemblyReferenceHandle(1), result.Terminal);
            Assert.IsNotNull(result.Rest);
            Assert.AreSequenceEqual(
                [MetadataTokens.TypeReferenceHandle(2), MetadataTokens.TypeReferenceHandle(3)],
                result.Rest);
            Assert.AreEqual(string.Empty, result.FirstNamespace);
            Assert.AreEqual("Type0", result.FirstName);
            Assert.IsNotNull(result.RestNames);
            Assert.AreSequenceEqual(["Type1", "Type2"], result.RestNames);
            Assert.AreEqual("Synthetic", result.OutermostNamespace);
            Assert.IsTrue(MetadataNestingWalker.TryFormatTypeReferenceName(
                result, out var name, out var namespaceName));
            Assert.AreEqual("Synthetic.Type2/Type1/Type0", name);
            Assert.AreEqual("Synthetic", namespaceName);
            Assert.IsTrue(MetadataNestingWalker.TryFormatTypeReferenceParentName(
                result, out var parentName));
            Assert.AreEqual("Synthetic.Type2/Type1", parentName);
        }

        AssertTypeReferenceTermination(1, "module-reference", ChainTermination.Complete,
            MetadataTokens.ModuleReferenceHandle(1));
        AssertTypeReferenceTermination(1, "module-definition", ChainTermination.Complete,
            EntityHandle.ModuleDefinition);
        AssertTypeReferenceTermination(1, "nil", ChainTermination.Complete, default);
        AssertTypeReferenceTermination(129, "assembly", ChainTermination.Complete,
            MetadataTokens.AssemblyReferenceHandle(1));
        AssertTypeReferenceTermination(130, "assembly", ChainTermination.DepthExceeded,
            MetadataTokens.TypeReferenceHandle(130));
    }

    /// <summary>Verifies resolution-scope cycle and invalid-parent classification.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ResolutionScopeChain_ClassifiesCyclesAndInvalidParents()
    {
        AssertTypeReferenceTermination(1, "self-cycle", ChainTermination.Cycle,
            MetadataTokens.TypeReferenceHandle(1));
        AssertTypeReferenceTermination(2, "two-cycle", ChainTermination.Cycle,
            MetadataTokens.TypeReferenceHandle(1));
        AssertTypeReferenceTermination(1, "invalid-parent", ChainTermination.InvalidMetadata,
            MetadataTokens.TypeReferenceHandle(2));
        AssertTypeReferenceTermination(1, "invalid-assembly", ChainTermination.InvalidMetadata,
            MetadataTokens.AssemblyReferenceHandle(2));
        AssertTypeReferenceTermination(1, "invalid-module-reference", ChainTermination.InvalidMetadata,
            MetadataTokens.ModuleReferenceHandle(2));
        AssertTypeReferenceTermination(1, "invalid-module-definition", ChainTermination.InvalidMetadata,
            MetadataTokens.EntityHandle(2));
        AssertTypeReferenceTermination(1, "invalid-name", ChainTermination.InvalidMetadata, default);
        AssertTypeReferenceTermination(1, "invalid-namespace", ChainTermination.InvalidMetadata, default);
        AssertTypeReferenceNestedNamespace();
    }

    /// <summary>Verifies exported-type order, formatting, legal terminals, and depth boundary.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ExportedTypeImplementationChain_ReportsOrderNameTerminalsAndDepthBoundary()
    {
        using (var stream = new MemoryStream(BuildExportedTypeChain(3, "assembly")))
        using (var peReader = new PEReader(stream))
        {
            var reader = peReader.GetMetadataReader();
            var result = MetadataNestingWalker.ExportedTypeImplementationChain(
                reader, MetadataTokens.ExportedTypeHandle(1));

            Assert.AreEqual(ChainTermination.Complete, result.Termination);
            Assert.AreEqual(MetadataTokens.ExportedTypeHandle(1), result.First);
            Assert.AreEqual((EntityHandle)MetadataTokens.AssemblyReferenceHandle(1), result.Terminal);
            Assert.IsNotNull(result.Rest);
            Assert.AreSequenceEqual(
                [MetadataTokens.ExportedTypeHandle(2), MetadataTokens.ExportedTypeHandle(3)],
                result.Rest);
            Assert.AreEqual(string.Empty, result.FirstNamespace);
            Assert.AreEqual("Type0", result.FirstName);
            Assert.IsNotNull(result.RestNames);
            Assert.AreSequenceEqual(["Type1", "Type2"], result.RestNames);
            Assert.AreEqual("Synthetic", result.OutermostNamespace);
            Assert.IsTrue(MetadataNestingWalker.TryFormatExportedTypeName(result, out var name));
            Assert.AreEqual("Synthetic.Type2/Type1/Type0", name);
        }

        AssertExportedTypeTermination(1, "file", ChainTermination.Complete,
            MetadataTokens.AssemblyFileHandle(1));
        AssertExportedTypeTermination(1, "nil", ChainTermination.Complete,
            default(AssemblyFileHandle));
        AssertExportedTypeTermination(129, "assembly", ChainTermination.Complete,
            MetadataTokens.AssemblyReferenceHandle(1));
        AssertExportedTypeTermination(130, "assembly", ChainTermination.DepthExceeded,
            MetadataTokens.ExportedTypeHandle(130));
    }

    /// <summary>Verifies exported-type cycle and invalid-parent classification.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ExportedTypeImplementationChain_ClassifiesCyclesAndInvalidParents()
    {
        AssertExportedTypeTermination(1, "self-cycle", ChainTermination.Cycle,
            MetadataTokens.ExportedTypeHandle(1));
        AssertExportedTypeTermination(2, "two-cycle", ChainTermination.Cycle,
            MetadataTokens.ExportedTypeHandle(1));
        AssertExportedTypeTermination(1, "invalid-parent", ChainTermination.InvalidMetadata,
            MetadataTokens.ExportedTypeHandle(2));
        AssertExportedTypeTermination(1, "invalid-file", ChainTermination.InvalidMetadata,
            MetadataTokens.AssemblyFileHandle(2));
        AssertExportedTypeTermination(1, "invalid-assembly", ChainTermination.InvalidMetadata,
            MetadataTokens.AssemblyReferenceHandle(2));
        AssertExportedTypeInvalidImplementationTag();
        AssertExportedTypeInvalidName();
        AssertExportedTypeInvalidNamespace();
        AssertExportedTypeNestedNamespace();
    }

    /// <summary>
    /// Verifies both signature providers preserve the outermost namespace for nested TypeDefs.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SignatureProviders_NestedTypeDefinition_UseOutermostNamespace()
    {
        using var stream = new MemoryStream(BuildTypeDefinitionChain(3));
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var handle = MetadataTokens.TypeDefinitionHandle(1);

        var display = new AssemblySignatureTypeProvider().GetTypeFromDefinition(
            reader,
            handle,
            rawTypeKind: 0);
        var attribution = new EntityResolver(reader).GetTypeFromDefinition(
            reader,
            handle,
            rawTypeKind: 0);

        Assert.AreEqual("Synthetic.Type2/Type1/Type0", display);
        Assert.AreEqual("Synthetic.Type2/Type1/Type0", attribution.Display);
        Assert.AreEqual("Synthetic", attribution.Namespace);
    }

    private static void AssertTypeDefinitionTermination(
        int count,
        TypeDefinitionHandle? finalParent,
        ChainTermination expected)
    {
        using var stream = new MemoryStream(BuildTypeDefinitionChain(count, finalParent));
        using var peReader = new PEReader(stream);
        var result = MetadataNestingWalker.DeclaringTypeChain(
            peReader.GetMetadataReader(), MetadataTokens.TypeDefinitionHandle(1));
        Assert.AreEqual(expected, result.Termination);
        Assert.AreEqual(MetadataTokens.TypeDefinitionHandle(1), result.First);
        if (count == 1 && finalParent is null)
        {
            Assert.IsNull(result.Rest);
            Assert.IsNull(result.RestNames);
        }
    }

    private static void AssertTypeDefinitionInvalidName()
    {
        var image = BuildTypeDefinitionChain(1);
        PatchFirstTypeDefinitionName(image);
        using var stream = new MemoryStream(image);
        using var peReader = new PEReader(stream);
        var result = MetadataNestingWalker.DeclaringTypeChain(
            peReader.GetMetadataReader(), MetadataTokens.TypeDefinitionHandle(1));

        Assert.AreEqual(ChainTermination.InvalidMetadata, result.Termination);
        Assert.AreEqual(MetadataTokens.TypeDefinitionHandle(1), result.First);
        Assert.AreEqual(string.Empty, result.FirstName);
        Assert.AreEqual(string.Empty, result.FirstNamespace);
        Assert.IsTrue(result.Terminal.IsNil);
    }

    private static void AssertTypeDefinitionInvalidNamespace()
    {
        var image = BuildTypeDefinitionChain(1);
        PatchFirstTypeDefinitionNamespace(image);
        using var stream = new MemoryStream(image);
        using var peReader = new PEReader(stream);
        var result = MetadataNestingWalker.DeclaringTypeChain(
            peReader.GetMetadataReader(), MetadataTokens.TypeDefinitionHandle(1));

        Assert.AreEqual(ChainTermination.InvalidMetadata, result.Termination);
        Assert.AreEqual(MetadataTokens.TypeDefinitionHandle(1), result.First);
        Assert.AreEqual(string.Empty, result.FirstName);
        Assert.AreEqual(string.Empty, result.FirstNamespace);
        Assert.IsNull(result.Rest);
        Assert.IsTrue(result.Terminal.IsNil);
    }

    private static void AssertTypeDefinitionVisibilityMismatch()
    {
        var image = BuildTypeDefinitionChain(2);
        PatchFirstTypeDefinitionAttributes(image, TypeAttributes.Public);
        using var stream = new MemoryStream(image);
        using var peReader = new PEReader(stream);
        var result = MetadataNestingWalker.DeclaringTypeChain(
            peReader.GetMetadataReader(), MetadataTokens.TypeDefinitionHandle(1));

        Assert.AreEqual(ChainTermination.InvalidMetadata, result.Termination);
        Assert.AreEqual(
            (EntityHandle)MetadataTokens.TypeDefinitionHandle(2),
            result.Terminal);
    }

    private static void AssertTypeDefinitionNestedNamespace()
    {
        var image = BuildTypeDefinitionChain(2);
        PatchFirstTypeDefinitionNestedNamespace(image);
        using var stream = new MemoryStream(image);
        using var peReader = new PEReader(stream);
        var result = MetadataNestingWalker.DeclaringTypeChain(
            peReader.GetMetadataReader(), MetadataTokens.TypeDefinitionHandle(1));

        Assert.AreEqual(ChainTermination.InvalidMetadata, result.Termination);
    }

    private static void AssertTypeReferenceNestedNamespace()
    {
        var image = BuildTypeReferenceChain(2, "assembly");
        PatchFirstTypeReferenceNestedNamespace(image);
        using var stream = new MemoryStream(image);
        using var peReader = new PEReader(stream);
        var result = MetadataNestingWalker.ResolutionScopeChain(
            peReader.GetMetadataReader(), MetadataTokens.TypeReferenceHandle(1));

        Assert.AreEqual(ChainTermination.InvalidMetadata, result.Termination);
    }

    private static void AssertExportedTypeNestedNamespace()
    {
        var image = BuildExportedTypeChain(2, "assembly");
        PatchFirstExportedTypeNestedNamespace(image);
        using var stream = new MemoryStream(image);
        using var peReader = new PEReader(stream);
        var result = MetadataNestingWalker.ExportedTypeImplementationChain(
            peReader.GetMetadataReader(), MetadataTokens.ExportedTypeHandle(1));

        Assert.AreEqual(ChainTermination.InvalidMetadata, result.Termination);
    }

    private static void AssertTypeReferenceTermination(
        int count,
        string terminal,
        ChainTermination expected,
        EntityHandle expectedTerminal)
    {
        using var stream = new MemoryStream(BuildTypeReferenceChain(count, terminal));
        using var peReader = new PEReader(stream);
        var result = MetadataNestingWalker.ResolutionScopeChain(
            peReader.GetMetadataReader(), MetadataTokens.TypeReferenceHandle(1));
        Assert.AreEqual(expected, result.Termination);
        Assert.AreEqual(MetadataTokens.TypeReferenceHandle(1), result.First);
        Assert.AreEqual(expectedTerminal, result.Terminal);
        if (count == 1)
        {
            Assert.IsNull(result.Rest);
            Assert.IsNull(result.RestNames);
        }
    }

    private static void AssertExportedTypeTermination(
        int count,
        string terminal,
        ChainTermination expected,
        EntityHandle expectedTerminal)
    {
        using var stream = new MemoryStream(BuildExportedTypeChain(count, terminal));
        using var peReader = new PEReader(stream);
        var result = MetadataNestingWalker.ExportedTypeImplementationChain(
            peReader.GetMetadataReader(), MetadataTokens.ExportedTypeHandle(1));
        Assert.AreEqual(expected, result.Termination);
        Assert.AreEqual(MetadataTokens.ExportedTypeHandle(1), result.First);
        Assert.AreEqual(expectedTerminal, result.Terminal);
        if (count == 1)
        {
            Assert.IsNull(result.Rest);
            Assert.IsNull(result.RestNames);
        }
    }

    private static void AssertExportedTypeInvalidName()
    {
        var image = BuildExportedTypeChain(1, "assembly");
        PatchFirstExportedTypeName(image);
        using var stream = new MemoryStream(image);
        using var peReader = new PEReader(stream);
        var result = MetadataNestingWalker.ExportedTypeImplementationChain(
            peReader.GetMetadataReader(), MetadataTokens.ExportedTypeHandle(1));

        Assert.AreEqual(ChainTermination.InvalidMetadata, result.Termination);
        Assert.AreEqual(MetadataTokens.ExportedTypeHandle(1), result.First);
        Assert.AreEqual(string.Empty, result.FirstName);
        Assert.AreEqual(string.Empty, result.FirstNamespace);
        Assert.IsTrue(result.Terminal.IsNil);
    }

    private static void AssertExportedTypeInvalidImplementationTag()
    {
        var image = BuildExportedTypeChain(1, "assembly");
        PatchFirstExportedTypeImplementation(image, encodedImplementation: 3);
        using var stream = new MemoryStream(image);
        using var peReader = new PEReader(stream);
        var result = MetadataNestingWalker.ExportedTypeImplementationChain(
            peReader.GetMetadataReader(), MetadataTokens.ExportedTypeHandle(1));

        Assert.AreEqual(ChainTermination.InvalidMetadata, result.Termination);
        Assert.AreEqual(MetadataTokens.ExportedTypeHandle(1), result.First);
        Assert.IsTrue(result.Terminal.IsNil);
    }

    private static void AssertExportedTypeInvalidNamespace()
    {
        var image = BuildExportedTypeChain(1, "assembly");
        PatchFirstExportedTypeNamespace(image);
        using var stream = new MemoryStream(image);
        using var peReader = new PEReader(stream);
        var result = MetadataNestingWalker.ExportedTypeImplementationChain(
            peReader.GetMetadataReader(), MetadataTokens.ExportedTypeHandle(1));

        Assert.AreEqual(ChainTermination.InvalidMetadata, result.Termination);
        Assert.AreEqual(MetadataTokens.ExportedTypeHandle(1), result.First);
        Assert.AreEqual(string.Empty, result.FirstName);
        Assert.AreEqual(string.Empty, result.FirstNamespace);
        Assert.IsNull(result.Rest);
        Assert.IsTrue(result.Terminal.IsNil);
    }

    private static byte[] BuildTypeDefinitionChain(
        int count,
        TypeDefinitionHandle? finalParent = null,
        bool includeNamespace = true)
    {
        var metadata = CreateMetadata();
        for (var i = 0; i < count; i++)
        {
            var isNested = i < count - 1 || finalParent.HasValue;
            metadata.AddTypeDefinition(
                isNested ? TypeAttributes.NestedPublic : TypeAttributes.Public,
                isNested || !includeNamespace ? default : metadata.GetOrAddString("Synthetic"),
                metadata.GetOrAddString($"Type{i}"),
                default,
                MetadataTokens.FieldDefinitionHandle(1),
                MetadataTokens.MethodDefinitionHandle(1));
        }

        for (var row = 1; row < count; row++)
        {
            metadata.AddNestedType(
                MetadataTokens.TypeDefinitionHandle(row),
                MetadataTokens.TypeDefinitionHandle(row + 1));
        }

        if (finalParent is { } parent)
        {
            metadata.AddNestedType(MetadataTokens.TypeDefinitionHandle(count), parent);
        }

        return Serialize(metadata);
    }

    private static byte[] BuildTypeReferenceChain(int count, string terminalKind)
    {
        var metadata = CreateMetadata();
        var assemblyReference = metadata.AddAssemblyReference(
            metadata.GetOrAddString("Referenced"), new Version(1, 0, 0, 0),
            default, default, default, default);
        var moduleReference = metadata.AddModuleReference(metadata.GetOrAddString("Referenced.netmodule"));

        EntityHandle terminal = terminalKind switch
        {
            "assembly" => assemblyReference,
            "module-reference" => moduleReference,
            "module-definition" => EntityHandle.ModuleDefinition,
            "nil" => default(ModuleDefinitionHandle),
            "self-cycle" => MetadataTokens.TypeReferenceHandle(1),
            "two-cycle" => MetadataTokens.TypeReferenceHandle(1),
            "invalid-parent" => MetadataTokens.TypeReferenceHandle(count + 1),
            "invalid-assembly" => MetadataTokens.AssemblyReferenceHandle(2),
            "invalid-module-reference" => MetadataTokens.ModuleReferenceHandle(2),
            "invalid-module-definition" => EntityHandle.ModuleDefinition,
            "invalid-name" => assemblyReference,
            "invalid-namespace" => assemblyReference,
            _ => throw new ArgumentOutOfRangeException(nameof(terminalKind)),
        };

        for (var i = 0; i < count; i++)
        {
            var scope = i < count - 1 ? MetadataTokens.TypeReferenceHandle(i + 2) : terminal;
            metadata.AddTypeReference(
                scope,
                scope.Kind == HandleKind.TypeReference
                    ? default
                    : metadata.GetOrAddString("Synthetic"),
                metadata.GetOrAddString($"Type{i}"));
        }

        var image = Serialize(metadata);
        if (terminalKind == "invalid-name")
        {
            PatchFirstTypeReferenceName(image);
        }
        else if (terminalKind == "invalid-namespace")
        {
            PatchFirstTypeReferenceNamespace(image);
        }
        else if (terminalKind == "invalid-module-definition")
        {
            PatchFirstTypeReferenceResolutionScope(image, encodedScope: 8);
        }
        return image;
    }

    private static byte[] BuildExportedTypeChain(int count, string terminalKind)
    {
        var metadata = CreateMetadata();
        var assemblyReference = metadata.AddAssemblyReference(
            metadata.GetOrAddString("Referenced"), new Version(1, 0, 0, 0),
            default, default, default, default);
        var file = metadata.AddAssemblyFile(
            metadata.GetOrAddString("Referenced.netmodule"), default, containsMetadata: true);

        EntityHandle terminal = terminalKind switch
        {
            "assembly" => assemblyReference,
            "file" => file,
            "nil" => default(AssemblyFileHandle),
            "self-cycle" => MetadataTokens.ExportedTypeHandle(1),
            "two-cycle" => MetadataTokens.ExportedTypeHandle(1),
            "invalid-parent" => MetadataTokens.ExportedTypeHandle(count + 1),
            "invalid-file" => MetadataTokens.AssemblyFileHandle(2),
            "invalid-assembly" => MetadataTokens.AssemblyReferenceHandle(2),
            _ => throw new ArgumentOutOfRangeException(nameof(terminalKind)),
        };

        for (var i = 0; i < count; i++)
        {
            var implementation = i < count - 1 ? MetadataTokens.ExportedTypeHandle(i + 2) : terminal;
            metadata.AddExportedType(
                implementation.Kind == HandleKind.ExportedType
                    ? TypeAttributes.NestedPublic
                    : TypeAttributes.Public,
                implementation.Kind == HandleKind.ExportedType
                    ? default
                    : metadata.GetOrAddString("Synthetic"),
                metadata.GetOrAddString($"Type{i}"),
                implementation,
                typeDefinitionId: 0);
        }

        return Serialize(metadata);
    }

    private static MetadataBuilder CreateMetadata()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(
            0, metadata.GetOrAddString("Synthetic.dll"), metadata.GetOrAddGuid(Guid.NewGuid()),
            default, default);
        metadata.AddAssembly(
            metadata.GetOrAddString("Synthetic"), new Version(1, 0, 0, 0),
            default, default, default, AssemblyHashAlgorithm.None);
        return metadata;
    }

    private static void PatchFirstTypeReferenceName(byte[] image)
    {
        using var stream = new MemoryStream(image, writable: false);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        Assert.IsLessThanOrEqualTo(ushort.MaxValue, reader.GetHeapSize(HeapIndex.String));
        var rowOffset = peReader.PEHeaders.MetadataStartOffset
            + reader.GetTableMetadataOffset(TableIndex.TypeRef);

        // ResolutionScope is a two-byte coded index in this small fixture; Name follows it.
        image[rowOffset + 2] = 0xFF;
        image[rowOffset + 3] = 0x7F;
    }

    private static void PatchFirstTypeReferenceNamespace(byte[] image)
    {
        using var stream = new MemoryStream(image, writable: false);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        Assert.IsLessThanOrEqualTo(ushort.MaxValue, reader.GetHeapSize(HeapIndex.String));
        var rowOffset = peReader.PEHeaders.MetadataStartOffset
            + reader.GetTableMetadataOffset(TableIndex.TypeRef);

        // ResolutionScope and Name precede the two-byte Namespace string-heap index.
        image[rowOffset + 4] = 0xFF;
        image[rowOffset + 5] = 0x7F;
    }

    private static void PatchFirstTypeReferenceResolutionScope(byte[] image, ushort encodedScope)
    {
        using var stream = new MemoryStream(image, writable: false);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var rowOffset = peReader.PEHeaders.MetadataStartOffset
            + reader.GetTableMetadataOffset(TableIndex.TypeRef);

        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(rowOffset), encodedScope);
    }

    private static void PatchFirstTypeReferenceNestedNamespace(byte[] image)
    {
        using var stream = new MemoryStream(image, writable: false);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var namespaceOffset = MetadataTokens.GetHeapOffset(
            reader.GetTypeReference(MetadataTokens.TypeReferenceHandle(2)).Namespace);
        var rowOffset = peReader.PEHeaders.MetadataStartOffset
            + reader.GetTableMetadataOffset(TableIndex.TypeRef);

        BinaryPrimitives.WriteUInt16LittleEndian(
            image.AsSpan(rowOffset + 4),
            checked((ushort)namespaceOffset));
    }

    private static void PatchFirstTypeDefinitionName(byte[] image)
    {
        using var stream = new MemoryStream(image, writable: false);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        Assert.IsLessThanOrEqualTo(ushort.MaxValue, reader.GetHeapSize(HeapIndex.String));
        var rowOffset = peReader.PEHeaders.MetadataStartOffset
            + reader.GetTableMetadataOffset(TableIndex.TypeDef);

        // Flags precede the two-byte Name string-heap index in this small fixture.
        image[rowOffset + 4] = 0xFF;
        image[rowOffset + 5] = 0x7F;
    }

    private static void PatchFirstTypeDefinitionNamespace(byte[] image)
    {
        using var stream = new MemoryStream(image, writable: false);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        Assert.IsLessThanOrEqualTo(ushort.MaxValue, reader.GetHeapSize(HeapIndex.String));
        var rowOffset = peReader.PEHeaders.MetadataStartOffset
            + reader.GetTableMetadataOffset(TableIndex.TypeDef);

        // Flags and Name precede the two-byte Namespace string-heap index.
        image[rowOffset + 6] = 0xFF;
        image[rowOffset + 7] = 0x7F;
    }

    private static void PatchFirstTypeDefinitionAttributes(byte[] image, TypeAttributes attributes)
    {
        using var stream = new MemoryStream(image, writable: false);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var rowOffset = peReader.PEHeaders.MetadataStartOffset
            + reader.GetTableMetadataOffset(TableIndex.TypeDef);

        BinaryPrimitives.WriteUInt32LittleEndian(
            image.AsSpan(rowOffset),
            (uint)attributes);
    }

    private static void PatchFirstTypeDefinitionNestedNamespace(byte[] image)
    {
        using var stream = new MemoryStream(image, writable: false);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var namespaceOffset = MetadataTokens.GetHeapOffset(
            reader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(2)).Namespace);
        var rowOffset = peReader.PEHeaders.MetadataStartOffset
            + reader.GetTableMetadataOffset(TableIndex.TypeDef);

        BinaryPrimitives.WriteUInt16LittleEndian(
            image.AsSpan(rowOffset + 6),
            checked((ushort)namespaceOffset));
    }

    private static void PatchFirstExportedTypeName(byte[] image)
    {
        using var stream = new MemoryStream(image, writable: false);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        Assert.IsLessThanOrEqualTo(ushort.MaxValue, reader.GetHeapSize(HeapIndex.String));
        var rowOffset = peReader.PEHeaders.MetadataStartOffset
            + reader.GetTableMetadataOffset(TableIndex.ExportedType);

        // Flags and TypeDefId precede the two-byte TypeName string-heap index.
        image[rowOffset + 8] = 0xFF;
        image[rowOffset + 9] = 0x7F;
    }

    private static void PatchFirstExportedTypeNamespace(byte[] image)
    {
        using var stream = new MemoryStream(image, writable: false);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        Assert.IsLessThanOrEqualTo(ushort.MaxValue, reader.GetHeapSize(HeapIndex.String));
        var rowOffset = peReader.PEHeaders.MetadataStartOffset
            + reader.GetTableMetadataOffset(TableIndex.ExportedType);

        // Flags, TypeDefId, and TypeName precede the two-byte TypeNamespace string-heap index.
        image[rowOffset + 10] = 0xFF;
        image[rowOffset + 11] = 0x7F;
    }

    private static void PatchFirstExportedTypeImplementation(byte[] image, ushort encodedImplementation)
    {
        using var stream = new MemoryStream(image, writable: false);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var rowOffset = peReader.PEHeaders.MetadataStartOffset
            + reader.GetTableMetadataOffset(TableIndex.ExportedType);

        // Flags, TypeDefId, TypeName, and TypeNamespace precede the coded Implementation index.
        BinaryPrimitives.WriteUInt16LittleEndian(
            image.AsSpan(rowOffset + 12),
            encodedImplementation);
    }

    private static void PatchFirstExportedTypeNestedNamespace(byte[] image)
    {
        using var stream = new MemoryStream(image, writable: false);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var namespaceOffset = MetadataTokens.GetHeapOffset(
            reader.GetExportedType(MetadataTokens.ExportedTypeHandle(2)).Namespace);
        var rowOffset = peReader.PEHeaders.MetadataStartOffset
            + reader.GetTableMetadataOffset(TableIndex.ExportedType);

        BinaryPrimitives.WriteUInt16LittleEndian(
            image.AsSpan(rowOffset + 10),
            checked((ushort)namespaceOffset));
    }

    private static byte[] Serialize(MetadataBuilder metadata)
    {
        var peBuilder = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll),
            new MetadataRootBuilder(metadata),
            new BlobBuilder());
        var image = new BlobBuilder();
        peBuilder.Serialize(image);
        return image.ToArray();
    }
}
