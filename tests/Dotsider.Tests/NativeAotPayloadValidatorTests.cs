namespace Dotsider.Tests;

/// <summary>
/// Exercises release directory validation with the native terminal layouts.
/// The same validator runs before packaging on every CI and release platform.
/// Unexpected assets and missing dependencies must both fail validation.
/// </summary>
[TestClass]
public sealed class NativeAotPayloadValidatorTests
{
    private string _output = "";

    /// <summary>Creates an isolated publish directory for each test.</summary>
    [TestInitialize]
    public void Initialize() => _output = Directory.CreateTempSubdirectory("dotsider-release-layout-").FullName;

    /// <summary>Removes the test's publish directory.</summary>
    [TestCleanup]
    public void Cleanup() => Directory.Delete(_output, recursive: true);

    /// <summary>Accepts the native terminal files shipped for each supported runtime.</summary>
    [TestMethod]
    [DataRow("win-x64", new[] { "conpty.dll", "arm64/OpenConsole.exe", "x64/OpenConsole.exe" })]
    [DataRow("win-arm64", new[] { "conpty.dll", "arm64/OpenConsole.exe" })]
    [DataRow("linux-x64", new[] { "libhex1binterop.so" })]
    [DataRow("linux-arm64", new[] { "libhex1binterop.so" })]
    [DataRow("linux-musl-x64", new[] { "libhex1binterop.so" })]
    [DataRow("linux-musl-arm64", new[] { "libhex1binterop.so" })]
    [DataRow("osx-x64", new[] { "libhex1binterop.dylib" })]
    [DataRow("osx-arm64", new[] { "libhex1binterop.dylib" })]
    public void Validate_AcceptsPlatformTerminalFiles(string rid, string[] terminalFiles)
    {
        WritePayload("dotsider", rid, terminalFiles);

        NativeAotPayloadValidator.Validate(_output, "dotsider", rid);
    }

    /// <summary>Requires every Windows console dependency, including both x64 package hosts.</summary>
    [TestMethod]
    [DataRow("conpty.dll")]
    [DataRow("arm64/OpenConsole.exe")]
    [DataRow("x64/OpenConsole.exe")]
    public void Validate_RejectsMissingWindowsTerminalFile(string missing)
    {
        WritePayload("dotsider", "win-x64", ["conpty.dll", "arm64/OpenConsole.exe", "x64/OpenConsole.exe"]);
        File.Delete(Path.Join(_output, missing));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            NativeAotPayloadValidator.Validate(_output, "dotsider", "win-x64"));
        Assert.Contains("Missing release files", exception.Message);
        Assert.Contains(missing, exception.Message);
    }

    /// <summary>Rejects flattened hosts, wrong architectures, symbols, and unrelated native files.</summary>
    [TestMethod]
    [DataRow("OpenConsole.exe")]
    [DataRow("x86/OpenConsole.exe")]
    [DataRow("unexpected.dll")]
    [DataRow("libhex1binterop.dll")]
    [DataRow("dotsider.pdb")]
    [DataRow("tracehost-extra/unexpected.dll")]
    public void Validate_RejectsUnexpectedWindowsFile(string unexpected)
    {
        WritePayload("dotsider", "win-x64", ["conpty.dll", "arm64/OpenConsole.exe", "x64/OpenConsole.exe"]);
        WriteFile(unexpected);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            NativeAotPayloadValidator.Validate(_output, "dotsider", "win-x64"));
        Assert.Contains("Unexpected release files", exception.Message);
        Assert.Contains(unexpected, exception.Message);
    }

    /// <summary>Does not allow terminal dependencies into the MCP release.</summary>
    [TestMethod]
    [DataRow("win-x64", "conpty.dll")]
    [DataRow("win-arm64", "arm64/OpenConsole.exe")]
    [DataRow("osx-arm64", "libhex1binterop.dylib")]
    [DataRow("linux-x64", "libhex1binterop.so")]
    public void Validate_McpRejectsTerminalFiles(string rid, string unexpected)
    {
        WritePayload("dotsider-mcp", rid, []);
        NativeAotPayloadValidator.Validate(_output, "dotsider-mcp", rid);
        WriteFile(unexpected);

        Assert.Throws<InvalidOperationException>(() =>
            NativeAotPayloadValidator.Validate(_output, "dotsider-mcp", rid));
    }

    private void WritePayload(string productName, string rid, string[] terminalFiles)
    {
        WriteFile(productName + (rid.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : ""));
        WriteFile("LICENSE");
        if (productName == "dotsider")
            WriteFile("tracehost/dotsider-tracehost.dll");
        foreach (string file in terminalFiles)
            WriteFile(file);
    }

    private void WriteFile(string relativePath)
    {
        string path = Path.Join(_output, relativePath);
        Directory.CreateDirectory(TestAssert.NotNull(Path.GetDirectoryName(path)));
        File.WriteAllText(path, "fixture");
    }
}
