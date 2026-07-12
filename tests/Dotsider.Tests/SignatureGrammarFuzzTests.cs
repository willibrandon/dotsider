using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Signatures;

namespace Dotsider.Tests;

/// <summary>
/// Deterministic grammar generation tests that exercise bounded signatures through the production facade.
/// </summary>
[TestClass]
public sealed class SignatureGrammarFuzzTests
{
    /// <summary>
    /// Verifies every context-legal recursive production at depths 127, 128, and 129 through a facade root.
    /// </summary>
    /// <param name="root">The signature root to exercise.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("Method")]
    [DataRow("Field")]
    [DataRow("Property")]
    [DataRow("Local")]
    [DataRow("Standalone")]
    [DataRow("TypeSpec")]
    [DataRow("MethodSpec")]
    public void SeededRecursiveProductions_EnforceDepthBoundaryThroughFacade(string root)
    {
        foreach (var production in GetRecursiveProductions(root))
        {
            for (var targetDepth = 127; targetDepth <= 129; targetDepth++)
            {
                var seed = CreateSeed(root, production, targetDepth);
                var type = BuildDepthType(root, production, targetDepth, ref seed);
                using var scope = CreateScope(root, BuildRootSignature(root, type));

                if (targetDepth <= SignatureBlobValidator.MaxSignatureDepth)
                {
                    DecodeWithAssemblyProvider(root, scope);
                    DecodeWithAttributionProvider(root, scope);
                }
                else
                {
                    var assemblyException = Assert.ThrowsExactly<BadImageFormatException>(() =>
                        DecodeWithAssemblyProvider(root, scope));
                    var attributionException = Assert.ThrowsExactly<BadImageFormatException>(() =>
                        DecodeWithAttributionProvider(root, scope));

                    Assert.Contains("129", assemblyException.Message);
                    Assert.Contains("128", assemblyException.Message);
                    Assert.Contains("129", attributionException.Message);
                    Assert.Contains("128", attributionException.Message);
                }
            }
        }
    }

    /// <summary>
    /// Verifies deterministic broad-but-shallow mixed grammars through both production providers.
    /// </summary>
    /// <param name="root">The signature root to exercise.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("Method")]
    [DataRow("Field")]
    [DataRow("Property")]
    [DataRow("Local")]
    [DataRow("Standalone")]
    [DataRow("TypeSpec")]
    [DataRow("MethodSpec")]
    public void SeededBroadShallowMixedGrammars_DecodeThroughBothProviders(string root)
    {
        for (var iteration = 0; iteration < 24; iteration++)
        {
            var seed = CreateSeed(root, "BroadMixed", iteration);
            var signature = BuildBroadRootSignature(root, ref seed);
            using var scope = CreateScope(root, signature);

            DecodeWithAssemblyProvider(root, scope);
            DecodeWithAttributionProvider(root, scope);
        }
    }

    private static IReadOnlyList<string> GetRecursiveProductions(string root) => root == "Local"
        ?
        [
            "Pointer",
            "ByReference",
            "SZArray",
            "ArrayElement",
            "GenericArgument",
            "FunctionPointerReturn",
            "FunctionPointerParameter",
            "RequiredModifier",
            "OptionalModifier",
            "Pinned",
        ]
        :
        [
            "Pointer",
            "ByReference",
            "SZArray",
            "ArrayElement",
            "GenericArgument",
            "FunctionPointerReturn",
            "FunctionPointerParameter",
            "RequiredModifier",
            "OptionalModifier",
        ];

    private static byte[] BuildDepthType(
        string root,
        string production,
        int targetDepth,
        ref uint seed)
    {
        var terminal = NextPrimitive(ref seed);
        return production switch
        {
            "Pointer" => BuildPointerChain(targetDepth, terminal),
            "ByReference" when AllowsDirectByReference(root) =>
                [0x10, .. BuildPointerChain(targetDepth - 1, terminal)],
            "ByReference" =>
                [0x1B, 0x00, 0x00, 0x10, .. BuildPointerChain(targetDepth - 2, terminal)],
            "SZArray" =>
                [0x1D, .. BuildPointerChain(targetDepth - 1, terminal)],
            "ArrayElement" =>
                [0x14, .. BuildPointerChain(targetDepth - 1, terminal), 0x01, 0x00, 0x00],
            "GenericArgument" =>
                [0x15, NextGenericBaseKind(ref seed), NextTypeHandle(ref seed), 0x01,
                    .. BuildPointerChain(targetDepth - 1, terminal)],
            "FunctionPointerReturn" =>
                [0x1B, 0x00, 0x00, .. BuildPointerChain(targetDepth - 1, terminal)],
            "FunctionPointerParameter" =>
                [0x1B, 0x00, 0x01, NextPrimitive(ref seed),
                    .. BuildPointerChain(targetDepth - 1, terminal)],
            "RequiredModifier" when AllowsDirectCustomModifier(root) =>
                [0x1F, NextTypeHandle(ref seed), .. BuildPointerChain(targetDepth - 1, terminal)],
            "RequiredModifier" =>
                [0x0F, 0x1F, NextTypeHandle(ref seed), .. BuildPointerChain(targetDepth - 2, terminal)],
            "OptionalModifier" when AllowsDirectCustomModifier(root) =>
                [0x20, NextTypeHandle(ref seed), .. BuildPointerChain(targetDepth - 1, terminal)],
            "OptionalModifier" =>
                [0x0F, 0x20, NextTypeHandle(ref seed), .. BuildPointerChain(targetDepth - 2, terminal)],
            "Pinned" when root == "Local" =>
                [0x45, 0x10, .. BuildPointerChain(targetDepth - 2, terminal)],
            _ => throw new ArgumentOutOfRangeException(nameof(production)),
        };
    }

    private static byte[] BuildPointerChain(int pointerCount, byte terminal)
    {
        var type = new byte[pointerCount + 1];
        Array.Fill(type, (byte)0x0F, 0, pointerCount);
        type[^1] = terminal;
        return type;
    }

    private static bool AllowsDirectByReference(string root) =>
        root is "Method" or "Property" or "Local" or "Standalone" or "TypeSpec";

    private static bool AllowsDirectCustomModifier(string root) =>
        root is not ("TypeSpec" or "MethodSpec");

    private static byte[] BuildBroadRootSignature(string root, ref uint seed)
    {
        const int maxDepth = 6;
        if (root == "TypeSpec")
        {
            return BuildMixedComposite(ref seed, depth: 0, maxDepth);
        }
        if (root == "Field")
        {
            return [0x06, .. BuildMixedComposite(ref seed, depth: 0, maxDepth)];
        }

        var count = 3 + Next(ref seed, 6);
        var signature = new List<byte>();
        switch (root)
        {
            case "Method":
            case "Standalone":
                signature.Add(0x00);
                signature.Add((byte)count);
                signature.AddRange(BuildMixedType(ref seed, depth: 0, maxDepth));
                for (var i = 0; i < count; i++)
                {
                    signature.AddRange(BuildMixedType(ref seed, depth: 0, maxDepth));
                }
                break;
            case "Property":
                signature.Add(0x08);
                signature.Add((byte)count);
                signature.AddRange(BuildMixedType(ref seed, depth: 0, maxDepth));
                for (var i = 0; i < count; i++)
                {
                    signature.AddRange(BuildMixedType(ref seed, depth: 0, maxDepth));
                }
                break;
            case "Local":
                signature.Add(0x07);
                signature.Add((byte)count);
                for (var i = 0; i < count; i++)
                {
                    signature.AddRange(BuildMixedType(ref seed, depth: 0, maxDepth));
                }
                break;
            case "MethodSpec":
                signature.Add(0x0A);
                signature.Add((byte)count);
                for (var i = 0; i < count; i++)
                {
                    signature.AddRange(BuildMixedType(ref seed, depth: 0, maxDepth));
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(root));
        }

        return [.. signature];
    }

    private static byte[] BuildMixedType(ref uint seed, int depth, int maxDepth)
    {
        if (depth >= maxDepth || Next(ref seed, 5) == 0)
        {
            return [NextPrimitive(ref seed)];
        }
        return BuildMixedComposite(ref seed, depth, maxDepth);
    }

    private static byte[] BuildMixedComposite(ref uint seed, int depth, int maxDepth)
    {
        switch (Next(ref seed, 5))
        {
            case 0:
                return [0x0F, .. BuildMixedType(ref seed, depth + 1, maxDepth)];
            case 1:
                return [0x1D, .. BuildMixedType(ref seed, depth + 1, maxDepth)];
            case 2:
                {
                    var rank = 1 + Next(ref seed, 3);
                    return [0x14, .. BuildMixedType(ref seed, depth + 1, maxDepth), (byte)rank, 0x00, 0x00];
                }
            case 3:
                {
                    var argumentCount = 1 + Next(ref seed, 3);
                    var result = new List<byte>
                {
                    0x15,
                    NextGenericBaseKind(ref seed),
                    NextTypeHandle(ref seed),
                    (byte)argumentCount,
                };
                    for (var i = 0; i < argumentCount; i++)
                    {
                        result.AddRange(BuildMixedType(ref seed, depth + 1, maxDepth));
                    }
                    return [.. result];
                }
            default:
                {
                    var parameterCount = Next(ref seed, 3);
                    var result = new List<byte> { 0x1B, 0x00, (byte)parameterCount };
                    result.AddRange(BuildMixedType(ref seed, depth + 1, maxDepth));
                    for (var i = 0; i < parameterCount; i++)
                    {
                        result.AddRange(BuildMixedType(ref seed, depth + 1, maxDepth));
                    }
                    return [.. result];
                }
        }
    }

    private static byte[] BuildRootSignature(string root, byte[] type) => root switch
    {
        "Method" or "Standalone" => [0x00, 0x00, .. type],
        "Field" => [0x06, .. type],
        "Property" => [0x08, 0x00, .. type],
        "Local" => [0x07, 0x01, .. type],
        "TypeSpec" => type,
        "MethodSpec" => [0x0A, 0x01, .. type],
        _ => throw new ArgumentOutOfRangeException(nameof(root)),
    };

    private static FacadeSignatureMetadataScope CreateScope(string root, byte[] signature) => root switch
    {
        "Method" => FacadeSignatureMetadataScope.Create(method: signature),
        "Field" => FacadeSignatureMetadataScope.Create(field: signature),
        "Property" => FacadeSignatureMetadataScope.Create(property: signature),
        "Local" => FacadeSignatureMetadataScope.Create(local: signature),
        "Standalone" => FacadeSignatureMetadataScope.Create(standaloneMethod: signature),
        "TypeSpec" => FacadeSignatureMetadataScope.Create(typeSpecifications: [signature]),
        "MethodSpec" => FacadeSignatureMetadataScope.Create(methodSpecification: signature),
        _ => throw new ArgumentOutOfRangeException(nameof(root)),
    };

    private static void DecodeWithAssemblyProvider(string root, FacadeSignatureMetadataScope scope)
    {
        var reader = scope.Reader;
        var provider = new AssemblySignatureTypeProvider();
        _ = root switch
        {
            "Method" => SafeSignatureDecoder.DecodeMethodSignature(
                reader,
                scope.MethodDefinition,
                provider,
                genericContext: default).ReturnType,
            "Field" => SafeSignatureDecoder.DecodeFieldSignature(
                reader,
                scope.FieldDefinition,
                provider,
                genericContext: default),
            "Property" => SafeSignatureDecoder.DecodePropertySignature(
                reader,
                scope.PropertyDefinition,
                provider,
                genericContext: default).ReturnType,
            "Local" => SafeSignatureDecoder.DecodeLocalSignature(
                reader,
                scope.LocalSignature,
                provider,
                genericContext: default)[0],
            "Standalone" => SafeSignatureDecoder.DecodeStandaloneMethodSignature(
                reader,
                scope.StandaloneMethod,
                provider,
                genericContext: default).ReturnType,
            "TypeSpec" => SafeSignatureDecoder.DecodeType(
                reader,
                scope.TypeSpecifications[0],
                provider,
                genericContext: default),
            "MethodSpec" => SafeSignatureDecoder.DecodeMethodSpecificationSignature(
                reader,
                scope.MethodSpecification,
                provider,
                genericContext: default)[0],
            _ => throw new ArgumentOutOfRangeException(nameof(root)),
        };
    }

    private static void DecodeWithAttributionProvider(string root, FacadeSignatureMetadataScope scope)
    {
        var reader = scope.Reader;
        var provider = new EntityResolver(reader);
        _ = root switch
        {
            "Method" => SafeSignatureDecoder.DecodeMethodSignature(
                reader,
                scope.MethodDefinition,
                provider,
                genericContext: default).ReturnType,
            "Field" => SafeSignatureDecoder.DecodeFieldSignature(
                reader,
                scope.FieldDefinition,
                provider,
                genericContext: default),
            "Property" => SafeSignatureDecoder.DecodePropertySignature(
                reader,
                scope.PropertyDefinition,
                provider,
                genericContext: default).ReturnType,
            "Local" => SafeSignatureDecoder.DecodeLocalSignature(
                reader,
                scope.LocalSignature,
                provider,
                genericContext: default)[0],
            "Standalone" => SafeSignatureDecoder.DecodeStandaloneMethodSignature(
                reader,
                scope.StandaloneMethod,
                provider,
                genericContext: default).ReturnType,
            "TypeSpec" => SafeSignatureDecoder.DecodeType(
                reader,
                scope.TypeSpecifications[0],
                provider,
                genericContext: default),
            "MethodSpec" => SafeSignatureDecoder.DecodeMethodSpecificationSignature(
                reader,
                scope.MethodSpecification,
                provider,
                genericContext: default)[0],
            _ => throw new ArgumentOutOfRangeException(nameof(root)),
        };
    }

    private static byte NextPrimitive(ref uint seed) => Next(ref seed, 4) switch
    {
        0 => 0x02,
        1 => 0x08,
        2 => 0x0E,
        _ => 0x1C,
    };

    private static byte NextGenericBaseKind(ref uint seed) =>
        Next(ref seed, 2) == 0 ? (byte)0x11 : (byte)0x12;

    private static byte NextTypeHandle(ref uint seed) =>
        Next(ref seed, 2) == 0 ? (byte)0x08 : (byte)0x05;

    private static uint CreateSeed(string root, string production, int discriminator)
    {
        var seed = 2_166_136_261u;
        foreach (var value in root)
        {
            seed = (seed ^ value) * 16_777_619u;
        }
        foreach (var value in production)
        {
            seed = (seed ^ value) * 16_777_619u;
        }
        seed = (seed ^ (uint)discriminator) * 16_777_619u;
        return seed == 0 ? 0x9E37_79B9u : seed;
    }

    private static int Next(ref uint seed, int exclusiveMaximum)
    {
        seed ^= seed << 13;
        seed ^= seed >> 17;
        seed ^= seed << 5;
        return (int)(seed % (uint)exclusiveMaximum);
    }
}
