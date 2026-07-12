using Dotsider.Core.Analysis.Signatures;
using System.Reflection.Metadata.Ecma335;

namespace Dotsider.Tests;

/// <summary>
/// Direct structural and boundary tests for <see cref="SignatureBlobValidator"/>.
/// </summary>
[TestClass]
public class SignatureBlobValidatorTests
{
    /// <summary>
    /// Verifies that every recursive type production accepts a path whose deepest node is exactly depth 128.
    /// </summary>
    /// <param name="production">The recursive production under test.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("Pointer")]
    [DataRow("ByReference")]
    [DataRow("SZArray")]
    [DataRow("ArrayElement")]
    [DataRow("GenericArgument")]
    [DataRow("FunctionPointerReturn")]
    [DataRow("FunctionPointerParameter")]
    [DataRow("RequiredModifier")]
    [DataRow("OptionalModifier")]
    [DataRow("PinnedLocal")]
    public void RecursiveProduction_AtDepthLimit_IsAccepted(string production)
    {
        ValidateRecursiveProduction(production, SignatureBlobValidator.MaxSignatureDepth);
    }

    /// <summary>
    /// Verifies that every recursive type production rejects a path whose deepest node is depth 129.
    /// </summary>
    /// <param name="production">The recursive production under test.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("Pointer")]
    [DataRow("ByReference")]
    [DataRow("SZArray")]
    [DataRow("ArrayElement")]
    [DataRow("GenericArgument")]
    [DataRow("FunctionPointerReturn")]
    [DataRow("FunctionPointerParameter")]
    [DataRow("RequiredModifier")]
    [DataRow("OptionalModifier")]
    [DataRow("PinnedLocal")]
    public void RecursiveProduction_AboveDepthLimit_IsRejected(string production)
    {
        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateRecursiveProduction(production, SignatureBlobValidator.MaxSignatureDepth + 1));

        Assert.Contains("129", exception.Message);
        Assert.Contains("128", exception.Message);
    }

    /// <summary>
    /// Verifies that each root validator accepts only its exact valid header shape.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RootHeaders_ExactKinds_AreAccepted()
    {
        ValidateField([0x06, 0x08]);
        ValidateLocal([0x07, 0x01, 0x08]);
        ValidateMethodSpecification([0x0A, 0x01, 0x08]);
        ValidateMethod([0x00, 0x00, 0x08], SignatureCallerKind.MethodDefinition);
        ValidateMethod([0x00, 0x00, 0x08], SignatureCallerKind.MemberReference);
        ValidateMethod([0x00, 0x00, 0x08], SignatureCallerKind.StandaloneSignature);
        ValidateMethod([0x00, 0x00, 0x08], SignatureCallerKind.FunctionPointer);
        ValidateMethod([0x08, 0x00, 0x08], SignatureCallerKind.PropertyDefinition);
        ValidateMethod([0x60, 0x00, 0x08], SignatureCallerKind.StandaloneSignature);
        ValidateMethod([0x60, 0x00, 0x08], SignatureCallerKind.FunctionPointer);
    }

    /// <summary>
    /// Verifies that caller-specific root headers, reserved bits, and exact entity headers fail closed.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RootHeaders_Mismatches_AreRejected()
    {
        Assert.ThrowsExactly<BadImageFormatException>(() => ValidateField([0x16, 0x08]));
        Assert.ThrowsExactly<BadImageFormatException>(() => ValidateLocal([0x27, 0x01, 0x08]));
        Assert.ThrowsExactly<BadImageFormatException>(() => ValidateMethodSpecification([0x1A, 0x01, 0x08]));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod([0x08, 0x00, 0x08], SignatureCallerKind.MethodDefinition));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod([0x00, 0x00, 0x08], SignatureCallerKind.PropertyDefinition));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod([0x80, 0x00, 0x08], SignatureCallerKind.MethodDefinition));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod([0x60, 0x00, 0x08], SignatureCallerKind.MethodDefinition));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod([0x60, 0x00, 0x08], SignatureCallerKind.MemberReference));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod([0x10, 0x01, 0x00, 0x08], SignatureCallerKind.StandaloneSignature));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod([0x00, 0x00, 0x08], (SignatureCallerKind)int.MaxValue));
    }

    /// <summary>
    /// Verifies the TypeSpec root productions supported by the .NET ECMA-335 augment.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TypeSpecificationRoot_EnforcesRootProduction()
    {
        using var scope = SignatureMetadataScope.Create(
            typeSpecifications:
            [
                [0x0F, 0x08],
                [0x13, 0x00],
                [0x1E, 0x00],
                [0x08],
                [0x01],
                [0x16],
                [0x1F, 0x04, 0x08],
                [0x10, 0x08],
                [0x12, 0x04],
                [0x11, 0x04],
                [0x45, 0x1C],
            ]);

        for (var i = 0; i <= 7; i++)
        {
            new SignatureBlobValidator(scope.Reader).ValidateTypeSpecification(
                scope.TypeSpecifications[i]);
        }

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            new SignatureBlobValidator(scope.Reader).ValidateTypeSpecification(scope.TypeSpecifications[8]));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            new SignatureBlobValidator(scope.Reader).ValidateTypeSpecification(scope.TypeSpecifications[9]));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            new SignatureBlobValidator(scope.Reader).ValidateTypeSpecification(scope.TypeSpecifications[10]));
    }

    /// <summary>
    /// Verifies a TypeSpec-root managed pointer accepts depth 128 and rejects depth 129.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TypeSpecificationByReferenceRoot_EnforcesExactDepthBoundary()
    {
        byte[] accepted =
        [
            0x10,
            .. BuildPointerType(SignatureBlobValidator.MaxSignatureDepth - 1),
        ];
        byte[] rejected =
        [
            0x10,
            .. BuildPointerType(SignatureBlobValidator.MaxSignatureDepth),
        ];
        using var scope = SignatureMetadataScope.Create(
            typeSpecifications: [accepted, rejected]);

        new SignatureBlobValidator(scope.Reader).ValidateTypeSpecification(
            scope.TypeSpecifications[0]);
        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            new SignatureBlobValidator(scope.Reader).ValidateTypeSpecification(
                scope.TypeSpecifications[1]));

        Assert.Contains("129", exception.Message);
        Assert.Contains("128", exception.Message);
    }

    /// <summary>
    /// Verifies the context-sensitive legality of VOID terminals.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Void_IsAcceptedOnlyInLegalContexts()
    {
        ValidateMethod([0x00, 0x00, 0x01], SignatureCallerKind.MethodDefinition);
        ValidateMethod([0x08, 0x00, 0x16], SignatureCallerKind.PropertyDefinition);
        ValidateField([0x06, 0x0F, 0x01]);
        ValidateField([0x06, 0x0F, 0x16]);

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod([0x08, 0x00, 0x01], SignatureCallerKind.PropertyDefinition));
        Assert.ThrowsExactly<BadImageFormatException>(() => ValidateField([0x06, 0x01]));
        Assert.ThrowsExactly<BadImageFormatException>(() => ValidateLocal([0x07, 0x01, 0x01]));
        Assert.ThrowsExactly<BadImageFormatException>(() => ValidateMethodSpecification([0x0A, 0x01, 0x01]));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod([0x00, 0x01, 0x08, 0x01], SignatureCallerKind.MethodDefinition));
    }

    /// <summary>
    /// Verifies that SENTINEL is accepted once only in a vararg call-site signature.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Sentinel_IsAcceptedOnlyInVarArgCallSites()
    {
        ValidateMethod([0x05, 0x01, 0x08, 0x41, 0x08], SignatureCallerKind.StandaloneSignature);
        ValidateMethod([0x01, 0x01, 0x08, 0x41, 0x08], SignatureCallerKind.StandaloneSignature);
        ValidateField([0x06, 0x1B, 0x05, 0x01, 0x08, 0x41, 0x08]);
        ValidateField([0x06, 0x1B, 0x01, 0x01, 0x08, 0x41, 0x08]);

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod([0x05, 0x01, 0x08, 0x41, 0x08], SignatureCallerKind.MethodDefinition));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod([0x00, 0x01, 0x08, 0x41, 0x08], SignatureCallerKind.StandaloneSignature));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateField([0x06, 0x1B, 0x00, 0x01, 0x08, 0x41, 0x08]));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod([0x08, 0x01, 0x08, 0x41, 0x08], SignatureCallerKind.PropertyDefinition));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod([0x05, 0x02, 0x08, 0x41, 0x08, 0x41, 0x08], SignatureCallerKind.StandaloneSignature));
    }

    /// <summary>
    /// Verifies that PINNED is accepted only at a local-variable position.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Pinned_IsAcceptedOnlyInLocalSignatures()
    {
        ValidateLocal([0x07, 0x01, 0x45, 0x1C]);
        ValidateLocal([0x07, 0x01, 0x45, 0x0E]);
        ValidateLocal([0x07, 0x01, 0x45, 0x12, 0x04]);
        ValidateLocal([0x07, 0x01, 0x45, 0x1D, 0x08]);
        ValidateLocal([0x07, 0x01, 0x45, 0x14, 0x08, 0x01, 0x00, 0x00]);
        ValidateLocal([0x07, 0x01, 0x45, 0x15, 0x12, 0x04, 0x01, 0x08]);
        ValidateLocal([0x07, 0x01, 0x45, 0x10, 0x08]);
        ValidateLocal([0x07, 0x01, 0x45, 0x1F, 0x04, 0x10, 0x08]);
        ValidateLocal([0x07, 0x01, 0x45, 0x08]);
        ValidateLocal([0x07, 0x01, 0x45, 0x16]);
        ValidateLocal([0x07, 0x01, 0x45, 0x0F, 0x08]);
        ValidateLocal([0x07, 0x01, 0x45, 0x11, 0x04]);

        Assert.ThrowsExactly<BadImageFormatException>(() => ValidateField([0x06, 0x45, 0x08]));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod([0x00, 0x01, 0x08, 0x45, 0x08], SignatureCallerKind.MethodDefinition));
        Assert.ThrowsExactly<BadImageFormatException>(() => ValidateLocal([0x07, 0x01, 0x45, 0x45, 0x08]));
    }

    /// <summary>
    /// Verifies the custom-modifier placements added by the .NET ECMA-335 augment.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void CustomModifiers_AreAcceptedInEveryRuntimeTypeProduction()
    {
        ValidateMethod(
            [0x00, 0x00, 0x10, 0x1F, 0x04, 0x08],
            SignatureCallerKind.MethodDefinition);
        ValidateField([0x06, 0x14, 0x1F, 0x04, 0x08, 0x01, 0x00, 0x00]);
        ValidateField([0x06, 0x15, 0x12, 0x04, 0x01, 0x1F, 0x04, 0x08]);
        ValidateMethodSpecification([0x0A, 0x01, 0x1F, 0x04, 0x08]);
    }

    /// <summary>
    /// Verifies canonical compressed unsigned and signed integers and rejects overlong or truncated encodings.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void CompressedIntegers_MustUseCanonicalEncoding()
    {
        ValidateField([0x06, 0x13, 0x80, 0x80]);
        ValidateField([0x06, 0x14, 0x08, 0x01, 0x00, 0x01, 0x00]);

        Assert.ThrowsExactly<BadImageFormatException>(() => ValidateLocal([0x07, 0x80, 0x01, 0x08]));
        Assert.ThrowsExactly<BadImageFormatException>(() => ValidateField([0x06, 0x12, 0x80, 0x04]));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateField([0x06, 0x14, 0x08, 0x01, 0x00, 0x01, 0x80, 0x00]));
        Assert.ThrowsExactly<BadImageFormatException>(() => ValidateField([0x06, 0x13, 0x80]));
    }

    /// <summary>
    /// Verifies that otherwise valid roots reject trailing bytes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TrailingBytes_AreRejected()
    {
        var exception = Assert.ThrowsExactly<BadImageFormatException>(() => ValidateField([0x06, 0x08, 0x08]));

        Assert.Contains("trailing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies the exact array-rank boundary and the requirement that rank be positive.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ArrayRank_EnforcesZeroOneThirtyTwoThirtyThreeBoundaries()
    {
        ValidateField(BuildArrayField(rank: 1, sizeCount: 0, lowerBoundCount: 0));
        ValidateField(BuildArrayField(rank: 32, sizeCount: 0, lowerBoundCount: 0));

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateField(BuildArrayField(rank: 0, sizeCount: 0, lowerBoundCount: 0)));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateField(BuildArrayField(rank: 33, sizeCount: 0, lowerBoundCount: 0)));
    }

    /// <summary>
    /// Verifies that array size and lower-bound sequences may reach rank but may not exceed it.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ArrayShapeCounts_CannotExceedRank()
    {
        ValidateField(BuildArrayField(rank: 2, sizeCount: 2, lowerBoundCount: 2));

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateField(BuildArrayField(rank: 2, sizeCount: 3, lowerBoundCount: 0)));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateField(BuildArrayField(rank: 2, sizeCount: 0, lowerBoundCount: 3)));
    }

    /// <summary>
    /// Verifies the exact generic-instantiation argument-count boundary.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GenericArgumentCount_Enforces1024And1025Boundary()
    {
        ValidateField(BuildGenericInstantiationField(SignatureBlobValidator.MaxTypeArguments));

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateField(BuildGenericInstantiationField(SignatureBlobValidator.MaxTypeArguments + 1)));
    }

    /// <summary>Verifies a GENERICINST base is exactly CLASS/VALUETYPE followed by an in-range TypeDef/TypeRef.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GenericInstantiationBase_EnforcesNonRecursiveTypeDefOrRefGrammar()
    {
        ValidateField([0x06, 0x15, 0x12, 0x04, 0x01, 0x08]);
        ValidateField([0x06, 0x15, 0x11, 0x05, 0x01, 0x08]);

        byte[][] malformed =
        [
            [0x06, 0x15, 0x08, 0x01, 0x08],
            [0x06, 0x15, 0x0F, 0x08, 0x01, 0x08],
            [0x06, 0x15, 0x1B, 0x00, 0x00, 0x08, 0x01, 0x08],
            [0x06, 0x15, 0x15, 0x12, 0x04, 0x01, 0x08, 0x01, 0x08],
            [0x06, 0x15, 0x12, 0x00, 0x01, 0x08],
            [0x06, 0x15, 0x12, 0x06, 0x01, 0x08],
        ];
        using var scope = SignatureMetadataScope.Create(malformed, typeSpecifications: [[0x0F, 0x08]]);
        foreach (var blob in scope.Blobs)
        {
            Assert.ThrowsExactly<BadImageFormatException>(() =>
                new SignatureBlobValidator(scope.Reader).ValidateFieldSignature(blob));
        }
    }

    /// <summary>Verifies the exact cumulative expanded-work boundary.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ExpandedWork_Enforces100000And100001Boundary()
    {
        ValidateMethod(
            BuildMethodWithParameters(SignatureBlobValidator.MaxSignatureWork - 1),
            SignatureCallerKind.MethodDefinition);

        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod(
                BuildMethodWithParameters(SignatureBlobValidator.MaxSignatureWork),
                SignatureCallerKind.MethodDefinition));
        Assert.Contains("100001", exception.Message);
        Assert.Contains("100000", exception.Message);
    }

    /// <summary>Verifies the MethodSpec sequence-count limit independently of GENERICINST types.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodSpecificationArgumentCount_Enforces1024And1025Boundary()
    {
        ValidateMethodSpecification(BuildMethodSpecification(SignatureBlobValidator.MaxTypeArguments));

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethodSpecification(BuildMethodSpecification(SignatureBlobValidator.MaxTypeArguments + 1)));
    }

    /// <summary>Verifies method generic arity is bounded as a scalar, independently of following elements.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodGenericArity_Enforces1024And1025Boundary()
    {
        ValidateMethod(BuildGenericMethod(SignatureBlobValidator.MaxTypeArguments), SignatureCallerKind.MethodDefinition);

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod(
                BuildGenericMethod(SignatureBlobValidator.MaxTypeArguments + 1),
                SignatureCallerKind.MethodDefinition));
    }

    /// <summary>Verifies local and property sequences enforce the independent cumulative work boundary.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void LocalAndPropertySequences_EnforceWorkBoundary()
    {
        ValidateLocal(BuildLocalSignature(SignatureBlobValidator.MaxSignatureWork));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateLocal(BuildLocalSignature(SignatureBlobValidator.MaxSignatureWork + 1)));

        ValidateMethod(
            BuildPropertySignature(SignatureBlobValidator.MaxSignatureWork - 1),
            SignatureCallerKind.PropertyDefinition);
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateMethod(
                BuildPropertySignature(SignatureBlobValidator.MaxSignatureWork),
                SignatureCallerKind.PropertyDefinition));
    }

    /// <summary>Verifies cached TypeSpec summaries are charged on every branch of a shared DAG.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SharedTypeSpecificationDag_ChargesCachedWorkAtEveryReuse()
    {
        const int parameterCount = 33_333;
        using (var accepted = SignatureMetadataScope.Create(
            [BuildSharedTypeSpecificationWorkMethod(parameterCount, pointerReturn: false)],
            typeSpecifications: [[0x13, 0x00]]))
        {
            new SignatureBlobValidator(accepted.Reader).ValidateMethodSignature(
                accepted.Blobs[0], SignatureCallerKind.MethodDefinition);
        }

        using var rejected = SignatureMetadataScope.Create(
            [BuildSharedTypeSpecificationWorkMethod(parameterCount, pointerReturn: true)],
            typeSpecifications: [[0x13, 0x00]]);
        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            new SignatureBlobValidator(rejected.Reader).ValidateMethodSignature(
                rejected.Blobs[0], SignatureCallerKind.MethodDefinition));
        Assert.Contains("100001", exception.Message);
        Assert.Contains("100000", exception.Message);
    }

    /// <summary>
    /// Verifies nil, out-of-range, reserved-tag, and contextually illegal type handles fail closed.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TypeHandles_MustBeNonNilInRangeAndContextuallyLegal()
    {
        byte[][] blobs =
        [
            [0x06, 0x12, 0x04],
            [0x06, 0x12, 0x00],
            [0x06, 0x12, 0x08],
            [0x06, 0x12, 0x09],
            [0x06, 0x12, 0x07],
            [0x06, 0x12, 0x06],
            [0x06, 0x1F, 0x02, 0x08],
            [0x06, 0x1F, 0x0A, 0x08],
        ];
        using var scope = SignatureMetadataScope.Create(blobs, typeSpecifications: [[0x0F, 0x08]]);

        new SignatureBlobValidator(scope.Reader).ValidateFieldSignature(scope.Blobs[0]);
        for (var i = 1; i < scope.Blobs.Count; i++)
        {
            var blob = scope.Blobs[i];
            Assert.ThrowsExactly<BadImageFormatException>(() =>
                new SignatureBlobValidator(scope.Reader).ValidateFieldSignature(blob));
        }

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            new SignatureBlobValidator(scope.Reader).ValidateTypeSpecification(default));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            new SignatureBlobValidator(scope.Reader).ValidateTypeSpecification(
                MetadataTokens.TypeSpecificationHandle(2)));
    }

    /// <summary>
    /// Verifies that a TypeSpec cannot reference itself through a custom modifier.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TypeSpecification_SelfCycle_IsRejected()
    {
        using var scope = SignatureMetadataScope.Create(
            typeSpecifications: [[0x0F, 0x1F, 0x06, 0x08]]);

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            new SignatureBlobValidator(scope.Reader).ValidateTypeSpecification(scope.TypeSpecifications[0]));
    }

    /// <summary>
    /// Verifies that a two-node TypeSpec cycle is rejected.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TypeSpecification_TwoNodeCycle_IsRejected()
    {
        using var scope = SignatureMetadataScope.Create(
            typeSpecifications:
            [
                [0x0F, 0x1F, 0x0A, 0x08],
                [0x0F, 0x1F, 0x06, 0x08],
            ]);

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            new SignatureBlobValidator(scope.Reader).ValidateTypeSpecification(scope.TypeSpecifications[0]));
    }

    /// <summary>
    /// Verifies a transitive TypeSpec graph whose deepest node is exactly depth 128.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TypeSpecification_TransitiveDepth128_IsAccepted()
    {
        ValidateTransitiveTypeSpecificationGraph(exceedsLimit: false);
    }

    /// <summary>
    /// Verifies a transitive TypeSpec graph whose deepest node is depth 129 is rejected.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TypeSpecification_TransitiveDepth129_IsRejected()
    {
        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            ValidateTransitiveTypeSpecificationGraph(exceedsLimit: true));

        Assert.Contains("129", exception.Message);
        Assert.Contains("128", exception.Message);
    }

    /// <summary>
    /// Verifies that sharing one acyclic TypeSpec from sibling branches is accepted.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TypeSpecification_SharedDag_IsAccepted()
    {
        var method = BuildSharedTypeSpecificationMethod(secondParameterPointerDepth: 0);
        using var scope = SignatureMetadataScope.Create([method], typeSpecifications: [[0x0F, 0x08]]);

        new SignatureBlobValidator(scope.Reader).ValidateMethodSignature(
            scope.Blobs[0], SignatureCallerKind.MethodDefinition);
    }

    /// <summary>
    /// Verifies a cached TypeSpec first seen shallow cannot be reused beneath wrappers that push it past depth 128.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TypeSpecification_ShallowFirstDeepSecondReuse_EnforcesCachedDepth()
    {
        var accepted = BuildSharedTypeSpecificationMethod(secondParameterPointerDepth: 126);
        using (var acceptedScope = SignatureMetadataScope.Create([accepted], typeSpecifications: [[0x0F, 0x08]]))
        {
            new SignatureBlobValidator(acceptedScope.Reader).ValidateMethodSignature(
                acceptedScope.Blobs[0], SignatureCallerKind.MethodDefinition);
        }

        var rejected = BuildSharedTypeSpecificationMethod(secondParameterPointerDepth: 127);
        using var rejectedScope = SignatureMetadataScope.Create([rejected], typeSpecifications: [[0x0F, 0x08]]);
        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            new SignatureBlobValidator(rejectedScope.Reader).ValidateMethodSignature(
                rejectedScope.Blobs[0], SignatureCallerKind.MethodDefinition));

        Assert.Contains("129", exception.Message);
        Assert.Contains("128", exception.Message);
    }

    private static void ValidateRecursiveProduction(string production, int deepestDepth)
    {
        var pointerDepth = production switch
        {
            "Pointer" => deepestDepth,
            "PinnedLocal" => deepestDepth - 2,
            _ => deepestDepth - 1,
        };
        var signature = BuildRecursiveProductionSignature(production, pointerDepth);

        switch (production)
        {
            case "ByReference":
                ValidateMethod(signature, SignatureCallerKind.MethodDefinition);
                break;
            case "PinnedLocal":
                ValidateLocal(signature);
                break;
            default:
                ValidateField(signature);
                break;
        }
    }

    private static byte[] BuildRecursiveProductionSignature(string production, int pointerDepth)
    {
        var child = BuildPointerType(pointerDepth);
        var signature = new List<byte>();

        switch (production)
        {
            case "Pointer":
                signature.Add(0x06);
                signature.AddRange(child);
                break;
            case "ByReference":
                signature.AddRange([0x00, 0x00, 0x10]);
                signature.AddRange(child);
                break;
            case "SZArray":
                signature.AddRange([0x06, 0x1D]);
                signature.AddRange(child);
                break;
            case "ArrayElement":
                signature.AddRange([0x06, 0x14]);
                signature.AddRange(child);
                signature.AddRange([0x01, 0x00, 0x00]);
                break;
            case "GenericArgument":
                signature.AddRange([0x06, 0x15, 0x12, 0x04, 0x01]);
                signature.AddRange(child);
                break;
            case "FunctionPointerReturn":
                signature.AddRange([0x06, 0x1B, 0x00, 0x00]);
                signature.AddRange(child);
                break;
            case "FunctionPointerParameter":
                signature.AddRange([0x06, 0x1B, 0x00, 0x01, 0x08]);
                signature.AddRange(child);
                break;
            case "RequiredModifier":
                signature.AddRange([0x06, 0x1F, 0x04]);
                signature.AddRange(child);
                break;
            case "OptionalModifier":
                signature.AddRange([0x06, 0x20, 0x04]);
                signature.AddRange(child);
                break;
            case "PinnedLocal":
                signature.AddRange([0x07, 0x01, 0x45, 0x10]);
                signature.AddRange(child);
                break;
            default:
                Assert.Fail($"Unknown recursive production '{production}'.");
                break;
        }

        return [.. signature];
    }

    private static byte[] BuildPointerType(int pointerDepth)
    {
        var type = new byte[pointerDepth + 1];
        Array.Fill(type, (byte)0x0F, 0, pointerDepth);
        type[^1] = 0x08;
        return type;
    }

    private static byte[] BuildArrayField(int rank, int sizeCount, int lowerBoundCount)
    {
        var signature = new List<byte> { 0x06, 0x14, 0x08 };
        AddCompressedUnsigned(signature, rank);
        AddCompressedUnsigned(signature, sizeCount);
        for (var i = 0; i < sizeCount; i++)
        {
            AddCompressedUnsigned(signature, 0);
        }
        AddCompressedUnsigned(signature, lowerBoundCount);
        for (var i = 0; i < lowerBoundCount; i++)
        {
            signature.Add(0x00);
        }
        return [.. signature];
    }

    private static byte[] BuildGenericInstantiationField(int argumentCount)
    {
        var signature = new List<byte> { 0x06, 0x15, 0x12, 0x04 };
        AddCompressedUnsigned(signature, argumentCount);
        for (var i = 0; i < argumentCount; i++)
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

    private static byte[] BuildMethodSpecification(int argumentCount)
    {
        var signature = new List<byte>(argumentCount + 5) { 0x0A };
        AddCompressedUnsigned(signature, argumentCount);
        for (var i = 0; i < argumentCount; i++)
        {
            signature.Add(0x08);
        }
        return [.. signature];
    }

    private static byte[] BuildGenericMethod(int arity)
    {
        var signature = new List<byte> { 0x10 };
        AddCompressedUnsigned(signature, arity);
        signature.AddRange([0x00, 0x08]);
        return [.. signature];
    }

    private static byte[] BuildLocalSignature(int localCount)
    {
        var signature = new List<byte>(localCount + 5) { 0x07 };
        AddCompressedUnsigned(signature, localCount);
        for (var i = 0; i < localCount; i++)
        {
            signature.Add(0x08);
        }
        return [.. signature];
    }

    private static byte[] BuildPropertySignature(int parameterCount)
    {
        var signature = new List<byte>(parameterCount + 6) { 0x08 };
        AddCompressedUnsigned(signature, parameterCount);
        signature.Add(0x08);
        for (var i = 0; i < parameterCount; i++)
        {
            signature.Add(0x08);
        }
        return [.. signature];
    }

    private static byte[] BuildSharedTypeSpecificationWorkMethod(int parameterCount, bool pointerReturn)
    {
        var signature = new List<byte>(parameterCount * 3 + 8) { 0x00 };
        AddCompressedUnsigned(signature, parameterCount);
        if (pointerReturn)
        {
            signature.Add(0x0F);
        }
        signature.Add(0x08);
        for (var i = 0; i < parameterCount; i++)
        {
            signature.AddRange([0x1F, 0x06, 0x08]);
        }
        return [.. signature];
    }

    private static void ValidateTransitiveTypeSpecificationGraph(bool exceedsLimit)
    {
        const int typeSpecificationCount = 64;
        var typeSpecifications = new List<byte[]>(typeSpecificationCount);
        for (var row = 1; row < typeSpecificationCount; row++)
        {
            var signature = new List<byte> { 0x1D, 0x1F };
            AddCompressedUnsigned(signature, ((row + 1) << 2) | 0x02);
            signature.Add(0x08);
            typeSpecifications.Add([.. signature]);
        }

        typeSpecifications.Add(exceedsLimit ? [0x0F, 0x0F, 0x08] : [0x0F, 0x08]);

        var root = new List<byte> { 0x06, 0x1F };
        AddCompressedUnsigned(root, 0x06);
        root.Add(0x08);
        using var scope = SignatureMetadataScope.Create([[.. root]], typeSpecifications);
        new SignatureBlobValidator(scope.Reader).ValidateFieldSignature(scope.Blobs[0]);
    }

    private static byte[] BuildSharedTypeSpecificationMethod(int secondParameterPointerDepth)
    {
        var signature = new List<byte>
        {
            0x00,
            0x02,
            0x08,
            0x1F,
            0x06,
            0x08,
        };
        for (var i = 0; i < secondParameterPointerDepth; i++)
        {
            signature.Add(0x0F);
        }
        signature.AddRange([0x1F, 0x06, 0x08]);
        return [.. signature];
    }

    private static void AddCompressedUnsigned(List<byte> bytes, int value)
    {
        Assert.IsGreaterThanOrEqualTo(0, value);
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

    private static void ValidateField(byte[] signature)
    {
        using var scope = SignatureMetadataScope.Create([signature]);
        new SignatureBlobValidator(scope.Reader).ValidateFieldSignature(scope.Blobs[0]);
    }

    private static void ValidateLocal(byte[] signature)
    {
        using var scope = SignatureMetadataScope.Create([signature]);
        new SignatureBlobValidator(scope.Reader).ValidateLocalSignature(scope.Blobs[0]);
    }

    private static void ValidateMethodSpecification(byte[] signature)
    {
        using var scope = SignatureMetadataScope.Create([signature]);
        new SignatureBlobValidator(scope.Reader).ValidateMethodSpecificationSignature(scope.Blobs[0]);
    }

    private static void ValidateMethod(byte[] signature, SignatureCallerKind callerKind)
    {
        using var scope = SignatureMetadataScope.Create([signature]);
        new SignatureBlobValidator(scope.Reader).ValidateMethodSignature(scope.Blobs[0], callerKind);
    }
}
