using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Reflection.Metadata.Ecma335;

namespace Dotsider.Tests;

/// <summary>
/// Verifies each migrated production caller degrades deterministically after facade rejection.
/// </summary>
[TestClass]
public sealed class MalformedSignatureFallbackTests
{
    /// <summary>Verifies AssemblyAnalyzer preserves rows while replacing malformed signatures.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AssemblyAnalyzer_MalformedMethodAndFieldSignatures_UseExactFallbacks()
    {
        using var scope = FacadeSignatureMetadataScope.Create(
            method: [0x08, 0x00, 0x08],
            field: [0x06, 0x01]);
        using var analyzer = new AssemblyAnalyzer(scope.Image, "MalformedSignatures.dll");

        Assert.HasCount(1, analyzer.MethodDefs);
        Assert.HasCount(1, analyzer.FieldDefs);
        var method = analyzer.MethodDefs[0];
        var field = analyzer.FieldDefs[0];
        Assert.AreEqual("(?)", method.Signature);
        Assert.AreEqual(string.Empty, field.Signature);
    }

    /// <summary>Verifies IlNavigationResolver refuses semantic matching after malformed MemberRef input.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IlNavigationResolver_MalformedMemberReferenceSignature_IsUnresolved()
    {
        using var scope = FacadeSignatureMetadataScope.Create(
            memberReferenceMethod: [0x08, 0x00, 0x08]);
        using var analyzer = new AssemblyAnalyzer(scope.Image, "MalformedNavigation.dll");
        var token = MetadataTokens.GetToken(scope.MemberReferenceMethod);

        var target = IlNavigationResolver.Resolve(analyzer, token);

        var unresolved = Assert.IsExactInstanceOfType<IlNavigationTarget.Unresolved>(target);
        Assert.Contains("malformed signature", unresolved.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies navigation fails closed when valid MemberRef and MethodSpec blobs reference an
    /// invalid metadata nesting chain instead of resolving a plausible local member.
    /// </summary>
    /// <param name="malformedChain">The malformed TypeRef-chain shape.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("Cycle")]
    [DataRow("DepthExceeded")]
    [DataRow("CorruptName")]
    public void IlNavigationResolver_MalformedSignatureTypeChain_IsFailClosed(string malformedChain)
    {
        var image = MetadataNestingConsumerMetadata.BuildMalformedSignatureChainAssembly(
            malformedChain);
        using var analyzer = new AssemblyAnalyzer(image, "MalformedSignatureChain.dll");
        Assert.HasCount(1, analyzer.MethodDefs);
        Assert.HasCount(1, analyzer.FieldDefs);

        var methodMember = IlNavigationResolver.Resolve(
            analyzer,
            MetadataTokens.GetToken(MetadataTokens.MemberReferenceHandle(1)));
        var fieldMember = IlNavigationResolver.Resolve(
            analyzer,
            MetadataTokens.GetToken(MetadataTokens.MemberReferenceHandle(2)));
        var methodSpecification = IlNavigationResolver.Resolve(
            analyzer,
            MetadataTokens.GetToken(MetadataTokens.MethodSpecificationHandle(1)));

        var unresolvedMethod = Assert.IsExactInstanceOfType<IlNavigationTarget.Unresolved>(
            methodMember);
        var unresolvedField = Assert.IsExactInstanceOfType<IlNavigationTarget.Unresolved>(
            fieldMember);
        var unresolvedSpecification =
            Assert.IsExactInstanceOfType<IlNavigationTarget.GenericInstantiation>(
                methodSpecification);
        Assert.Contains(
            "malformed signature",
            unresolvedMethod.Reason,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "malformed signature",
            unresolvedField.Reason,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "invalid resolution-scope chain",
            unresolvedSpecification.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies the mstat provider preserves name identity while dropping a malformed overload signature.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void EntityResolver_MalformedMemberReferenceSignature_DegradesToNameOnly()
    {
        using var scope = FacadeSignatureMetadataScope.Create(
            memberReferenceMethod: [0x08, 0x00, 0x08]);
        var resolver = new EntityResolver(scope.Reader);

        var member = resolver.ResolveMethod(MetadataTokens.GetToken(scope.MemberReferenceMethod));

        Assert.AreEqual("ReferencedMethod", member.Name);
        Assert.AreEqual(string.Empty, member.Signature);
        Assert.AreNotEqual(TypeAttribution.Unknown, member.Type);
    }

    /// <summary>Verifies AssemblyDiffer fails closed when a non-nil local signature is malformed.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AssemblyDiffer_MalformedLocalSignature_IsDifferent()
    {
        using var scope = FacadeSignatureMetadataScope.Create(
            local: [0x07, 0x01, 0x01],
            emitMethodBody: true);
        using var analyzer = new AssemblyAnalyzer(scope.Image, "MalformedLocals.dll");
        Assert.HasCount(1, analyzer.MethodDefs);
        var method = analyzer.MethodDefs[0];
        var body = analyzer.GetMethodBody(method);
        Assert.IsNotNull(body);

        Assert.IsTrue(AssemblyDiffer.LocalSignaturesDiffer(
            analyzer.GetMetadataReader(), body, analyzer.GetMetadataReader(), body));
    }

    /// <summary>
    /// Verifies identical local blobs that reference invalid metadata chains fail closed as
    /// different instead of comparing equal through token-placeholder strings.
    /// </summary>
    /// <param name="malformedChain">The malformed TypeRef-chain shape.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("Cycle")]
    [DataRow("DepthExceeded")]
    [DataRow("CorruptName")]
    public void AssemblyDiffer_MalformedLocalTypeChain_IsDifferent(string malformedChain)
    {
        var image = MetadataNestingConsumerMetadata.BuildMalformedSignatureChainAssembly(
            malformedChain);
        using var analyzer = new AssemblyAnalyzer(image, "MalformedSignatureChain.dll");
        Assert.HasCount(1, analyzer.MethodDefs);
        var method = analyzer.MethodDefs[0];
        var body = analyzer.GetMethodBody(method);
        Assert.IsNotNull(body);
        Assert.IsFalse(body.LocalSignature.IsNil);

        var reader = analyzer.GetMetadataReader();
        Assert.IsNotNull(reader);
        Assert.IsTrue(AssemblyDiffer.LocalSignaturesDiffer(reader, body, reader, body));
    }

    /// <summary>Verifies IlDisassembler still returns IL when local-signature decoding fails.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IlDisassembler_MalformedLocalSignature_PreservesInstructions()
    {
        using var scope = FacadeSignatureMetadataScope.Create(
            local: [0x07, 0x01, 0x01],
            emitMethodBody: true);
        using var analyzer = new AssemblyAnalyzer(scope.Image, "MalformedLocals.dll");
        Assert.HasCount(1, analyzer.MethodDefs);
        var method = analyzer.MethodDefs[0];

        var instructions = new IlDisassembler(analyzer).Disassemble(method);

        Assert.HasCount(1, instructions);
        var instruction = instructions[0];
        Assert.AreEqual("ret", instruction.OpCode);
    }
}
