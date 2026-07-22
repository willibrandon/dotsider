using Dotsider.Core.Analysis;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the PE/CLR metadata inspection MCP tool suite.
/// </summary>
/// <summary>
/// Creates the tests using the shared sample assembly fixture.
/// </summary>
[TestClass]
public class MetadataToolsTests : McpServerTestBase
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// get_pe_headers returns parsed PE header info without errors for a valid assembly.
    /// </summary>
    [TestMethod]
    public async Task GetPeHeaders_ValidAssembly_ReturnsHeaders()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_pe_headers",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.HelloWorldDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.DoesNotContain("Error", text);
    }

    /// <summary>
    /// get_clr_header returns CLR directory info without errors for a managed assembly.
    /// </summary>
    [TestMethod]
    public async Task GetClrHeader_ValidAssembly_ReturnsClrInfo()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_clr_header",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.HelloWorldDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.DoesNotContain("Error", text);
    }

    /// <summary>
    /// get_sections enumerates the PE section table as a non-empty JSON array.
    /// </summary>
    [TestMethod]
    public async Task GetSections_ValidAssembly_ReturnsSections()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_sections",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var sections = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsGreaterThan(0, sections.GetArrayLength());
    }

    /// <summary>
    /// get_custom_attributes returns at least one attribute for a real library.
    /// </summary>
    [TestMethod]
    public async Task GetCustomAttributes_ValidAssembly_ReturnsAttributes()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_custom_attributes",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var attrs = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsGreaterThan(0, attrs.GetArrayLength());
    }

    /// <summary>
    /// By default, compiler-generated attributes like Nullable/CompilerGenerated are filtered out.
    /// </summary>
    [TestMethod]
    public async Task GetCustomAttributes_DefaultFiltering_ExcludesCompilerGenerated()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_custom_attributes",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.DoesNotContain("CompilerGeneratedAttribute", text);
        Assert.DoesNotContain("NullableContextAttribute", text);
        Assert.DoesNotContain("NullableAttribute", text);
        Assert.DoesNotContain("DebuggerBrowsableAttribute", text);
    }

    /// <summary>
    /// Opting in via includeCompilerGenerated re-exposes the noisy compiler attributes.
    /// </summary>
    [TestMethod]
    public async Task GetCustomAttributes_IncludeCompilerGenerated_ReturnsAll()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_custom_attributes",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.RichLibraryDll,
                ["includeCompilerGenerated"] = true
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        // With includeCompilerGenerated=true, these should be present
        Assert.Contains("CompilerGeneratedAttribute", text);
    }

    /// <summary>
    /// The advertised tool schema surfaces the includeCompilerGenerated parameter to clients.
    /// </summary>
    [TestMethod]
    public async Task GetCustomAttributes_ToolSchema_IncludesFilterParameter()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var tools = await client.ListToolsAsync(cancellationToken: TestCancellationToken);
        var customAttrTool = tools.First(t => t.Name == "get_custom_attributes");
        var schema = customAttrTool.JsonSchema.ToString();
        Assert.Contains("includeCompilerGenerated", schema);
    }

    /// <summary>
    /// get_resources always returns a JSON array, even for assemblies with no embedded resources.
    /// </summary>
    [TestMethod]
    public async Task GetResources_ValidAssembly_ReturnsResourceList()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_resources",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.HelloWorldDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.AreEqual(JsonValueKind.Array,
            JsonSerializer.Deserialize<JsonElement>(text).ValueKind);
    }

    /// <summary>
    /// resolve_token turns a raw metadata token into a human-readable member name.
    /// </summary>
    [TestMethod]
    public async Task ResolveToken_ValidToken_ReturnsResolvedName()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        // 0x02000002 is typically a TypeDef token for the first user type
        var result = await client.CallToolAsync(
            "resolve_token",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.HelloWorldDll,
                ["token"] = 0x02000002
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsTrue(json.TryGetProperty("resolved", out var resolved));
        Assert.IsFalse(string.IsNullOrEmpty(resolved.GetString()));
    }

    /// <summary>
    /// resolve_token decodes a compiler-produced MethodSpec into its constructed generic method.
    /// </summary>
    [TestMethod]
    public async Task ResolveToken_MethodSpecs_ReturnConstructedGenericMethods()
    {
        var assemblyPath = typeof(MethodSpecReproFixture).Assembly.Location;
        int[] methodSpecTokens;
        using (var analyzer = new AssemblyAnalyzer(assemblyPath))
        {
            var method = Assert.ContainsSingle(analyzer.MethodDefs.Where(candidate =>
                candidate.DeclaringType == MethodSpecReproFixture.TypeName
                && candidate.Name == MethodSpecReproFixture.MethodName));
            methodSpecTokens = [.. new IlDisassembler(analyzer)
                .Disassemble(method)
                .Where(candidate => candidate.OpCode == "call"
                    && candidate.MetadataToken is { } token
                    && MetadataTokens.EntityHandle(token).Kind == HandleKind.MethodSpecification)
                .Select(instruction => instruction.MetadataToken!.Value)];
        }

        Assert.HasCount(MethodSpecReproFixture.ExpectedDisplays.Count, methodSpecTokens);
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        var resolvedNames = new List<string>();
        foreach (var methodSpecToken in methodSpecTokens)
        {
            var result = await client.CallToolAsync(
                "resolve_token",
                new Dictionary<string, object?>
                {
                    ["assemblyPath"] = assemblyPath,
                    ["token"] = methodSpecToken
                },
                cancellationToken: TestCancellationToken);

            var text = GetTextContent(result);
            Assert.IsNotNull(text);
            var json = JsonSerializer.Deserialize<JsonElement>(text);
            resolvedNames.Add(json.GetProperty("resolved").GetString()!);
        }

        Assert.AreSequenceEqual(MethodSpecReproFixture.ExpectedDisplays, resolvedNames);
    }
}
