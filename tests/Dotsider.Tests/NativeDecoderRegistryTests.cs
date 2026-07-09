using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Verifies the native decoder registry is the single architecture support table.
/// These tests prevent new enum values from silently falling through to an unrelated decoder.
/// Unknown remains the only intentionally unsupported architecture state.
/// </summary>
[TestClass]
public class NativeDecoderRegistryTests
{
    /// <summary>
    /// Verifies every concrete native architecture has an explicit registry row.
    /// This keeps support table-driven instead of hidden in a switch default.
    /// A new enum value must add a decoder before it can pass this test.
    /// </summary>
    [TestMethod]
    public void ConcreteArchitectures_AreRegistered()
    {
        var expected = Enum.GetValues<NativeArchitecture>()
            .Where(a => a != NativeArchitecture.Unknown)
            .Order()
            .ToArray();

        var actual = NativeDecoderRegistry.SupportedArchitectures
            .Order()
            .ToArray();

        Assert.AreSequenceEqual(expected, actual);
    }

    /// <summary>
    /// Verifies only unknown architecture is unsupported by the registry.
    /// This documents that recognized .NET architectures must decode.
    /// The public disassembler therefore never guesses x64 for another enum value.
    /// </summary>
    [TestMethod]
    public void Unknown_IsOnlyUnsupportedArchitecture()
    {
        foreach (var architecture in Enum.GetValues<NativeArchitecture>())
        {
            Assert.AreEqual(
                architecture != NativeArchitecture.Unknown,
                NativeDecoderRegistry.IsSupported(architecture));
        }
    }

    /// <summary>
    /// Verifies unknown architecture does not dispatch to any decoder.
    /// The public disassembler still emits exact fallback bytes for raw unknown input.
    /// That fallback is honest and does not claim a concrete instruction set.
    /// </summary>
    [TestMethod]
    public void Unknown_DisassemblesAsByteFallback()
    {
        Assert.IsFalse(NativeDecoderRegistry.TryDecode(
            NativeArchitecture.Unknown, [0x40], 0, 0x1000, out _));

        var insn = Assert.ContainsSingle(NativeDisassembler.Disassemble([0x40], 0x1000, NativeArchitecture.Unknown));
        Assert.AreEqual(".byte", insn.Mnemonic);
        Assert.IsTrue(insn.IsFallback);
    }
}
