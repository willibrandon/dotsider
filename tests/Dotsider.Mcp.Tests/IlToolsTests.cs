using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for IL disassembly and opcode search MCP tools.
/// </summary>
/// <summary>
/// Creates the tests using the shared sample assembly fixture.
/// </summary>
[Collection("SampleAssemblies")]
public class IlToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    /// <summary>
    /// disassemble_method returns an instruction list for an existing method body.
    /// </summary>
    [Fact]
    public async Task DisassembleMethod_ValidMethod_ReturnsIlInstructions()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "disassemble_method",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["typeName"] = "UserService",
                ["methodName"] = "Add"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.TryGetProperty("instructions", out var instructions));
        Assert.True(instructions.GetArrayLength() > 0);
    }

    /// <summary>
    /// disassemble_method can include portable PDB debug information when requested.
    /// </summary>
    [Fact]
    public async Task DisassembleMethod_WithDebugInfo_ReturnsPortablePdbData()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "disassemble_method",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["typeName"] = "UserService",
                ["methodName"] = "Add",
                ["includeDebugInfo"] = true
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);

        Assert.Equal("sidecar", json.GetProperty("pdb").GetProperty("kind").GetString());
        Assert.True(json.GetProperty("sourceLink").GetProperty("isPresent").GetBoolean());
        Assert.True(json.GetProperty("debugInfo").GetProperty("sequencePoints").GetArrayLength() > 0);
        Assert.Contains(json.GetProperty("instructions").EnumerateArray(),
            instruction => instruction.TryGetProperty("localName", out var localName)
                && localName.GetString() == "id");
    }

    /// <summary>
    /// get_method_debug_info returns sequence points and local names for a method.
    /// </summary>
    [Fact]
    public async Task GetMethodDebugInfo_ReturnsSequencePointsAndLocals()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_method_debug_info",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["typeName"] = "UserService",
                ["methodName"] = "Add"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);

        Assert.Equal("sidecar", json.GetProperty("pdb").GetProperty("kind").GetString());
        Assert.Contains(json.GetProperty("sequencePoints").EnumerateArray(),
            point => point.GetProperty("document").GetString()?.EndsWith("UserService.cs",
                StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(json.GetProperty("locals").EnumerateArray(),
            local => local.GetProperty("name").GetString() == "id");
    }

    /// <summary>
    /// get_source_link returns decoded Source Link mappings.
    /// </summary>
    [Fact]
    public async Task GetSourceLink_ReturnsMappings()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_source_link",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);

        Assert.True(json.GetProperty("isPresent").GetBoolean());
        Assert.Contains(json.GetProperty("mappings").EnumerateArray(),
            mapping => mapping.GetProperty("urlTemplate").GetString()?.Contains("raw.githubusercontent.com",
                StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>
    /// Requesting IL for a method that does not exist yields a descriptive error.
    /// </summary>
    [Fact]
    public async Task DisassembleMethod_NonExistentMethod_ReturnsError()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "disassemble_method",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.HelloWorldDll,
                ["typeName"] = "Program",
                ["methodName"] = "NonExistentMethod"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.Contains("Error", text);
    }

    /// <summary>
    /// search_il_opcodes locates call-family instructions across the assembly's bodies.
    /// </summary>
    [Fact]
    public async Task SearchIlOpcodes_CallInstruction_FindsMatches()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "search_il_opcodes",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["query"] = "call",
                ["maxResults"] = 5
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var results = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(results.GetArrayLength() > 0);
    }

    /// <summary>
    /// search_il_opcodes surfaces newobj sites for identifying object allocations.
    /// </summary>
    [Fact]
    public async Task SearchIlOpcodes_NewobjInstruction_FindsObjectCreation()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "search_il_opcodes",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.ComplexAppDll,
                ["query"] = "newobj",
                ["maxResults"] = 10
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var results = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal(JsonValueKind.Array, results.ValueKind);
    }
}
