using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the MCP assembly-inspection tool suite.
/// </summary>
/// <summary>
/// Creates the tests using the shared sample assembly fixture.
/// </summary>
[TestClass]
public class AssemblyToolsTests : McpServerTestBase
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies get_assembly_info returns populated metadata for a simple console executable.
    /// </summary>
    [TestMethod]
    public async Task GetAssemblyInfo_HelloWorld_ReturnsAssemblyMetadata()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.HelloWorldDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual("HelloWorld", json.GetProperty("assemblyName").GetString());
        Assert.IsTrue(json.GetProperty("hasMetadata").GetBoolean());
        Assert.IsGreaterThan(0, json.GetProperty("typeCount").GetInt32());
        Assert.IsGreaterThan(0, json.GetProperty("methodCount").GetInt32());
    }

    /// <summary>
    /// Confirms get_assembly_info surfaces version data and external references for a richer library.
    /// </summary>
    [TestMethod]
    public async Task GetAssemblyInfo_RichLibrary_IncludesVersion()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual("RichLibrary", json.GetProperty("assemblyName").GetString());
        Assert.IsGreaterThan(0, json.GetProperty("assemblyRefCount").GetInt32());
    }

    /// <summary>
    /// Invoking get_assembly_info without required arguments yields a descriptive error payload.
    /// </summary>
    [TestMethod]
    public async Task GetAssemblyInfo_NoParams_ReturnsError()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_assembly_info",
            new Dictionary<string, object?>(),
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.Contains("Error", text);
    }

    /// <summary>
    /// list_types enumerates defined types from a basic assembly.
    /// </summary>
    [TestMethod]
    public async Task ListTypes_HelloWorld_ReturnsTypes()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_types",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.HelloWorldDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var types = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsGreaterThan(0, types.GetArrayLength());
    }

    /// <summary>
    /// A query filter narrows list_types output to name-matching results only.
    /// </summary>
    [TestMethod]
    public async Task ListTypes_WithQuery_FiltersResults()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_types",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.RichLibraryDll,
                ["query"] = "UserService"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var types = JsonSerializer.Deserialize<JsonElement>(text);
        foreach (var type in types.EnumerateArray())
        {
            Assert.Contains("UserService", type.GetProperty("fullName").GetString()!,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// maxResults caps list_types output to protect clients from oversized payloads.
    /// </summary>
    [TestMethod]
    public async Task ListTypes_WithMaxResults_LimitsOutput()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_types",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.RichLibraryDll,
                ["maxResults"] = 3
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var types = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsLessThanOrEqualTo(3, types.GetArrayLength());
    }

    /// <summary>
    /// list_methods returns defined methods for a trivial assembly.
    /// </summary>
    [TestMethod]
    public async Task ListMethods_HelloWorld_ReturnsMethods()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_methods",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.HelloWorldDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var methods = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsGreaterThan(0, methods.GetArrayLength());
    }

    /// <summary>
    /// typeName filter restricts list_methods to methods of the specified declaring type.
    /// </summary>
    [TestMethod]
    public async Task ListMethods_FilterByTypeName_ReturnsFilteredMethods()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_methods",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.RichLibraryDll,
                ["typeName"] = "UserService"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var methods = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsGreaterThan(0, methods.GetArrayLength());
        foreach (var method in methods.EnumerateArray())
        {
            Assert.Contains("UserService", method.GetProperty("declaringType").GetString()!,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// find_members returns a grouped payload of types and methods matching the query.
    /// </summary>
    [TestMethod]
    public async Task FindMembers_SearchQuery_ReturnsMatchingMembers()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "find_members",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.RichLibraryDll,
                ["query"] = "User"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsTrue(json.TryGetProperty("types", out _) || json.TryGetProperty("methods", out _));
    }

    /// <summary>
    /// A missing file surfaces as an IsError result with a clear not-found message.
    /// </summary>
    [TestMethod]
    public async Task GetAssemblyInfo_NonexistentFile_ReturnsFileNotFoundError()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = "/nonexistent/path.dll" },
            cancellationToken: TestCancellationToken);

        Assert.IsTrue(result.IsError);
        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.Contains("File not found", text);
        Assert.Contains("/nonexistent/path.dll", text);
    }

    /// <summary>
    /// An effectively empty assembly still returns a valid JSON array from list_types.
    /// </summary>
    [TestMethod]
    public async Task ListTypes_EmptyLib_ReturnsMinimalTypes()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_types",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.EmptyLibDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var types = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual(JsonValueKind.Array, types.ValueKind);
    }

    /// <summary>
    /// list_types unwraps Webcil browser app assemblies and returns their managed type definitions.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ListTypes_WebcilWasm_ReturnsManagedTypes()
    {
        TestSkip.When(Samples.WasmConsoleWebcilWasm is null,
            "browser-wasm publish did not produce the Webcil app assembly on this leg.");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_types",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.WasmConsoleWebcilWasm },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var types = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Contains(static type =>
            type.GetProperty("fullName").GetString() == "WasmCalculator", types.EnumerateArray());
    }

    /// <summary>
    /// get_assembly_info exposes displayName, bundle flags, and preferred runtime pack.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetAssemblyInfo_IncludesNewProperties()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsNotNull(json.GetProperty("displayName").GetString());
        Assert.IsFalse(json.GetProperty("isBundleBacked").GetBoolean());
        Assert.IsTrue(json.GetProperty("canSaveInPlace").GetBoolean());
        Assert.AreEqual("Microsoft.NETCore.App", json.GetProperty("preferredRuntimePack").GetString());
    }

    /// <summary>
    /// get_assembly_info reports a Native AOT executable's binary kind and
    /// ReadyToRun header facts.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetAssemblyInfo_NativeAot_ReportsBinaryKindAndRtr()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual("nativeAot", json.GetProperty("binaryKind").GetString());
        var aotInfo = json.GetProperty("nativeAotInfo");
        Assert.IsGreaterThanOrEqualTo(1, aotInfo.GetProperty("majorVersion").GetInt32());
        Assert.IsGreaterThanOrEqualTo(1, aotInfo.GetProperty("sectionCount").GetInt32());
        Assert.IsFalse(json.GetProperty("hasMetadata").GetBoolean());
    }

    /// <summary>
    /// get_assembly_info reports a raw <c>dotnet.native.wasm</c> module as WebAssembly and includes
    /// function, code, data, and symbol-map facts from the SDK-produced module.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetAssemblyInfo_Wasm_ReportsModuleFacts()
    {
        var wasmPath = GetWasmNativePath();

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = wasmPath },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual("wasm", json.GetProperty("binaryKind").GetString());
        Assert.AreEqual("Wasm32", json.GetProperty("architecture").GetString());
        Assert.IsFalse(json.GetProperty("hasMetadata").GetBoolean());
        var wasm = json.GetProperty("wasm");
        Assert.IsGreaterThan(0, wasm.GetProperty("definedFunctionCount").GetInt32());
        Assert.IsGreaterThan(0, wasm.GetProperty("importedFunctionCount").GetInt32());
        Assert.IsGreaterThan(0, wasm.GetProperty("codeSize").GetInt64());
        Assert.IsGreaterThan(0, wasm.GetProperty("dataSize").GetInt64());
        Assert.AreEqual("Loaded", wasm.GetProperty("symbolMapStatus").GetString());
    }

    /// <summary>
    /// get_assembly_info reports a Webcil-wrapped browser app assembly as normal managed metadata
    /// with Webcil payload facts attached, not as the raw runtime WebAssembly module.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetAssemblyInfo_WebcilWasm_ReportsManagedMetadata()
    {
        TestSkip.When(Samples.WasmConsoleWebcilWasm is null,
            "browser-wasm publish did not produce the Webcil app assembly on this leg.");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.WasmConsoleWebcilWasm },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual("managed", json.GetProperty("binaryKind").GetString());
        Assert.AreEqual("Wasm32", json.GetProperty("architecture").GetString());
        Assert.IsTrue(json.GetProperty("hasMetadata").GetBoolean());
        Assert.AreEqual("WasmConsole", json.GetProperty("assemblyName").GetString());
        Assert.IsFalse(json.TryGetProperty("wasm", out _));
        var webcil = json.GetProperty("webcil");
        Assert.IsTrue(webcil.GetProperty("isWasmWrapped").GetBoolean());
        Assert.IsGreaterThan(0, webcil.GetProperty("sectionCount").GetInt32());
        Assert.IsGreaterThan(0, webcil.GetProperty("metadataSize").GetInt32());
    }

    /// <summary>
    /// get_assembly_info reports a managed assembly's binary kind as managed with
    /// no Native AOT info attached.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetAssemblyInfo_Managed_ReportsManagedKind()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual("managed", json.GetProperty("binaryKind").GetString());
        Assert.IsFalse(json.TryGetProperty("nativeAotInfo", out _));
    }

    /// <summary>
    /// A self-contained apphost is reported as bundle-backed with an in-bundle display name.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetAssemblyInfo_BundleBacked_ShowsBundleInfo()
    {
        Assert.IsNotNull(Samples.SelfContainedConsoleExe);
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.SelfContainedConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsTrue(json.GetProperty("isBundleBacked").GetBoolean());
        Assert.AreEqual("SelfContainedConsole.dll", json.GetProperty("displayName").GetString());
        Assert.IsFalse(json.GetProperty("canSaveInPlace").GetBoolean());
    }

    /// <summary>
    /// ASP.NET Core apps report Microsoft.AspNetCore.App as their preferred runtime pack.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetAssemblyInfo_AspNetCore_PreferredPack()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.MinimalApiDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual("Microsoft.AspNetCore.App", json.GetProperty("preferredRuntimePack").GetString());
    }

    /// <summary>
    /// get_assembly_info reports Native AOT section, recovered-type, and frozen-string counts.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetAssemblyInfo_NativeAot_ReportsAotCounts()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsGreaterThan(0, json.GetProperty("readyToRunSectionCount").GetInt32());
        Assert.IsGreaterThan(0, json.GetProperty("recoveredTypeCount").GetInt32());
        Assert.IsTrue(json.TryGetProperty("frozenStringCount", out _));
    }

    /// <summary>
    /// list_types falls back to the types recovered from a Native AOT binary's metadata.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ListTypes_NativeAot_ReturnsRecoveredTypes()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("list_types",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual(JsonValueKind.Array, json.ValueKind);
        Assert.IsGreaterThan(0, json.GetArrayLength());
        var names = json.EnumerateArray().Select(e => e.GetProperty("fullName").GetString()).ToList();
        Assert.Contains("Program", names);
    }

    /// <summary>
    /// list_methods falls back to recovered Native AOT method names when ECMA metadata is absent.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ListMethods_NativeAot_ReturnsRecoveredMethods()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("list_methods",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.NativeAotConsoleExe,
                ["typeName"] = "Program"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual(JsonValueKind.Array, json.ValueKind);
        Assert.IsGreaterThan(0, json.GetArrayLength());
        TestAssert.All(json.EnumerateArray(), e =>
            Assert.AreEqual("RecoveredNativeAot", e.GetProperty("source").GetString()));
    }

    /// <summary>
    /// find_members searches recovered Native AOT types and methods instead of metadata-only tables.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task FindMembers_NativeAot_SearchesRecoveredInventory()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("find_members",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.NativeAotConsoleExe,
                ["query"] = "Program"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsTrue(json.GetProperty("types").GetArrayLength() > 0
            || json.GetProperty("methods").GetArrayLength() > 0);
        Assert.AreEqual(0, json.GetProperty("memberRefs").GetArrayLength());
    }

    private static string GetWasmNativePath()
    {
        TestSkip.When(Samples.WasmConsoleNativeWasm is null && Samples.ReadyToRunConsoleWasmNativeWasm is null,
            "browser-wasm publish did not run on this leg.");

        return Samples.WasmConsoleNativeWasm ?? Samples.ReadyToRunConsoleWasmNativeWasm!;
    }
}
