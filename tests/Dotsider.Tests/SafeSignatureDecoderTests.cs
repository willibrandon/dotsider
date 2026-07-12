using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Signatures;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Exercises every production signature root through the production decoding facade.
/// </summary>
[TestClass]
public sealed class SafeSignatureDecoderTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>Verifies every entity-root facade validates and decodes a valid signature.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void EveryFacadeRoot_ValidSignature_Decodes()
    {
        using var scope = FacadeSignatureMetadataScope.Create(typeSpecifications: [[0x0F, 0x08]]);
        var reader = scope.Reader;
        var provider = new AssemblySignatureTypeProvider();

        var method = SafeSignatureDecoder.DecodeMethodSignature(
            reader, scope.MethodDefinition, provider, genericContext: default);
        var field = SafeSignatureDecoder.DecodeFieldSignature(
            reader, scope.FieldDefinition, provider, genericContext: default);
        var property = SafeSignatureDecoder.DecodePropertySignature(
            reader, scope.PropertyDefinition, provider, genericContext: default);
        var standalone = SafeSignatureDecoder.DecodeStandaloneMethodSignature(
            reader, scope.StandaloneMethod, provider, genericContext: default);
        var locals = SafeSignatureDecoder.DecodeLocalSignature(
            reader, scope.LocalSignature, provider, genericContext: default);
        var memberMethod = SafeSignatureDecoder.DecodeMemberReferenceMethodSignature(
            reader, scope.MemberReferenceMethod, provider, genericContext: default);
        var memberField = SafeSignatureDecoder.DecodeMemberReferenceFieldSignature(
            reader, scope.MemberReferenceField, provider, genericContext: default);
        var methodArguments = SafeSignatureDecoder.DecodeMethodSpecificationSignature(
            reader, scope.MethodSpecification, provider, genericContext: default);
        var typeSpecification = SafeSignatureDecoder.DecodeType(
            reader, scope.TypeSpecifications[0], provider, genericContext: default);

        Assert.AreEqual("int", method.ReturnType);
        Assert.AreEqual("int", field);
        Assert.AreEqual("int", property.ReturnType);
        Assert.AreEqual("int", standalone.ReturnType);
        Assert.AreSequenceEqual(["int"], locals);
        Assert.AreEqual("int", memberMethod.ReturnType);
        Assert.AreEqual("int", memberField);
        Assert.AreSequenceEqual(["int"], methodArguments);
        Assert.AreEqual("int*", typeSpecification);
    }

    /// <summary>
    /// Verifies entity facades bind handles to the supplied reader and expose no cross-reader
    /// entity-struct overload that could validate one image and decode another.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void EntityFacades_AreHandleBoundToSuppliedReader()
    {
        using var deep = FacadeSignatureMetadataScope.Create(
            method: BuildMethod(BuildPointerType(SignatureBlobValidator.MaxSignatureDepth + 1)),
            typeSpecifications: [BuildPointerType(SignatureBlobValidator.MaxSignatureDepth + 1)]);
        using var benign = FacadeSignatureMetadataScope.Create(typeSpecifications: [[0x08]]);

        var decoded = SafeSignatureDecoder.DecodeMethodSignature(
            benign.Reader,
            deep.MethodDefinition,
            new AssemblySignatureTypeProvider(),
            genericContext: default);
        Assert.AreEqual("int", decoded.ReturnType);
        Assert.ThrowsExactly<BadImageFormatException>(() => SafeSignatureDecoder.DecodeMethodSignature(
            deep.Reader,
            benign.MethodDefinition,
            new AssemblySignatureTypeProvider(),
            genericContext: default));

        var decodedType = SafeSignatureDecoder.DecodeType(
            benign.Reader,
            deep.TypeSpecifications[0],
            new AssemblySignatureTypeProvider(),
            genericContext: default);
        Assert.AreEqual("int", decodedType);
        Assert.ThrowsExactly<BadImageFormatException>(() => SafeSignatureDecoder.DecodeType(
            deep.Reader,
            benign.TypeSpecifications[0],
            new AssemblySignatureTypeProvider(),
            genericContext: default));

        var expectedHandleTypes = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            [nameof(SafeSignatureDecoder.DecodeMethodSignature)] = typeof(MethodDefinitionHandle),
            [nameof(SafeSignatureDecoder.DecodeFieldSignature)] = typeof(FieldDefinitionHandle),
            [nameof(SafeSignatureDecoder.DecodePropertySignature)] = typeof(PropertyDefinitionHandle),
            [nameof(SafeSignatureDecoder.DecodeMethodSpecificationSignature)] = typeof(MethodSpecificationHandle),
            [nameof(SafeSignatureDecoder.DecodeStandaloneMethodSignature)] = typeof(StandaloneSignatureHandle),
            [nameof(SafeSignatureDecoder.DecodeLocalSignature)] = typeof(StandaloneSignatureHandle),
            [nameof(SafeSignatureDecoder.DecodeMemberReferenceMethodSignature)] = typeof(MemberReferenceHandle),
            [nameof(SafeSignatureDecoder.DecodeMemberReferenceFieldSignature)] = typeof(MemberReferenceHandle),
            [nameof(SafeSignatureDecoder.DecodeType)] = typeof(TypeSpecificationHandle),
        };
        var methods = typeof(SafeSignatureDecoder).GetMethods(
            BindingFlags.Static | BindingFlags.NonPublic);
        foreach (var (name, handleType) in expectedHandleTypes)
        {
            var method = methods.Single(candidate => candidate.Name == name);
            Assert.AreEqual(handleType, method.GetParameters()[1].ParameterType);
        }
    }

    /// <summary>Verifies the mstat attribution provider decodes only through the same facade.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MstatProvider_ValidSignature_DecodesThroughFacade()
    {
        using var scope = FacadeSignatureMetadataScope.Create(typeSpecifications: [[0x1D, 0x08]]);
        var resolver = new EntityResolver(scope.Reader);

        var field = SafeSignatureDecoder.DecodeFieldSignature(
            scope.Reader,
            scope.FieldDefinition,
            resolver,
            genericContext: default);
        var type = SafeSignatureDecoder.DecodeType(
            scope.Reader,
            scope.TypeSpecifications[0],
            resolver,
            genericContext: default);

        Assert.AreEqual("int", field.Display);
        Assert.AreEqual("int[]", type.Display);
    }

    /// <summary>Verifies facade decoding is value-identical to SRM for every valid root in a real assembly.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RealAssembly_AllSignatureRoots_MatchFrameworkDecoder()
    {
        AssertRealAssemblySignatureRootsMatchFrameworkDecoder(Samples.RichLibraryDll);
        if (Samples.ReadyToRunConsoleDll is { } readyToRunAssembly)
        {
            AssertRealAssemblySignatureRootsMatchFrameworkDecoder(readyToRunAssembly);
        }
    }

    private static void AssertRealAssemblySignatureRootsMatchFrameworkDecoder(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var provider = new AssemblySignatureTypeProvider();

        foreach (var handle in reader.MethodDefinitions)
        {
            var definition = reader.GetMethodDefinition(handle);
            var expected = definition.DecodeSignature(provider, genericContext: default);
            var actual = SafeSignatureDecoder.DecodeMethodSignature(
                reader, handle, provider, genericContext: default);
            AssertMethodSignaturesEqual(expected, actual);
        }

        foreach (var handle in reader.FieldDefinitions)
        {
            var definition = reader.GetFieldDefinition(handle);
            Assert.AreEqual(
                definition.DecodeSignature(provider, genericContext: default),
                SafeSignatureDecoder.DecodeFieldSignature(reader, handle, provider, genericContext: default));
        }

        foreach (var handle in reader.PropertyDefinitions)
        {
            var definition = reader.GetPropertyDefinition(handle);
            AssertMethodSignaturesEqual(
                definition.DecodeSignature(provider, genericContext: default),
                SafeSignatureDecoder.DecodePropertySignature(reader, handle, provider, genericContext: default));
        }

        for (var row = 1; row <= reader.GetTableRowCount(TableIndex.TypeSpec); row++)
        {
            var handle = MetadataTokens.TypeSpecificationHandle(row);
            var definition = reader.GetTypeSpecification(handle);
            Assert.AreEqual(
                definition.DecodeSignature(provider, genericContext: default),
                SafeSignatureDecoder.DecodeType(reader, handle, provider, genericContext: default));
        }

        for (var row = 1; row <= reader.GetTableRowCount(TableIndex.MethodSpec); row++)
        {
            var handle = MetadataTokens.MethodSpecificationHandle(row);
            var definition = reader.GetMethodSpecification(handle);
            Assert.AreSequenceEqual(
                definition.DecodeSignature(provider, genericContext: default),
                SafeSignatureDecoder.DecodeMethodSpecificationSignature(
                    reader, handle, provider, genericContext: default));
        }

        for (var row = 1; row <= reader.GetTableRowCount(TableIndex.StandAloneSig); row++)
        {
            var handle = MetadataTokens.StandaloneSignatureHandle(row);
            var signature = reader.GetStandaloneSignature(handle);
            var blob = reader.GetBlobReader(signature.Signature);
            if (blob.ReadSignatureHeader().Kind == SignatureKind.LocalVariables)
            {
                Assert.AreSequenceEqual(
                    signature.DecodeLocalSignature(provider, genericContext: default),
                    SafeSignatureDecoder.DecodeLocalSignature(reader, handle, provider, genericContext: default));
            }
            else
            {
                AssertMethodSignaturesEqual(
                    signature.DecodeMethodSignature(provider, genericContext: default),
                    SafeSignatureDecoder.DecodeStandaloneMethodSignature(
                        reader, handle, provider, genericContext: default));
            }
        }

        foreach (var handle in reader.MemberReferences)
        {
            var reference = reader.GetMemberReference(handle);
            if (reference.GetKind() == MemberReferenceKind.Field)
            {
                Assert.AreEqual(
                    reference.DecodeFieldSignature(provider, genericContext: default),
                    SafeSignatureDecoder.DecodeMemberReferenceFieldSignature(
                        reader, handle, provider, genericContext: default));
            }
            else
            {
                AssertMethodSignaturesEqual(
                    reference.DecodeMethodSignature(provider, genericContext: default),
                    SafeSignatureDecoder.DecodeMemberReferenceMethodSignature(
                        reader, handle, provider, genericContext: default));
            }
        }
    }

    /// <summary>Verifies deep input is rejected by the facade before SRM's recursive decoder runs.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodFacade_Depth129_IsRejectedBeforeDecode()
    {
        var signature = new byte[3 + SignatureBlobValidator.MaxSignatureDepth + 1];
        signature[0] = 0x00;
        signature[1] = 0x00;
        Array.Fill(signature, (byte)0x0F, 2, SignatureBlobValidator.MaxSignatureDepth + 1);
        signature[^1] = 0x08;
        using var scope = FacadeSignatureMetadataScope.Create(method: signature);

        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            SafeSignatureDecoder.DecodeMethodSignature(
                scope.Reader,
                scope.MethodDefinition,
                new AssemblySignatureTypeProvider(),
                genericContext: default));

        Assert.Contains("129", exception.Message);
        Assert.Contains("128", exception.Message);
    }

    /// <summary>Verifies a cyclic custom-modifier TypeSpec is rejected before provider re-entry.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FieldFacade_CyclicTypeSpecification_IsRejectedBeforeDecode()
    {
        using var scope = FacadeSignatureMetadataScope.Create(
            field: [0x06, 0x1F, 0x06, 0x08],
            typeSpecifications: [[0x0F, 0x1F, 0x06, 0x08]]);

        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            SafeSignatureDecoder.DecodeFieldSignature(
                scope.Reader,
                scope.FieldDefinition,
                new AssemblySignatureTypeProvider(),
                genericContext: default));

        Assert.Contains("Cyclic", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies caller-specific root grammar is enforced through the facade itself.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FacadeRoot_WrongEntityHeader_IsRejected()
    {
        using var scope = FacadeSignatureMetadataScope.Create(method: [0x08, 0x00, 0x08]);

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            SafeSignatureDecoder.DecodeMethodSignature(
                scope.Reader,
                scope.MethodDefinition,
                new AssemblySignatureTypeProvider(),
                genericContext: default));
    }

    /// <summary>Verifies every public facade root accepts exactly 128 nested type edges.</summary>
    /// <param name="root">The facade root under test.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("MethodDef")]
    [DataRow("FieldDef")]
    [DataRow("Property")]
    [DataRow("StandaloneMethod")]
    [DataRow("Local")]
    [DataRow("MemberRefMethod")]
    [DataRow("MemberRefField")]
    [DataRow("MethodSpec")]
    [DataRow("TypeSpec")]
    public void EveryFacadeRoot_Depth128_IsAccepted(string root)
    {
        DecodeFacadeRoot(root, SignatureBlobValidator.MaxSignatureDepth, useMstatProvider: false);
        DecodeFacadeRoot(root, SignatureBlobValidator.MaxSignatureDepth, useMstatProvider: true);
    }

    /// <summary>Verifies every public facade root rejects the 129th nested type edge.</summary>
    /// <param name="root">The facade root under test.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("MethodDef")]
    [DataRow("FieldDef")]
    [DataRow("Property")]
    [DataRow("StandaloneMethod")]
    [DataRow("Local")]
    [DataRow("MemberRefMethod")]
    [DataRow("MemberRefField")]
    [DataRow("MethodSpec")]
    [DataRow("TypeSpec")]
    public void EveryFacadeRoot_Depth129_IsRejected(string root)
    {
        var assemblyException = Assert.ThrowsExactly<BadImageFormatException>(() =>
            DecodeFacadeRoot(
                root,
                SignatureBlobValidator.MaxSignatureDepth + 1,
                useMstatProvider: false));
        var mstatException = Assert.ThrowsExactly<BadImageFormatException>(() =>
            DecodeFacadeRoot(
                root,
                SignatureBlobValidator.MaxSignatureDepth + 1,
                useMstatProvider: true));

        Assert.Contains("129", assemblyException.Message);
        Assert.Contains("128", assemblyException.Message);
        Assert.Contains("129", mstatException.Message);
        Assert.Contains("128", mstatException.Message);
    }

    /// <summary>Verifies every recursive edge at depth 128 through both production providers.</summary>
    /// <param name="production">The recursive production under test.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("Pointer")]
    [DataRow("ByReference")]
    [DataRow("SZArray")]
    [DataRow("Array")]
    [DataRow("GenericArgument")]
    [DataRow("FunctionPointerReturn")]
    [DataRow("FunctionPointerParameter")]
    [DataRow("RequiredModifier")]
    [DataRow("OptionalModifier")]
    [DataRow("Pinned")]
    public void BothProviders_EveryRecursiveEdge_Depth128_IsAccepted(string production)
    {
        DecodeRecursiveProduction(production, SignatureBlobValidator.MaxSignatureDepth, useMstatProvider: false);
        DecodeRecursiveProduction(production, SignatureBlobValidator.MaxSignatureDepth, useMstatProvider: true);
    }

    /// <summary>Verifies every recursive edge at depth 129 through both production providers.</summary>
    /// <param name="production">The recursive production under test.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("Pointer")]
    [DataRow("ByReference")]
    [DataRow("SZArray")]
    [DataRow("Array")]
    [DataRow("GenericArgument")]
    [DataRow("FunctionPointerReturn")]
    [DataRow("FunctionPointerParameter")]
    [DataRow("RequiredModifier")]
    [DataRow("OptionalModifier")]
    [DataRow("Pinned")]
    public void BothProviders_EveryRecursiveEdge_Depth129_IsRejected(string production)
    {
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            DecodeRecursiveProduction(
                production, SignatureBlobValidator.MaxSignatureDepth + 1, useMstatProvider: false));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            DecodeRecursiveProduction(
                production, SignatureBlobValidator.MaxSignatureDepth + 1, useMstatProvider: true));
    }

    /// <summary>Verifies cyclic TypeSpec graphs are rejected through the facade for both providers.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BothProviders_TypeSpecificationCycles_AreRejectedThroughFacade()
    {
        byte[][][] graphs =
        [
            [[0x0F, 0x1F, 0x06, 0x08]],
            [[0x0F, 0x1F, 0x0A, 0x08], [0x0F, 0x1F, 0x06, 0x08]],
        ];

        foreach (var graph in graphs)
        {
            using var scope = FacadeSignatureMetadataScope.Create(typeSpecifications: graph);
            Assert.ThrowsExactly<BadImageFormatException>(() => SafeSignatureDecoder.DecodeType(
                scope.Reader,
                scope.TypeSpecifications[0],
                new AssemblySignatureTypeProvider(),
                genericContext: default));
            Assert.ThrowsExactly<BadImageFormatException>(() => SafeSignatureDecoder.DecodeType(
                scope.Reader,
                scope.TypeSpecifications[0],
                new EntityResolver(scope.Reader),
                genericContext: default));
        }
    }

    /// <summary>Verifies shared acyclic TypeSpec reuse unwinds correctly through both providers.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BothProviders_SharedTypeSpecificationDag_DecodesThroughFacade()
    {
        byte[] method = [0x00, 0x02, 0x08, 0x1F, 0x06, 0x08, 0x1F, 0x06, 0x08];
        using var scope = FacadeSignatureMetadataScope.Create(
            method: method,
            typeSpecifications: [[0x0F, 0x08]]);
        var definition = scope.MethodDefinition;

        var strings = SafeSignatureDecoder.DecodeMethodSignature(
            scope.Reader, definition, new AssemblySignatureTypeProvider(), genericContext: default);
        var attributions = SafeSignatureDecoder.DecodeMethodSignature(
            scope.Reader, definition, new EntityResolver(scope.Reader), genericContext: default);

        Assert.HasCount(2, strings.ParameterTypes);
        Assert.HasCount(2, attributions.ParameterTypes);
    }

    /// <summary>Verifies transitive TypeSpec depth is cumulative through the facade for both providers.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BothProviders_TransitiveTypeSpecificationDepth_IsCumulativeThroughFacade()
    {
        using (var accepted = FacadeSignatureMetadataScope.Create(
            field: [0x06, 0x1F, 0x06, 0x08],
            typeSpecifications: BuildTransitiveTypeSpecifications(exceedsLimit: false)))
        {
            _ = SafeSignatureDecoder.DecodeFieldSignature(
                accepted.Reader,
                accepted.FieldDefinition,
                new AssemblySignatureTypeProvider(),
                genericContext: default);
            _ = SafeSignatureDecoder.DecodeFieldSignature(
                accepted.Reader,
                accepted.FieldDefinition,
                new EntityResolver(accepted.Reader),
                genericContext: default);
        }

        using var rejected = FacadeSignatureMetadataScope.Create(
            field: [0x06, 0x1F, 0x06, 0x08],
            typeSpecifications: BuildTransitiveTypeSpecifications(exceedsLimit: true));
        Assert.ThrowsExactly<BadImageFormatException>(() => SafeSignatureDecoder.DecodeFieldSignature(
            rejected.Reader,
            rejected.FieldDefinition,
            new AssemblySignatureTypeProvider(),
            genericContext: default));
        Assert.ThrowsExactly<BadImageFormatException>(() => SafeSignatureDecoder.DecodeFieldSignature(
            rejected.Reader,
            rejected.FieldDefinition,
            new EntityResolver(rejected.Reader),
            genericContext: default));
    }

    /// <summary>Verifies cached shallow-first TypeSpec reuse cannot bypass cumulative depth in either provider.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BothProviders_ShallowFirstDeepSecondTypeSpecificationReuse_EnforcesDepth()
    {
        using (var accepted = FacadeSignatureMetadataScope.Create(
            method: BuildSharedTypeSpecificationMethod(secondParameterPointerDepth: 126),
            typeSpecifications: [[0x0F, 0x08]]))
        {
            _ = SafeSignatureDecoder.DecodeMethodSignature(
                accepted.Reader,
                accepted.MethodDefinition,
                new AssemblySignatureTypeProvider(),
                genericContext: default);
            _ = SafeSignatureDecoder.DecodeMethodSignature(
                accepted.Reader,
                accepted.MethodDefinition,
                new EntityResolver(accepted.Reader),
                genericContext: default);
        }

        using var rejected = FacadeSignatureMetadataScope.Create(
            method: BuildSharedTypeSpecificationMethod(secondParameterPointerDepth: 127),
            typeSpecifications: [[0x0F, 0x08]]);
        Assert.ThrowsExactly<BadImageFormatException>(() => SafeSignatureDecoder.DecodeMethodSignature(
            rejected.Reader,
            rejected.MethodDefinition,
            new AssemblySignatureTypeProvider(),
            genericContext: default));
        Assert.ThrowsExactly<BadImageFormatException>(() => SafeSignatureDecoder.DecodeMethodSignature(
            rejected.Reader,
            rejected.MethodDefinition,
            new EntityResolver(rejected.Reader),
            genericContext: default));
    }

    /// <summary>
    /// Verifies both production providers accept the signature grammar corrections used by .NET.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BothProviders_RuntimeAugmentGrammar_DecodesThroughFacade()
    {
        AssertBothProvidersAccept("MethodDef", [0x00, 0x00, 0x10, 0x1F, 0x04, 0x08]);
        AssertBothProvidersAccept("FieldDef", [0x06, 0x10, 0x1E, 0x00]);
        AssertBothProvidersAccept("FieldDef", [0x06, 0x16]);
        AssertBothProvidersAccept("FieldDef", [0x06, 0x10, 0x16]);
        AssertBothProvidersAccept("FieldDef", [0x06, 0x0F, 0x16]);
        AssertBothProvidersAccept("MemberRefField", [0x06, 0x10, 0x13, 0x00]);
        AssertBothProvidersAccept("MemberRefField", [0x06, 0x16]);
        AssertBothProvidersAccept("FieldDef", [0x06, 0x14, 0x1F, 0x04, 0x08, 0x01, 0x00, 0x00]);
        AssertBothProvidersAccept("FieldDef", [0x06, 0x15, 0x12, 0x04, 0x01, 0x1F, 0x04, 0x08]);
        AssertBothProvidersAccept("MethodSpec", [0x0A, 0x01, 0x1F, 0x04, 0x08]);
        AssertBothProvidersAccept("MethodDef", [0x00, 0x00, 0x01]);
        AssertBothProvidersAccept("FieldDef", [0x06, 0x0F, 0x01]);
        AssertBothProvidersAccept("FieldDef", [0x06, 0x1B, 0x00, 0x00, 0x01]);
        AssertBothProvidersAccept("Property", [0x08, 0x00, 0x16]);
        AssertBothProvidersAccept("StandaloneMethod", [0x01, 0x01, 0x08, 0x41, 0x08]);
        AssertBothProvidersAccept("MemberRefMethod", [0x05, 0x01, 0x08, 0x41, 0x08]);
        AssertBothProvidersAccept("Local", [0x07, 0x01, 0x45, 0x1C]);
        AssertBothProvidersAccept("Local", [0x07, 0x01, 0x45, 0x10, 0x08]);
        AssertBothProvidersAccept("Local", [0x07, 0x01, 0x45, 0x08]);
        AssertBothProvidersAccept("Local", [0x07, 0x01, 0x45, 0x16]);
        AssertBothProvidersAccept("Local", [0x07, 0x01, 0x45, 0x11, 0x04]);
        AssertBothProvidersAccept("TypeSpec", [0x08]);
        AssertBothProvidersAccept("TypeSpec", [0x01]);
        AssertBothProvidersAccept("TypeSpec", [0x16]);
        AssertBothProvidersAccept("TypeSpec", [0x1F, 0x04, 0x08]);
        AssertBothProvidersAccept("TypeSpec", [0x10, 0x08]);
    }

    /// <summary>
    /// Verifies both production providers accept a TypeSpec-root managed pointer at depth 128
    /// and reject it at depth 129.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BothProviders_TypeSpecificationByReferenceRoot_EnforcesExactDepthBoundary()
    {
        AssertBothProvidersAccept(
            "TypeSpec",
            [0x10, .. BuildPointerType(SignatureBlobValidator.MaxSignatureDepth - 1)]);
        AssertBothProvidersReject(
            "TypeSpec",
            [0x10, .. BuildPointerType(SignatureBlobValidator.MaxSignatureDepth)]);
    }

    /// <summary>
    /// Verifies both production providers reject caller-context and TypeSpec grammar violations.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BothProviders_ContextGrammarViolations_AreRejectedThroughFacade()
    {
        AssertBothProvidersReject("MethodDef", [0x60, 0x00, 0x08]);
        AssertBothProvidersReject("MemberRefMethod", [0x60, 0x00, 0x08]);
        AssertBothProvidersReject("StandaloneMethod", [0x00, 0x01, 0x08, 0x41, 0x08]);
        AssertBothProvidersReject("Property", [0x08, 0x00, 0x01]);
        AssertBothProvidersReject("TypeSpec", [0x12, 0x04]);
        AssertBothProvidersReject("TypeSpec", [0x11, 0x04]);
        AssertBothProvidersReject("TypeSpec", [0x45, 0x1C]);
    }

    /// <summary>
    /// Verifies non-canonical compressed unsigned and signed integers fail before either
    /// production provider reaches the framework decoder.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BothProviders_NonCanonicalCompressedIntegers_AreRejectedThroughFacade()
    {
        AssertBothProvidersReject("Local", [0x07, 0x80, 0x01, 0x08]);
        AssertBothProvidersReject("FieldDef", [0x06, 0x12, 0x80, 0x04]);
        AssertBothProvidersReject(
            "FieldDef",
            [0x06, 0x14, 0x08, 0x01, 0x00, 0x01, 0x80, 0x00]);
    }

    /// <summary>
    /// Verifies every facade root rejects otherwise-valid signatures with trailing bytes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BothProviders_EveryFacadeRoot_TrailingBytesAreRejected()
    {
        AssertBothProvidersReject("MethodDef", [0x00, 0x00, 0x08, 0x08]);
        AssertBothProvidersReject("FieldDef", [0x06, 0x08, 0x08]);
        AssertBothProvidersReject("Property", [0x08, 0x00, 0x08, 0x08]);
        AssertBothProvidersReject("StandaloneMethod", [0x00, 0x00, 0x08, 0x08]);
        AssertBothProvidersReject("Local", [0x07, 0x01, 0x08, 0x08]);
        AssertBothProvidersReject("MemberRefMethod", [0x00, 0x00, 0x08, 0x08]);
        AssertBothProvidersReject("MemberRefField", [0x06, 0x08, 0x08]);
        AssertBothProvidersReject("MethodSpec", [0x0A, 0x01, 0x08, 0x08]);
        AssertBothProvidersReject("TypeSpec", [0x0F, 0x08, 0x08]);
    }

    /// <summary>
    /// Verifies GENERICINST bases and signature type handles fail closed through both providers.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BothProviders_GenericBasesAndTypeHandles_AreValidatedThroughFacade()
    {
        AssertBothProvidersAccept("FieldDef", [0x06, 0x15, 0x12, 0x04, 0x01, 0x08]);
        AssertBothProvidersAccept("FieldDef", [0x06, 0x15, 0x11, 0x04, 0x01, 0x08]);

        AssertBothProvidersReject("FieldDef", [0x06, 0x15, 0x08, 0x01, 0x08]);
        AssertBothProvidersReject("FieldDef", [0x06, 0x15, 0x0F, 0x08, 0x01, 0x08]);
        AssertBothProvidersReject("FieldDef", [0x06, 0x15, 0x1B, 0x00, 0x00, 0x08, 0x01, 0x08]);
        AssertBothProvidersReject("FieldDef", [0x06, 0x15, 0x15, 0x12, 0x04, 0x01, 0x08]);
        AssertBothProvidersReject("FieldDef", [0x06, 0x15, 0x12, 0x00, 0x01, 0x08]);
        AssertBothProvidersReject(
            "FieldDef",
            [0x06, 0x15, 0x12, 0x06, 0x01, 0x08],
            typeSpecifications: [[0x0F, 0x08]]);

        AssertBothProvidersReject("FieldDef", [0x06, 0x1F, 0x02, 0x08]);
        AssertBothProvidersReject(
            "FieldDef",
            [0x06, 0x1F, 0x0A, 0x08],
            typeSpecifications: [[0x0F, 0x08]]);
        AssertBothProvidersReject("FieldDef", [0x06, 0x1F, 0x07, 0x08]);
    }

    /// <summary>
    /// Verifies sequence counts are rejected before iteration when their elements cannot fit.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BothProviders_SequenceCountExceedingRemainingBytes_IsRejected()
    {
        AssertBothProvidersReject("Local", [0x07, 0x02, 0x08]);
        AssertBothProvidersReject("MethodSpec", [0x0A, 0x02, 0x08]);
        AssertBothProvidersReject("FieldDef", [0x06, 0x15, 0x12, 0x04, 0x02, 0x08]);
    }

    /// <summary>
    /// Verifies array-shape and type-argument boundaries through both production providers.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BothProviders_ArrayAndTypeArgumentBoundaries_AreEnforced()
    {
        AssertBothProvidersAccept("FieldDef", [0x06, 0x14, 0x08, 0x01, 0x00, 0x00]);
        AssertBothProvidersAccept("FieldDef", [0x06, 0x14, 0x08, 0x20, 0x00, 0x00]);
        AssertBothProvidersAccept(
            "FieldDef",
            [0x06, 0x14, 0x08, 0x02, 0x02, 0x00, 0x00, 0x02, 0x00, 0x00]);

        AssertBothProvidersReject("FieldDef", [0x06, 0x14, 0x08, 0x00, 0x00, 0x00]);
        AssertBothProvidersReject("FieldDef", [0x06, 0x14, 0x08, 0x21, 0x00, 0x00]);
        AssertBothProvidersReject(
            "FieldDef",
            [0x06, 0x14, 0x08, 0x02, 0x03, 0x00, 0x00, 0x00, 0x00]);
        AssertBothProvidersReject(
            "FieldDef",
            [0x06, 0x14, 0x08, 0x02, 0x00, 0x03, 0x00, 0x00, 0x00]);

        AssertBothProvidersAccept(
            "FieldDef",
            BuildGenericInstantiationField(SignatureBlobValidator.MaxTypeArguments));
        AssertBothProvidersReject(
            "FieldDef",
            BuildGenericInstantiationField(SignatureBlobValidator.MaxTypeArguments + 1));
        AssertBothProvidersAccept(
            "MethodSpec",
            BuildMethodSpecificationWithArguments(SignatureBlobValidator.MaxTypeArguments));
        AssertBothProvidersReject(
            "MethodSpec",
            BuildMethodSpecificationWithArguments(SignatureBlobValidator.MaxTypeArguments + 1));
        AssertBothProvidersAccept(
            "MethodDef",
            BuildGenericMethod(SignatureBlobValidator.MaxTypeArguments));
        AssertBothProvidersReject(
            "MethodDef",
            BuildGenericMethod(SignatureBlobValidator.MaxTypeArguments + 1));
    }

    /// <summary>
    /// Verifies the exact cumulative work boundary through both production providers.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BothProviders_ExpandedWorkBoundary_IsEnforced()
    {
        AssertBothProvidersAccept(
            "Local",
            BuildLocalWithCount(SignatureBlobValidator.MaxSignatureWork));
        AssertBothProvidersReject(
            "Local",
            BuildLocalWithCount(SignatureBlobValidator.MaxSignatureWork + 1));
        AssertBothProvidersAccept(
            "MethodDef",
            BuildMethodWithParameters(SignatureBlobValidator.MaxSignatureWork - 1));
        AssertBothProvidersReject(
            "MethodDef",
            BuildMethodWithParameters(SignatureBlobValidator.MaxSignatureWork));
    }

    private static void AssertBothProvidersAccept(
        string root,
        byte[] signature,
        IReadOnlyList<byte[]>? typeSpecifications = null)
    {
        using var scope = CreateFacadeScope(root, signature, typeSpecifications);
        DecodeFacadeRootCore(
            scope.Reader,
            scope,
            root,
            new AssemblySignatureTypeProvider());
        DecodeFacadeRootCore(
            scope.Reader,
            scope,
            root,
            new EntityResolver(scope.Reader));
    }

    private static void AssertBothProvidersReject(
        string root,
        byte[] signature,
        IReadOnlyList<byte[]>? typeSpecifications = null)
    {
        using var scope = CreateFacadeScope(root, signature, typeSpecifications);
        Assert.ThrowsExactly<BadImageFormatException>(() => DecodeFacadeRootCore(
            scope.Reader,
            scope,
            root,
            new AssemblySignatureTypeProvider()));
        Assert.ThrowsExactly<BadImageFormatException>(() => DecodeFacadeRootCore(
            scope.Reader,
            scope,
            root,
            new EntityResolver(scope.Reader)));
    }

    private static FacadeSignatureMetadataScope CreateFacadeScope(
        string root,
        byte[] signature,
        IReadOnlyList<byte[]>? typeSpecifications)
    {
        var specifications = typeSpecifications ??
            (root == "TypeSpec" ? [signature] : [[0x0F, 0x08]]);
        return root switch
        {
            "MethodDef" => FacadeSignatureMetadataScope.Create(
                method: signature,
                typeSpecifications: specifications),
            "FieldDef" => FacadeSignatureMetadataScope.Create(
                field: signature,
                typeSpecifications: specifications),
            "Property" => FacadeSignatureMetadataScope.Create(
                property: signature,
                typeSpecifications: specifications),
            "StandaloneMethod" => FacadeSignatureMetadataScope.Create(
                standaloneMethod: signature,
                typeSpecifications: specifications),
            "Local" => FacadeSignatureMetadataScope.Create(
                local: signature,
                typeSpecifications: specifications),
            "MemberRefMethod" => FacadeSignatureMetadataScope.Create(
                memberReferenceMethod: signature,
                typeSpecifications: specifications),
            "MemberRefField" => FacadeSignatureMetadataScope.Create(
                memberReferenceField: signature,
                typeSpecifications: specifications),
            "MethodSpec" => FacadeSignatureMetadataScope.Create(
                methodSpecification: signature,
                typeSpecifications: specifications),
            "TypeSpec" => FacadeSignatureMetadataScope.Create(
                typeSpecifications: specifications),
            _ => throw new ArgumentOutOfRangeException(nameof(root)),
        };
    }

    private static void DecodeFacadeRoot(string root, int depth, bool useMstatProvider)
    {
        var type = BuildPointerType(depth);
        using var scope = FacadeSignatureMetadataScope.Create(
            method: root == "MethodDef" ? BuildMethod(type) : null,
            field: root == "FieldDef" ? BuildField(type) : null,
            property: root == "Property" ? BuildProperty(type) : null,
            standaloneMethod: root == "StandaloneMethod" ? BuildMethod(type) : null,
            local: root == "Local" ? BuildLocal(type) : null,
            memberReferenceMethod: root == "MemberRefMethod" ? BuildMethod(type) : null,
            memberReferenceField: root == "MemberRefField" ? BuildField(type) : null,
            methodSpecification: root == "MethodSpec" ? BuildMethodSpecification(type) : null,
            typeSpecifications: root == "TypeSpec" ? [type] : [[0x0F, 0x08]]);
        var reader = scope.Reader;

        if (useMstatProvider)
        {
            DecodeFacadeRootCore(reader, scope, root, new EntityResolver(reader));
        }
        else
        {
            DecodeFacadeRootCore(reader, scope, root, new AssemblySignatureTypeProvider());
        }
    }

    private static void DecodeFacadeRootCore<TType>(
        MetadataReader reader,
        FacadeSignatureMetadataScope scope,
        string root,
        ISignatureTypeProvider<TType, object?> provider)
    {
        _ = root switch
        {
            "MethodDef" => SafeSignatureDecoder.DecodeMethodSignature(
                reader, scope.MethodDefinition, provider, genericContext: default).ReturnType,
            "FieldDef" => SafeSignatureDecoder.DecodeFieldSignature(
                reader, scope.FieldDefinition, provider, genericContext: default),
            "Property" => SafeSignatureDecoder.DecodePropertySignature(
                reader, scope.PropertyDefinition, provider, genericContext: default).ReturnType,
            "StandaloneMethod" => SafeSignatureDecoder.DecodeStandaloneMethodSignature(
                reader, scope.StandaloneMethod, provider, genericContext: default).ReturnType,
            "Local" => SafeSignatureDecoder.DecodeLocalSignature(
                reader, scope.LocalSignature, provider, genericContext: default)[0],
            "MemberRefMethod" => SafeSignatureDecoder.DecodeMemberReferenceMethodSignature(
                reader, scope.MemberReferenceMethod, provider, genericContext: default).ReturnType,
            "MemberRefField" => SafeSignatureDecoder.DecodeMemberReferenceFieldSignature(
                reader, scope.MemberReferenceField, provider, genericContext: default),
            "MethodSpec" => SafeSignatureDecoder.DecodeMethodSpecificationSignature(
                reader, scope.MethodSpecification, provider, genericContext: default)[0],
            "TypeSpec" => SafeSignatureDecoder.DecodeType(
                reader, scope.TypeSpecifications[0], provider, genericContext: default),
            _ => throw new ArgumentOutOfRangeException(nameof(root)),
        };
    }

    private static void DecodeRecursiveProduction(string production, int depth, bool useMstatProvider)
    {
        var pointerDepth = production switch
        {
            "Pointer" => depth,
            "Pinned" => depth - 2,
            _ => depth - 1,
        };
        var child = BuildPointerType(pointerDepth);
        var method = production == "ByReference" ? BuildMethod([0x10, .. child]) : null;
        var local = production == "Pinned" ? BuildLocal([0x45, 0x10, .. child]) : null;
        var field = production is "ByReference" or "Pinned"
            ? null
            : BuildField(WrapRecursiveProduction(production, child));
        using var scope = FacadeSignatureMetadataScope.Create(method: method, field: field, local: local);

        if (useMstatProvider)
        {
            var provider = new EntityResolver(scope.Reader);
            if (production == "ByReference")
            {
                _ = SafeSignatureDecoder.DecodeMethodSignature(
                    scope.Reader,
                    scope.MethodDefinition,
                    provider,
                    genericContext: default);
            }
            else if (production == "Pinned")
            {
                _ = SafeSignatureDecoder.DecodeLocalSignature(
                    scope.Reader,
                    scope.LocalSignature,
                    provider,
                    genericContext: default);
            }
            else
            {
                _ = SafeSignatureDecoder.DecodeFieldSignature(
                    scope.Reader,
                    scope.FieldDefinition,
                    provider,
                    genericContext: default);
            }
        }
        else
        {
            var provider = new AssemblySignatureTypeProvider();
            if (production == "ByReference")
            {
                _ = SafeSignatureDecoder.DecodeMethodSignature(
                    scope.Reader,
                    scope.MethodDefinition,
                    provider,
                    genericContext: default);
            }
            else if (production == "Pinned")
            {
                _ = SafeSignatureDecoder.DecodeLocalSignature(
                    scope.Reader,
                    scope.LocalSignature,
                    provider,
                    genericContext: default);
            }
            else
            {
                _ = SafeSignatureDecoder.DecodeFieldSignature(
                    scope.Reader,
                    scope.FieldDefinition,
                    provider,
                    genericContext: default);
            }
        }
    }

    private static byte[] WrapRecursiveProduction(string production, byte[] child) => production switch
    {
        "Pointer" => child,
        "SZArray" => [0x1D, .. child],
        "Array" => [0x14, .. child, 0x01, 0x00, 0x00],
        "GenericArgument" => [0x15, 0x12, 0x04, 0x01, .. child],
        "FunctionPointerReturn" => [0x1B, 0x00, 0x00, .. child],
        "FunctionPointerParameter" => [0x1B, 0x00, 0x01, 0x08, .. child],
        "RequiredModifier" => [0x1F, 0x04, .. child],
        "OptionalModifier" => [0x20, 0x04, .. child],
        _ => throw new ArgumentOutOfRangeException(nameof(production)),
    };

    private static List<byte[]> BuildTransitiveTypeSpecifications(bool exceedsLimit)
    {
        const int count = 64;
        var signatures = new List<byte[]>(count);
        for (var row = 1; row < count; row++)
        {
            var signature = new List<byte> { 0x1D, 0x1F };
            AddCompressedUnsigned(signature, ((row + 1) << 2) | 0x02);
            signature.Add(0x08);
            signatures.Add([.. signature]);
        }

        signatures.Add(exceedsLimit ? [0x0F, 0x0F, 0x08] : [0x0F, 0x08]);
        return signatures;
    }

    private static byte[] BuildSharedTypeSpecificationMethod(int secondParameterPointerDepth)
    {
        var signature = new List<byte> { 0x00, 0x02, 0x08, 0x1F, 0x06, 0x08 };
        for (var i = 0; i < secondParameterPointerDepth; i++)
        {
            signature.Add(0x0F);
        }
        signature.AddRange([0x1F, 0x06, 0x08]);
        return [.. signature];
    }

    private static byte[] BuildGenericInstantiationField(int argumentCount)
    {
        var signature = new List<byte>(argumentCount + 8) { 0x06, 0x15, 0x12, 0x04 };
        AddCompressedUnsigned(signature, argumentCount);
        for (var i = 0; i < argumentCount; i++)
        {
            signature.Add(0x08);
        }

        return [.. signature];
    }

    private static byte[] BuildMethodSpecificationWithArguments(int argumentCount)
    {
        var signature = new List<byte>(argumentCount + 6) { 0x0A };
        AddCompressedUnsigned(signature, argumentCount);
        for (var i = 0; i < argumentCount; i++)
        {
            signature.Add(0x08);
        }

        return [.. signature];
    }

    private static byte[] BuildGenericMethod(int genericArity)
    {
        var signature = new List<byte> { 0x10 };
        AddCompressedUnsigned(signature, genericArity);
        signature.AddRange([0x00, 0x08]);
        return [.. signature];
    }

    private static byte[] BuildLocalWithCount(int count)
    {
        var signature = new List<byte>(count + 6) { 0x07 };
        AddCompressedUnsigned(signature, count);
        for (var i = 0; i < count; i++)
        {
            signature.Add(0x08);
        }

        return [.. signature];
    }

    private static byte[] BuildMethodWithParameters(int parameterCount)
    {
        var signature = new List<byte>(parameterCount + 7) { 0x00 };
        AddCompressedUnsigned(signature, parameterCount);
        signature.Add(0x08);
        for (var i = 0; i < parameterCount; i++)
        {
            signature.Add(0x08);
        }

        return [.. signature];
    }

    private static void AddCompressedUnsigned(List<byte> bytes, int value)
    {
        if (value <= 0x7F)
        {
            bytes.Add((byte)value);
        }
        else if (value <= 0x3FFF)
        {
            bytes.Add((byte)(0x80 | (value >> 8)));
            bytes.Add((byte)value);
        }
        else
        {
            bytes.Add((byte)(0xC0 | (value >> 24)));
            bytes.Add((byte)(value >> 16));
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)value);
        }
    }

    private static void AssertMethodSignaturesEqual(
        MethodSignature<string> expected,
        MethodSignature<string> actual)
    {
        Assert.AreEqual(expected.Header.RawValue, actual.Header.RawValue);
        Assert.AreEqual(expected.GenericParameterCount, actual.GenericParameterCount);
        Assert.AreEqual(expected.RequiredParameterCount, actual.RequiredParameterCount);
        Assert.AreEqual(expected.ReturnType, actual.ReturnType);
        Assert.AreSequenceEqual(expected.ParameterTypes, actual.ParameterTypes);
    }

    private static byte[] BuildPointerType(int depth)
    {
        var type = new byte[depth + 1];
        Array.Fill(type, (byte)0x0F, 0, depth);
        type[^1] = 0x08;
        return type;
    }

    private static byte[] BuildMethod(byte[] type) => [0x00, 0x00, .. type];

    private static byte[] BuildField(byte[] type) => [0x06, .. type];

    private static byte[] BuildProperty(byte[] type) => [0x08, 0x00, .. type];

    private static byte[] BuildLocal(byte[] type) => [0x07, 0x01, .. type];

    private static byte[] BuildMethodSpecification(byte[] type) => [0x0A, 0x01, .. type];
}
