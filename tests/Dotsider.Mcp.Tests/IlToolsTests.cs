using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for IL disassembly and opcode search MCP tools.
/// </summary>
/// <summary>
/// Creates the tests using the shared sample assembly fixture.
/// </summary>
[TestClass]
public class IlToolsTests : McpServerTestBase
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// disassemble_method returns an instruction list for an existing method body.
    /// </summary>
    [TestMethod]
    public async Task DisassembleMethod_ValidMethod_ReturnsIlInstructions()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "disassemble_method",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.RichLibraryDll,
                ["typeName"] = "UserService",
                ["methodName"] = "Add"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsTrue(json.TryGetProperty("instructions", out var instructions));
        Assert.IsGreaterThan(0, instructions.GetArrayLength());
    }

    /// <summary>
    /// disassemble_method renders compiler-produced MethodSpec operands as constructed generic methods.
    /// </summary>
    [TestMethod]
    public async Task DisassembleMethod_MethodSpecs_ReturnConstructedGenericOperands()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "disassemble_method",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = typeof(MethodSpecReproFixture).Assembly.Location,
                ["typeName"] = MethodSpecReproFixture.TypeName,
                ["methodName"] = MethodSpecReproFixture.MethodName
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        var methodSpecOperands = json.GetProperty("instructions")
            .EnumerateArray()
            .Where(instruction => instruction.TryGetProperty("metadataToken", out var token)
                && token.ValueKind == JsonValueKind.Number
                && (uint)token.GetInt32() >> 24 == 0x2B)
            .Select(instruction => instruction.GetProperty("operand").GetString()!)
            .ToArray();

        Assert.AreSequenceEqual(MethodSpecReproFixture.ExpectedDisplays, methodSpecOperands);
    }

    /// <summary>
    /// disassemble_method can include portable PDB debug information when requested.
    /// </summary>
    [TestMethod]
    public async Task DisassembleMethod_WithDebugInfo_ReturnsPortablePdbData()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "disassemble_method",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.RichLibraryDll,
                ["typeName"] = "UserService",
                ["methodName"] = "Add",
                ["includeDebugInfo"] = true
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);

        Assert.AreEqual("sidecar", json.GetProperty("pdb").GetProperty("kind").GetString());
        Assert.IsTrue(json.GetProperty("sourceLink").GetProperty("isPresent").GetBoolean());
        Assert.IsGreaterThan(0, json.GetProperty("debugInfo").GetProperty("sequencePoints").GetArrayLength());
        Assert.Contains(instruction => instruction.TryGetProperty("localName", out var localName)
                && localName.GetString() == "id", json.GetProperty("instructions").EnumerateArray());
    }

    /// <summary>
    /// get_method_debug_info returns sequence points and local names for a method.
    /// </summary>
    [TestMethod]
    public async Task GetMethodDebugInfo_ReturnsSequencePointsAndLocals()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_method_debug_info",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.RichLibraryDll,
                ["typeName"] = "UserService",
                ["methodName"] = "Add"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);

        Assert.AreEqual("sidecar", json.GetProperty("pdb").GetProperty("kind").GetString());
        Assert.Contains(point => point.GetProperty("document").GetString()?.EndsWith("UserService.cs",
                StringComparison.OrdinalIgnoreCase) == true, json.GetProperty("sequencePoints").EnumerateArray());
        Assert.Contains(local => local.GetProperty("name").GetString() == "id", json.GetProperty("locals").EnumerateArray());
    }

    /// <summary>
    /// get_source_link returns decoded Source Link mappings.
    /// </summary>
    [TestMethod]
    public async Task GetSourceLink_ReturnsMappings()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_source_link",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.RichLibraryDll
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);

        Assert.IsTrue(json.GetProperty("isPresent").GetBoolean());
        Assert.Contains(mapping => mapping.GetProperty("urlTemplate").GetString()?.Contains("raw.githubusercontent.com",
                StringComparison.OrdinalIgnoreCase) == true, json.GetProperty("mappings").EnumerateArray());
    }

    /// <summary>
    /// Requesting IL for a method that does not exist yields a descriptive error.
    /// </summary>
    [TestMethod]
    public async Task DisassembleMethod_NonExistentMethod_ReturnsError()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "disassemble_method",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.HelloWorldDll,
                ["typeName"] = "Program",
                ["methodName"] = "NonExistentMethod"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.Contains("Error", text);
    }

    /// <summary>
    /// search_il_opcodes locates call-family instructions across the assembly's bodies.
    /// </summary>
    [TestMethod]
    public async Task SearchIlOpcodes_CallInstruction_FindsMatches()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "search_il_opcodes",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.RichLibraryDll,
                ["query"] = "call",
                ["maxResults"] = 5
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var results = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsGreaterThan(0, results.GetArrayLength());
    }

    /// <summary>
    /// search_il_opcodes surfaces newobj sites for identifying object allocations.
    /// </summary>
    [TestMethod]
    public async Task SearchIlOpcodes_NewobjInstruction_FindsObjectCreation()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "search_il_opcodes",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.ComplexAppDll,
                ["query"] = "newobj",
                ["maxResults"] = 10
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var results = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual(JsonValueKind.Array, results.ValueKind);
    }
}
