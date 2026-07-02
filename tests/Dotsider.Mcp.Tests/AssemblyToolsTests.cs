using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the MCP assembly-inspection tool suite.
/// </summary>
/// <summary>
/// Creates the tests using the shared sample assembly fixture.
/// </summary>
[Collection("SampleAssemblies")]
public class AssemblyToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    /// <summary>
    /// Verifies get_assembly_info returns populated metadata for a simple console executable.
    /// </summary>
    [Fact]
    public async Task GetAssemblyInfo_HelloWorld_ReturnsAssemblyMetadata()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.HelloWorldDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal("HelloWorld", json.GetProperty("assemblyName").GetString());
        Assert.True(json.GetProperty("hasMetadata").GetBoolean());
        Assert.True(json.GetProperty("typeCount").GetInt32() > 0);
        Assert.True(json.GetProperty("methodCount").GetInt32() > 0);
    }

    /// <summary>
    /// Confirms get_assembly_info surfaces version data and external references for a richer library.
    /// </summary>
    [Fact]
    public async Task GetAssemblyInfo_RichLibrary_IncludesVersion()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal("RichLibrary", json.GetProperty("assemblyName").GetString());
        Assert.True(json.GetProperty("assemblyRefCount").GetInt32() > 0);
    }

    /// <summary>
    /// Invoking get_assembly_info without required arguments yields a descriptive error payload.
    /// </summary>
    [Fact]
    public async Task GetAssemblyInfo_NoParams_ReturnsError()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_assembly_info",
            new Dictionary<string, object?>(),
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.Contains("Error", text);
    }

    /// <summary>
    /// list_types enumerates defined types from a basic assembly.
    /// </summary>
    [Fact]
    public async Task ListTypes_HelloWorld_ReturnsTypes()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_types",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.HelloWorldDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var types = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(types.GetArrayLength() > 0);
    }

    /// <summary>
    /// A query filter narrows list_types output to name-matching results only.
    /// </summary>
    [Fact]
    public async Task ListTypes_WithQuery_FiltersResults()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_types",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["query"] = "UserService"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var types = JsonSerializer.Deserialize<JsonElement>(text);
        foreach (var type in types.EnumerateArray())
        {
            Assert.Contains("UserService", type.GetProperty("fullName").GetString(),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// maxResults caps list_types output to protect clients from oversized payloads.
    /// </summary>
    [Fact]
    public async Task ListTypes_WithMaxResults_LimitsOutput()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_types",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["maxResults"] = 3
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var types = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(types.GetArrayLength() <= 3);
    }

    /// <summary>
    /// list_methods returns defined methods for a trivial assembly.
    /// </summary>
    [Fact]
    public async Task ListMethods_HelloWorld_ReturnsMethods()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_methods",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.HelloWorldDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var methods = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(methods.GetArrayLength() > 0);
    }

    /// <summary>
    /// typeName filter restricts list_methods to methods of the specified declaring type.
    /// </summary>
    [Fact]
    public async Task ListMethods_FilterByTypeName_ReturnsFilteredMethods()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_methods",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["typeName"] = "UserService"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var methods = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(methods.GetArrayLength() > 0);
        foreach (var method in methods.EnumerateArray())
        {
            Assert.Contains("UserService", method.GetProperty("declaringType").GetString(),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// find_members returns a grouped payload of types and methods matching the query.
    /// </summary>
    [Fact]
    public async Task FindMembers_SearchQuery_ReturnsMatchingMembers()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "find_members",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["query"] = "User"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.TryGetProperty("types", out _) || json.TryGetProperty("methods", out _));
    }

    /// <summary>
    /// A missing file surfaces as an IsError result with a clear not-found message.
    /// </summary>
    [Fact]
    public async Task GetAssemblyInfo_NonexistentFile_ReturnsFileNotFoundError()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = "/nonexistent/path.dll" },
            cancellationToken: TestCancellationToken);

        Assert.True(result.IsError);
        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.Contains("File not found", text);
        Assert.Contains("/nonexistent/path.dll", text);
    }

    /// <summary>
    /// An effectively empty assembly still returns a valid JSON array from list_types.
    /// </summary>
    [Fact]
    public async Task ListTypes_EmptyLib_ReturnsMinimalTypes()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_types",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.EmptyLibDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var types = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal(JsonValueKind.Array, types.ValueKind);
    }

    /// <summary>
    /// get_assembly_info exposes displayName, bundle flags, and preferred runtime pack.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task GetAssemblyInfo_IncludesNewProperties()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.NotNull(json.GetProperty("displayName").GetString());
        Assert.False(json.GetProperty("isBundleBacked").GetBoolean());
        Assert.True(json.GetProperty("canSaveInPlace").GetBoolean());
        Assert.Equal("Microsoft.NETCore.App", json.GetProperty("preferredRuntimePack").GetString());
    }

    /// <summary>
    /// get_assembly_info reports a Native AOT executable's binary kind and
    /// ReadyToRun header facts.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task GetAssemblyInfo_NativeAot_ReportsBinaryKindAndRtr()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal("nativeAot", json.GetProperty("binaryKind").GetString());
        var aotInfo = json.GetProperty("nativeAotInfo");
        Assert.True(aotInfo.GetProperty("majorVersion").GetInt32() >= 1);
        Assert.True(aotInfo.GetProperty("sectionCount").GetInt32() >= 1);
        Assert.False(json.GetProperty("hasMetadata").GetBoolean());
    }

    /// <summary>
    /// get_assembly_info reports a managed assembly's binary kind as managed with
    /// no Native AOT info attached.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task GetAssemblyInfo_Managed_ReportsManagedKind()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal("managed", json.GetProperty("binaryKind").GetString());
        Assert.False(json.TryGetProperty("nativeAotInfo", out _));
    }

    /// <summary>
    /// A self-contained apphost is reported as bundle-backed with an in-bundle display name.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task GetAssemblyInfo_BundleBacked_ShowsBundleInfo()
    {
        Assert.NotNull(samples.SelfContainedConsoleExe);
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.SelfContainedConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.GetProperty("isBundleBacked").GetBoolean());
        Assert.Equal("SelfContainedConsole.dll", json.GetProperty("displayName").GetString());
        Assert.False(json.GetProperty("canSaveInPlace").GetBoolean());
    }

    /// <summary>
    /// ASP.NET Core apps report Microsoft.AspNetCore.App as their preferred runtime pack.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task GetAssemblyInfo_AspNetCore_PreferredPack()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.MinimalApiDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal("Microsoft.AspNetCore.App", json.GetProperty("preferredRuntimePack").GetString());
    }
}
