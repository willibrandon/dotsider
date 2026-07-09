namespace Dotsider.Mcp.Tests;

/// <summary>
/// Behavior tests for the ReadyToRun MCP surface against the real crossgen2 fixture: the dedicated
/// <c>correlate_r2r_method</c> tool (a method resolved to its IL and precompiled native, and the
/// ambiguous-name error path) and <c>get_native_disassembly</c> made R2R-method-aware — a multi-range
/// method resolves to the method and renders all its ranges rather than a false per-range ambiguity.
/// </summary>
[TestClass]
public class ReadyToRunToolsTests : McpServerTestBase
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private const string SkipReason = "ReadyToRun crossgen2 publish did not run on this leg.";

    /// <summary>correlate_r2r_method resolves a unique method to its report with IL and native code.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task CorrelateR2rMethod_ByName_ReturnsReport()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "correlate_r2r_method",
            new Dictionary<string, object?>
            {
                ["methodOrAddress"] = "Greeter.get_Name",
                ["assemblyPath"] = Samples.ReadyToRunConsoleDll,
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.IsFalse(text!.StartsWith("Error", StringComparison.Ordinal), "the tool reported an error");
        Assert.Contains("get_Name", text);
        // The JSON report carries the honest native-availability state (camelCase enum) and the IL.
        Assert.Contains("precompiled", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"il\"", text);
    }

    /// <summary>correlate_r2r_method reports an overloaded name as an ambiguity, never first-match.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task CorrelateR2rMethod_Overloaded_ReturnsAmbiguity()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "correlate_r2r_method",
            new Dictionary<string, object?>
            {
                ["methodOrAddress"] = "Greet",
                ["assemblyPath"] = Samples.ReadyToRunConsoleDll,
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.Contains("ambiguous", text!.ToLowerInvariant());
    }

    /// <summary>get_native_disassembly renders every range of a multi-range R2R method (no false ambiguity).</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetNativeDisassembly_ReadyToRun_MultiRange_RendersAllRanges()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_native_disassembly",
            new Dictionary<string, object?>
            {
                ["symbolName"] = "MoveNext",
                ["assemblyPath"] = Samples.ReadyToRunConsoleDll,
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        // Not an ambiguity error, and the import resolver named a cross-call target in the body.
        Assert.DoesNotContain("ambiguous", text!.ToLowerInvariant());
        Assert.Contains("WriteLine", text);
    }
}
