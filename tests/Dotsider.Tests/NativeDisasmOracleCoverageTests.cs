using Dotsider.Core.Analysis.Models;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// Verifies that architectures without always-available SDK publishes have committed oracles.
/// The oracle metadata records the runtime source files and capture utility used for review.
/// This keeps unsupported public SDK RID packs from becoming untested decoder paths.
/// </summary>
[TestClass]
public sealed class NativeDisasmOracleCoverageTests
{
    /// <summary>
    /// Confirms a committed fixture exists for each architecture that may need external capture.
    /// The fixture must point back to the file-based oracle utility and runtime ground-truth files.
    /// This is the explicit fallback when dotnet publish cannot produce the image locally.
    /// </summary>
    [TestMethod]
    [DataRow("riscv64", "runtime-smoke.json", NativeArchitecture.RiscV64)]
    [DataRow("loongarch64", "runtime-smoke.json", NativeArchitecture.LoongArch64)]
    [DataRow("wasm32", "runtime-smoke.json", NativeArchitecture.Wasm32)]
    public void RuntimeOracleFixture_RecordsCaptureScriptAndRuntimeSources(
        string directory, string fileName, NativeArchitecture expectedArchitecture)
    {
        var root = FindRepositoryRoot();
        var fixturePath = Path.Combine(
            root, "tests", "Dotsider.Tests", "Fixtures", "Disasm", directory, fileName);

        Assert.IsTrue(File.Exists(fixturePath), fixturePath);

        using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var fixture = document.RootElement;
        var architecture = Enum.Parse<NativeArchitecture>(fixture.GetProperty("architecture").GetString()!);

        Assert.AreEqual(expectedArchitecture, architecture);
        Assert.AreEqual("scripts/Capture-DisasmOracle.cs", fixture.GetProperty("oracle").GetProperty("script").GetString());
        Assert.IsTrue(File.Exists(Path.Combine(root, "scripts", "Capture-DisasmOracle.cs")));

        var runtimeFiles = fixture.GetProperty("runtimeFiles").EnumerateArray().ToArray();
        Assert.IsNotEmpty(runtimeFiles);
        TestAssert.All(runtimeFiles, file =>
            Assert.IsFalse(string.IsNullOrWhiteSpace(file.GetString())));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dotsider.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
