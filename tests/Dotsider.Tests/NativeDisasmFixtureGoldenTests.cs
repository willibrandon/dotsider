using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;
using System.Globalization;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// Verifies committed native-disassembly fixture goldens.
/// These fixtures keep deterministic byte-level decoder coverage in the repository.
/// Real sample assembly tests cover crossgen2 output where the SDK can produce it locally.
/// </summary>
[TestClass]
public sealed class NativeDisasmFixtureGoldenTests
{
    /// <summary>
    /// Verifies every committed native-disassembly fixture decodes exactly as expected.
    /// Length sums must match the fixture byte count and valid fixture rows must not fallback.
    /// The fixture metadata records the oracle source used when reviewing the byte sequence.
    /// </summary>
    [TestMethod]
    public void FixtureGoldens_DecodeExpectedInstructions()
    {
        string root = FindRepositoryRoot();
        string fixtureRoot = Path.Combine(root, "tests", "Dotsider.Tests", "Fixtures", "Disasm");
        string[] fixtures = Directory.GetFiles(fixtureRoot, "*.json", SearchOption.AllDirectories);

        Assert.IsNotEmpty(fixtures);
        foreach (string fixture in fixtures)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(fixture));
            JsonElement rootElement = document.RootElement;
            var architecture = Enum.Parse<NativeArchitecture>(rootElement.GetProperty("architecture").GetString()!);
            ulong baseAddress = ParseHexUlong(rootElement.GetProperty("baseAddress").GetString()!);
            byte[] bytes = ParseHexBytes(rootElement.GetProperty("hex").GetString()!);

            Assert.IsTrue(rootElement.TryGetProperty("oracle", out JsonElement oracle), fixture);
            Assert.IsFalse(string.IsNullOrWhiteSpace(oracle.GetProperty("kind").GetString()), fixture);
            Assert.IsTrue(rootElement.TryGetProperty("runtimeFiles", out JsonElement runtimeFiles), fixture);
            Assert.IsNotEmpty(runtimeFiles.EnumerateArray());

            IReadOnlyList<NativeInstruction> instructions = NativeDisassembler.Disassemble(bytes, baseAddress, architecture);
            JsonElement.ArrayEnumerator expected = rootElement.GetProperty("expected").EnumerateArray();
            var expectedRows = expected.ToArray();

            Assert.HasCount(expectedRows.Length, instructions);
            Assert.AreEqual(bytes.Length, instructions.Sum(static instruction => instruction.Length));

            for (var i = 0; i < expectedRows.Length; i++)
            {
                JsonElement expectedRow = expectedRows[i];
                NativeInstruction instruction = instructions[i];
                int offset = expectedRow.GetProperty("offset").GetInt32();

                Assert.AreEqual(baseAddress + (ulong)offset, instruction.Address);
                Assert.AreEqual(expectedRow.GetProperty("mnemonic").GetString(), instruction.Mnemonic);
                Assert.AreEqual(expectedRow.GetProperty("length").GetInt32(), instruction.Length);
                Assert.AreEqual(
                    Enum.Parse<NativeFlowKind>(expectedRow.GetProperty("flow").GetString()!),
                    instruction.Flow);

                if (expectedRow.TryGetProperty("operandText", out JsonElement operandText))
                {
                    Assert.AreEqual(operandText.GetString(), instruction.OperandText);
                }

                if (expectedRow.TryGetProperty("target", out JsonElement target))
                {
                    Assert.AreEqual(ParseHexUlong(target.GetString()!), instruction.TargetAddress);
                }

                if (!expectedRow.TryGetProperty("fallback", out JsonElement fallback) || !fallback.GetBoolean())
                {
                    Assert.IsFalse(instruction.IsFallback, $"{fixture}: {instruction.Address:x} {instruction.Mnemonic} {instruction.OperandText}");
                }
            }
        }
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

    private static ulong ParseHexUlong(string value)
    {
        string text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return ulong.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static byte[] ParseHexBytes(string value)
    {
        string compact = string.Concat(value.Where(static c => !char.IsWhiteSpace(c)));
        Assert.AreEqual(0, compact.Length % 2, value);

        var bytes = new byte[compact.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = byte.Parse(compact.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return bytes;
    }
}
