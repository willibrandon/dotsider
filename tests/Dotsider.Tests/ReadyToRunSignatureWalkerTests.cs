using Dotsider.Core.Analysis.ReadyToRun;

namespace Dotsider.Tests;

/// <summary>
/// Synthetic ReadyToRun signature walker regressions for metadata-scope transitions.
/// </summary>
[TestClass]
public class ReadyToRunSignatureWalkerTests
{
    /// <summary>
    /// Verifies that the walker accepts exactly 128 nested type edges.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TypeNestingDepth_AtLimit_IsAccepted()
    {
        var signature = BuildMethodInstantiationSignature(BuildPointerType(128));

        var result = ReadyToRunSignatureWalker.ParseMethod(new R2RNativeReader(signature), 0, null);

        Assert.AreEqual(signature.Length, result.Offset);
    }

    /// <summary>
    /// Verifies that the walker rejects the 129th nested type edge and reports the exact boundary.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TypeNestingDepth_AboveLimit_IsRejected()
    {
        var signature = BuildMethodInstantiationSignature(BuildPointerType(129));

        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            ReadyToRunSignatureWalker.ParseMethod(new R2RNativeReader(signature), 0, null));

        Assert.Contains("129", exception.Message);
        Assert.Contains("128", exception.Message);
    }

    /// <summary>
    /// Verifies that a module metadata transition does not reset the current type depth.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TypeNestingDepth_ModuleTransition_PreservesDepth()
    {
        var type = new List<byte> { 0x3f, 0x01 };
        type.AddRange(BuildPointerType(128));
        var signature = BuildMethodInstantiationSignature([.. type]);

        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            ReadyToRunSignatureWalker.ParseMethod(new R2RNativeReader(signature), 0, null, _ => null));

        Assert.Contains("129", exception.Message);
        Assert.Contains("128", exception.Message);
    }

    /// <summary>
    /// Verifies that completing one type restores the depth before the next sibling is walked.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TypeNestingDepth_Siblings_HaveIndependentDepth()
    {
        var signature = BuildMethodInstantiationSignature(BuildPointerType(128), BuildPointerType(128));

        var result = ReadyToRunSignatureWalker.ParseMethod(new R2RNativeReader(signature), 0, null);

        Assert.AreEqual(signature.Length, result.Offset);
    }

    /// <summary>Verifies every recursive ReadyToRun type production rejects a depth-128 child.</summary>
    /// <param name="production">The recursive production under test.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("Pointer")]
    [DataRow("ByReference")]
    [DataRow("SZArray")]
    [DataRow("Pinned")]
    [DataRow("NativeValueType")]
    [DataRow("RequiredModifier")]
    [DataRow("OptionalModifier")]
    [DataRow("ArrayElement")]
    [DataRow("GenericType")]
    [DataRow("GenericArgument")]
    [DataRow("Module")]
    [DataRow("FunctionPointerReturn")]
    [DataRow("FunctionPointerParameter")]
    public void RecursiveProduction_WithDepth128Child_Rejects129thEntry(string production)
    {
        var recursiveType = WrapRecursiveProduction(production, BuildPointerType(128));
        var signature = BuildMethodInstantiationSignature(recursiveType);

        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            ReadyToRunSignatureWalker.ParseMethod(new R2RNativeReader(signature), 0, null, _ => null));

        Assert.Contains("129", exception.Message);
        Assert.Contains("128", exception.Message);
    }

    /// <summary>Verifies 128 repeated module-scope edges remain within the limit.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RepeatedModuleZapsig_AtDepthLimit_IsAccepted()
    {
        byte[] type = [0x08];
        for (var i = 0; i < 128; i++)
        {
            type = [0x3F, 0x01, .. type];
        }
        var signature = BuildMethodInstantiationSignature(type);

        var result = ReadyToRunSignatureWalker.ParseMethod(
            new R2RNativeReader(signature), 0, null, _ => null);

        Assert.AreEqual(signature.Length, result.Offset);
    }

    /// <summary>Verifies 128 nested generic-argument edges remain within the limit.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NestedGenericArguments_AtDepthLimit_IsAccepted()
    {
        byte[] type = [0x08];
        for (var i = 0; i < 128; i++)
        {
            type = [0x15, 0x12, 0x04, 0x01, .. type];
        }
        var signature = BuildMethodInstantiationSignature(type);

        var result = ReadyToRunSignatureWalker.ParseMethod(new R2RNativeReader(signature), 0, null);

        Assert.AreEqual(signature.Length, result.Offset);
    }

    /// <summary>
    /// Verifies that an unsupported element type is rejected instead of producing plausible output.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void UnsupportedElementType_IsRejected()
    {
        var signature = BuildMethodInstantiationSignature([0x21]);

        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            ReadyToRunSignatureWalker.ParseMethod(new R2RNativeReader(signature), 0, null));

        Assert.Contains("0x21", exception.Message);
    }

    /// <summary>Verifies CLASS and VALUETYPE reject token kinds other than TypeDef and TypeRef.</summary>
    /// <param name="elementType">The CLASS or VALUETYPE element code.</param>
    /// <param name="encodedToken">The compressed ReadyToRun type token.</param>
    /// <param name="expectedKind">The invalid decoded metadata handle kind.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(0x12, 0x06, "TypeSpecification")]
    [DataRow(0x11, 0x07, "ModuleDefinition")]
    public void TypeToken_WrongKind_IsRejected(int elementType, int encodedToken, string expectedKind)
    {
        var signature = BuildMethodInstantiationSignature([(byte)elementType, (byte)encodedToken]);

        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            ReadyToRunSignatureWalker.ParseMethod(new R2RNativeReader(signature), 0, null));

        Assert.Contains(expectedKind, exception.Message);
    }

    /// <summary>Verifies TypeDef and TypeRef tokens must identify an existing metadata row.</summary>
    /// <param name="elementType">The CLASS or VALUETYPE element code.</param>
    /// <param name="encodedToken">The compressed ReadyToRun type token.</param>
    /// <param name="expectedKind">The decoded metadata handle kind.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(0x12, 0x0C, "TypeDefinition")]
    [DataRow(0x11, 0x05, "TypeReference")]
    public void TypeToken_OutOfRange_IsRejected(int elementType, int encodedToken, string expectedKind)
    {
        using var metadata = new ReadyToRunMetadataScope(
            SyntheticMetadataBuilder.BuildAssemblyWithMethodTokens("TokenRows"));
        var signature = BuildMethodInstantiationSignature([(byte)elementType, (byte)encodedToken]);

        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            ReadyToRunSignatureWalker.ParseMethod(
                new R2RNativeReader(signature), 0, metadata.Reader));

        Assert.Contains(expectedKind, exception.Message);
        Assert.Contains("exceeds", exception.Message);
    }

    /// <summary>Verifies MethodDef and MemberRef signatures reject nil and out-of-range rows.</summary>
    /// <param name="memberReference">Whether the signature identifies a MemberRef rather than a MethodDef.</param>
    /// <param name="row">The invalid metadata row encoded by the signature.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(false, 0)]
    [DataRow(false, 2)]
    [DataRow(true, 0)]
    [DataRow(true, 2)]
    public void MethodToken_InvalidRow_IsRejected(bool memberReference, int row)
    {
        using var metadata = new ReadyToRunMetadataScope(
            SyntheticMetadataBuilder.BuildAssemblyWithMethodTokens("MethodTokenRows"));
        byte[] signature = memberReference
            ? [0x10, (byte)row]
            : [0x00, (byte)row];

        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            ReadyToRunSignatureWalker.ParseMethod(
                new R2RNativeReader(signature), 0, metadata.Reader));

        Assert.Contains(memberReference ? "MemberReference" : "MethodDefinition", exception.Message);
        Assert.Contains(row.ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Message);
    }

    /// <summary>Verifies valid MethodDef and MemberRef rows retain their exact token kinds.</summary>
    /// <param name="memberReference">Whether the signature identifies a MemberRef rather than a MethodDef.</param>
    /// <param name="expectedToken">The expected ECMA-335 entity token.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(false, 0x0600_0001)]
    [DataRow(true, 0x0A00_0001)]
    public void MethodToken_ValidRow_IsAccepted(bool memberReference, int expectedToken)
    {
        using var metadata = new ReadyToRunMetadataScope(
            SyntheticMetadataBuilder.BuildAssemblyWithMethodTokens("ValidMethodTokenRows"));
        byte[] signature = memberReference
            ? [0x10, 0x01]
            : [0x00, 0x01];

        var parsed = ReadyToRunSignatureWalker.ParseMethod(
            new R2RNativeReader(signature), 0, metadata.Reader);

        Assert.AreEqual(expectedToken, parsed.MethodToken);
        Assert.AreEqual(signature.Length, parsed.Offset);
    }

    /// <summary>Verifies a slot-form method intentionally produces no metadata token.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodToken_SlotForm_PreservesZeroToken()
    {
        using var metadata = new ReadyToRunMetadataScope(
            SyntheticMetadataBuilder.BuildAssemblyWithMethodTokens("SlotForm"));

        var signature = ReadyToRunSignatureWalker.ParseMethod(
            new R2RNativeReader(new byte[] { 0x08, 0x00 }), 0, metadata.Reader);

        Assert.AreEqual(0, signature.MethodToken);
    }

    /// <summary>Verifies all non-structural method modifiers, including AsyncVariant, are accepted.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodFlags_AsyncVariant_IsAccepted()
    {
        using var metadata = new ReadyToRunMetadataScope(
            SyntheticMetadataBuilder.BuildAssemblyWithMethodTokens("AsyncVariant"));
        var flags = EncodeCompressedUInt(0x0103); // AsyncVariant | InstantiatingStub | UnboxingStub
        byte[] signature = [.. flags, 0x01];

        var parsed = ReadyToRunSignatureWalker.ParseMethod(
            new R2RNativeReader(signature), 0, metadata.Reader);

        Assert.AreEqual(0x0600_0001, parsed.MethodToken);
        Assert.AreEqual(signature.Length, parsed.Offset);
    }

    /// <summary>Verifies unknown method-signature flag bits fail closed.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodFlags_UnknownBit_IsRejected()
    {
        var flags = EncodeCompressedUInt(0x0200);
        byte[] signature = [.. flags, 0x01];

        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            ReadyToRunSignatureWalker.ParseMethod(new R2RNativeReader(signature), 0, null));

        Assert.Contains("unknown flags", exception.Message);
        Assert.Contains("0x200", exception.Message);
    }

    /// <summary>Verifies slot-form and MemberRef-form flags cannot describe the same method token.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodFlags_SlotAndMemberRef_AreRejected()
    {
        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            ReadyToRunSignatureWalker.ParseMethod(
                new R2RNativeReader(new byte[] { 0x18, 0x01 }), 0, null));

        Assert.Contains("cannot combine", exception.Message);
    }

    /// <summary>
    /// Verifies a module-wrapped generic-instantiation type decodes its generic type in the referenced
    /// module while decoding type arguments in the outer signature scope, without attributing the
    /// method token to that module.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ModuleZapsig_GenericInstantiation_ParsesArgumentsInOuterMetadataScope()
    {
        using var outer = new ReadyToRunMetadataScope(
            SyntheticMetadataBuilder.BuildAssemblyWithMethodTokens("Outer", "CurrentGeneric", "OuterArg"));
        using var module = new ReadyToRunMetadataScope(
            SyntheticMetadataBuilder.BuildAssembly("Module", "ExternalGeneric", "WrongArg"));

        byte[] signature =
        [
            0x04,       // READYTORUN_METHOD_SIG_MethodInstantiation
            0x01,       // MethodDef rid 1
            0x01,       // one method-instantiation argument
            0x3f, 0x02, // MODULE_ZAPSIG module 2
            0x15,       // GENERICINST
            0x12, 0x08, // CLASS TypeDef rid 2 => ExternalGeneric in module scope
            0x01,       // one generic type argument
            0x12, 0x0c  // CLASS TypeDef rid 3 => OuterArg in outer scope
        ];

        var sig = ReadyToRunSignatureWalker.ParseMethod(
            new R2RNativeReader(signature), 0, outer.Reader, i => i == 2 ? module.Reader : null);

        Assert.AreEqual(signature.Length, sig.Offset);
        Assert.AreEqual(0x0600_0001, sig.MethodToken);
        Assert.IsFalse(sig.CrossModule);
        Assert.AreEqual(-1, sig.ModuleIndex);
        Assert.AreEqual("<ExternalGeneric<OuterArg>>", sig.InstantiationDisplay);
    }

    /// <summary>
    /// Verifies unresolved module-zapsig type arguments are neither resolved against the outer metadata
    /// nor mistaken for an override of the method token's metadata scope.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ModuleZapsig_UnresolvedModule_DoesNotResolveAgainstOuterMetadata()
    {
        using var outer = new ReadyToRunMetadataScope(
            SyntheticMetadataBuilder.BuildAssemblyWithMethodTokens("Outer", "WrongType"));

        byte[] signature =
        [
            0x04,       // READYTORUN_METHOD_SIG_MethodInstantiation
            0x01,       // MethodDef rid 1
            0x01,       // one method-instantiation argument
            0x3f, 0x07, // MODULE_ZAPSIG module 7, intentionally unresolved
            0x12, 0x08  // CLASS TypeDef rid 2; this is WrongType in the outer metadata
        ];

        var sig = ReadyToRunSignatureWalker.ParseMethod(new R2RNativeReader(signature), 0, outer.Reader);

        Assert.AreEqual("<Type>", sig.InstantiationDisplay);
        Assert.AreEqual(-1, sig.ModuleIndex);
        Assert.IsFalse(sig.CrossModule);
    }

    /// <summary>
    /// Verifies a leading module override on the owner type selects the method token's metadata
    /// scope, matching <c>ReadyToRunReader.GetMetadataReaderFromModuleOverride</c>.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void OwnerType_LeadingModuleZapsig_AttributesMethodToOwningModule()
    {
        using var outer = new ReadyToRunMetadataScope(
            SyntheticMetadataBuilder.BuildAssembly("Outer", "WrongOwner"));
        using var module = new ReadyToRunMetadataScope(
            SyntheticMetadataBuilder.BuildAssemblyWithMethodTokens("Module", "ExternalOwner"));

        byte[] signature =
        [
            0x40,       // READYTORUN_METHOD_SIG_OwnerType
            0x3F, 0x02, // MODULE_ZAPSIG module 2
            0x12, 0x08, // CLASS TypeDef rid 2 => ExternalOwner in module scope
            0x01        // MethodDef rid 1
        ];

        var sig = ReadyToRunSignatureWalker.ParseMethod(
            new R2RNativeReader(signature), 0, outer.Reader, i => i == 2 ? module.Reader : null);

        Assert.AreEqual(signature.Length, sig.Offset);
        Assert.AreEqual(0x0600_0001, sig.MethodToken);
        Assert.IsTrue(sig.CrossModule);
        Assert.AreEqual(2, sig.ModuleIndex);
    }

    private static byte[] BuildMethodInstantiationSignature(params byte[][] arguments)
    {
        var signature = new List<byte>
        {
            0x04, // READYTORUN_METHOD_SIG_MethodInstantiation
            0x01, // MethodDef rid 1
            (byte)arguments.Length,
        };

        foreach (var argument in arguments)
        {
            signature.AddRange(argument);
        }

        return [.. signature];
    }

    private static byte[] BuildPointerType(int nestingDepth)
    {
        var type = new byte[nestingDepth + 1];
        Array.Fill(type, (byte)0x0f, 0, nestingDepth);
        type[^1] = 0x08;
        return type;
    }

    private static byte[] EncodeCompressedUInt(uint value)
    {
        if (value <= 0x7F)
        {
            return [(byte)value];
        }

        if (value <= 0x3FFF)
        {
            return [(byte)(0x80 | value >> 8), (byte)value];
        }

        throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static byte[] WrapRecursiveProduction(string production, byte[] child) => production switch
    {
        "Pointer" => [0x0F, .. child],
        "ByReference" => [0x10, .. child],
        "SZArray" => [0x1D, .. child],
        "Pinned" => [0x45, .. child],
        "NativeValueType" => [0x3D, .. child],
        "RequiredModifier" => [0x1F, 0x04, .. child],
        "OptionalModifier" => [0x20, 0x04, .. child],
        "ArrayElement" => [0x14, .. child, 0x01, 0x00, 0x00],
        "GenericType" => [0x15, .. child, 0x00],
        "GenericArgument" => [0x15, 0x12, 0x04, 0x01, .. child],
        "Module" => [0x3F, 0x01, .. child],
        "FunctionPointerReturn" => [0x1B, 0x00, 0x00, .. child],
        "FunctionPointerParameter" => [0x1B, 0x00, 0x01, 0x08, .. child],
        _ => throw new ArgumentOutOfRangeException(nameof(production)),
    };

}
