using Dotsider.Core.Protocol;
using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Verifies MCP tracing tools preserve literal process arguments.
/// </summary>
[TestClass]
public sealed class TraceToolsTests : McpServerTestBase
{
    /// <summary>
    /// Verifies the advertised start-trace schema exposes an array of strings.
    /// </summary>
    [TestMethod]
    public async Task StartTrace_ToolSchema_UsesStringArray()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var tools = await client.ListToolsAsync(cancellationToken: TestCancellationToken);
        var startTrace = tools.First(static tool => tool.Name == "start_trace");
        using var schema = JsonDocument.Parse(startTrace.JsonSchema.ToString());
        var arguments = schema.RootElement
            .GetProperty("properties")
            .GetProperty("arguments");
        if (arguments.TryGetProperty("anyOf", out var alternatives))
        {
            arguments = alternatives.EnumerateArray()
                .First(static alternative =>
                    alternative.TryGetProperty("type", out var type)
                    && type.GetString() == "array");
        }

        AssertSchemaType(arguments, "array");
        AssertSchemaType(arguments.GetProperty("items"), "string");
    }

    /// <summary>
    /// Verifies MCP argument values cross the real diagnostics socket unchanged.
    /// </summary>
    [TestMethod]
    public async Task StartTrace_LiteralArguments_PreservesExactArray()
    {
        var pid = TestSocketIds.NextPid();
        string[]? receivedArguments = null;
        await using var socket = new TestDotsiderSocket(pid, "/tmp/test/HelloWorld.dll");
        socket.OnMethod("start-trace", request =>
        {
            receivedArguments = request.Arguments;
            return DotsiderResponse.Ok(new
            {
                Message = "Trace start queued"
            });
        });
        socket.Start();
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        string[] expected =
        [
            "--fx-version",
            "value with spaces",
            "",
            "a&b",
            "$(whoami)"
        ];

        var result = await client.CallToolAsync(
            "start_trace",
            new Dictionary<string, object?>
            {
                ["sessionId"] = pid,
                ["arguments"] = expected
            },
            cancellationToken: TestCancellationToken);

        Assert.Contains("Trace start queued", GetTextContent(result)!);
        AssertArguments(expected, receivedArguments);
    }

    private static void AssertArguments(
        string[] expected,
        string[]? actual)
    {
        Assert.IsNotNull(actual);
        Assert.HasCount(expected.Length, actual);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.AreEqual(expected[index], actual[index], $"Argument {index} differs.");
        }
    }

    private static void AssertSchemaType(JsonElement schema, string expectedType)
    {
        var type = schema.GetProperty("type");
        if (type.ValueKind == JsonValueKind.Array)
        {
            Assert.Contains(
                expectedType,
                type.EnumerateArray().Select(static value => value.GetString()));
            return;
        }

        Assert.AreEqual(expectedType, type.GetString());
    }
}
