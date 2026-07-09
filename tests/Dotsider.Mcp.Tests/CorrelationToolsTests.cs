namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the pre-ILC correlation MCP tool: <c>correlate_method</c> against the real Native
/// AOT fixture — the counts summary on <c>get_assembly_info</c>, a unique method resolved by name
/// and by address, and the ambiguous-name error path.
/// </summary>
[TestClass]
public class CorrelationToolsTests : McpServerTestBase
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// get_assembly_info carries the cheap pre-ILC probe summary for a Native AOT binary.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetAssemblyInfo_NativeAot_CarriesPreIlcSummary()
    {
        TestSkip.When(Samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.Contains("preIlc", text);
        Assert.Contains("hasAttachableCompanion", text);
    }

    /// <summary>
    /// correlate_method resolves a unique method by name and returns its IL and status.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task CorrelateMethod_ByName_ReturnsReport()
    {
        TestSkip.When(Samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "correlate_method",
            new Dictionary<string, object?>
            {
                ["methodOrAddress"] = "Greeter.Describe",
                ["assemblyPath"] = Samples.NativeAotConsoleExe
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.IsFalse(text.StartsWith("Error", StringComparison.Ordinal), "the tool reported an error");
        Assert.Contains("\"method\"", text);
        Assert.Contains("Greeter::Describe", text);
        Assert.Contains("\"il\"", text);
    }

    /// <summary>
    /// correlate_method resolves by native address and returns the correlation-aware disassembly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task CorrelateMethod_ByAddress_ReturnsNativeDisassembly()
    {
        TestSkip.When(Samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        ulong? va = null;
        using (var analyzer = new Dotsider.Core.Analysis.AssemblyAnalyzer(Samples.NativeAotConsoleExe!))
        {
            analyzer.AttachPreIlcCompanions();
            var correlation = analyzer.ManagedNativeIndex?.Methods.FirstOrDefault(m =>
                m.Status == Dotsider.Core.Analysis.Models.MethodCorrelationStatus.CorrelatedExact
                && m.NativeSymbols.Count > 0
                && m.NativeSymbols[0].FileOffset is not null);
            va = correlation?.NativeSymbols[0].VirtualAddress;
        }

        TestSkip.When(va is null, "no exact correlation with a native symbol on this leg");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "correlate_method",
            new Dictionary<string, object?>
            {
                ["methodOrAddress"] = $"0x{va!.Value:x}",
                ["assemblyPath"] = Samples.NativeAotConsoleExe
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.IsFalse(text.StartsWith("Error", StringComparison.Ordinal), "the tool reported an error");
        Assert.Contains("\"nativeDisassembly\"", text);
    }

    /// <summary>
    /// correlate_method surfaces an ambiguous name as an error listing every candidate.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task CorrelateMethod_AmbiguousName_ReturnsCandidateError()
    {
        TestSkip.When(Samples.NativeAotConsoleManagedDll is null, "pre-ILC companion was not produced");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "correlate_method",
            new Dictionary<string, object?>
            {
                ["methodOrAddress"] = "Greeter.Greet",
                ["assemblyPath"] = Samples.NativeAotConsoleExe
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.Contains("ambiguous", text);
        Assert.Contains("Greeter::Greet", text);
    }

    /// <summary>
    /// correlate_method reports the Native AOT requirement for a managed assembly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task CorrelateMethod_Managed_ReturnsError()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "correlate_method",
            new Dictionary<string, object?>
            {
                ["methodOrAddress"] = "Foo",
                ["assemblyPath"] = Samples.RichLibraryDll
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.Contains("Error", text);
        Assert.Contains("Native AOT", text);
    }
}
