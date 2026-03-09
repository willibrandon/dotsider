using System.Text.Json;

namespace Dotsider.Mcp.Tests;

[Collection("SampleAssemblies")]
public class IlToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
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
