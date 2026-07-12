using System.Text.RegularExpressions;

namespace Dotsider.Tests;

/// <summary>
/// Verifies the compile-time and source-level boundaries around raw SRM signature decoding.
/// </summary>
[TestClass]
public sealed partial class SignatureDecoderEnforcementTests
{
    /// <summary>Verifies every banned entity and decoder API remains listed in the analyzer policy.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BannedSymbols_ContainsCompleteDecoderSurface()
    {
        var policy = File.ReadAllText(Path.Combine(TestHelpers.GetRepoRoot(), "src", "BannedSymbols.txt"));
        string[] expectedSymbols =
        [
            "MethodDefinition.DecodeSignature``2",
            "FieldDefinition.DecodeSignature``2",
            "PropertyDefinition.DecodeSignature``2",
            "MethodSpecification.DecodeSignature``2",
            "TypeSpecification.DecodeSignature``2",
            "StandaloneSignature.DecodeMethodSignature``2",
            "StandaloneSignature.DecodeLocalSignature``2",
            "MemberReference.DecodeMethodSignature``2",
            "MemberReference.DecodeFieldSignature``2",
            "SignatureDecoder`2.DecodeFieldSignature",
            "SignatureDecoder`2.DecodeLocalSignature",
            "SignatureDecoder`2.DecodeMethodSignature",
            "SignatureDecoder`2.DecodeMethodSpecificationSignature",
            "SignatureDecoder`2.DecodeType",
        ];

        foreach (var symbol in expectedSymbols)
        {
            Assert.Contains(symbol, policy);
        }
        Assert.HasCount(expectedSymbols.Length, policy.Split('\n').Where(line => line.StartsWith("M:", StringComparison.Ordinal)));
    }

    /// <summary>Verifies raw decode calls and analyzer suppressions exist only in the facade.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ProductionSource_HasNoDecoderBypassOutsideFacade()
    {
        var sourceRoot = Path.Combine(TestHelpers.GetRepoRoot(), "src");
        var facadePath = Path.GetFullPath(Path.Combine(
            sourceRoot, "Dotsider.Core", "Analysis", "Signatures", "SafeSignatureDecoder.cs"));
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFullPath(file) == facadePath)
            {
                continue;
            }

            var source = File.ReadAllText(file);
            Assert.IsFalse(
                BannedDecoderBypassPattern().IsMatch(source),
                $"Raw signature decode bypass in {file}");
            Assert.DoesNotContain("#pragma warning disable RS0030", source, StringComparison.Ordinal);
        }

        var facade = File.ReadAllText(facadePath);
        var suppressionBlocks = SuppressionBlockPattern().Matches(facade);
        Assert.HasCount(9, suppressionBlocks);
        foreach (Match block in suppressionBlocks)
        {
            Assert.HasCount(1, RawFacadeCallPattern().Matches(block.Groups["body"].Value));
        }
    }

    [GeneratedRegex(
        @"\.DecodeSignature\s*\(|(?<!SafeSignatureDecoder)\.(?:DecodeMethodSignature|DecodeFieldSignature|DecodeLocalSignature|DecodeMethodSpecificationSignature|DecodeType)\s*\(|new\s+SignatureDecoder\s*<|SignatureDecoder\s*<[^>]+>\s*\.\s*Decode",
        RegexOptions.CultureInvariant)]
    private static partial Regex BannedDecoderBypassPattern();

    [GeneratedRegex(
        @"#pragma warning disable RS0030(?<body>.*?)#pragma warning restore RS0030",
        RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex SuppressionBlockPattern();

    [GeneratedRegex(
        @"\.(?:DecodeSignature|DecodeMethodSignature|DecodeFieldSignature|DecodeLocalSignature)\s*\(",
        RegexOptions.CultureInvariant)]
    private static partial Regex RawFacadeCallPattern();
}
