using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Verifies the Wasm decoder against a real SDK-produced browser Wasm module.
/// The fixture publish emits <c>dotnet.native.wasm</c> with the Mono runtime and app code.
/// This covers real Wasm function bodies without pretending they are ReadyToRun images.
/// </summary>
[Collection("SampleAssemblies")]
public sealed class WasmSdkModuleDecoderTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Parses the code section from <c>dotnet.native.wasm</c> and decodes real function bodies.
    /// The test is skipped when the host SDK lacks the browser-wasm workload.
    /// Fallback instructions indicate that emitted Wasm bytecode was not modeled.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void BrowserWasmNativeModule_FunctionBodiesDecodeWithoutFallback()
    {
        Assert.SkipWhen(
            samples.ReadyToRunConsoleWasmNativeWasm is null,
            "browser-wasm publish did not run on this leg.");

        var module = File.ReadAllBytes(samples.ReadyToRunConsoleWasmNativeWasm!);
        var bodies = ReadFunctionBodies(module)
            .Where(static body => body.Code.Length > 0)
            .Take(32)
            .ToList();

        Assert.True(bodies.Count >= 16, "Expected at least 16 non-empty Wasm function bodies.");

        var sawCall = false;
        var sawLocalGet = false;
        var failures = new List<string>();

        foreach (var body in bodies)
        {
            var instructions = NativeDisassembler.Disassemble(
                body.Code, (ulong)body.CodeOffset, NativeArchitecture.Wasm32);
            var length = instructions.Sum(static instruction => instruction.Length);
            var fallback = instructions.FirstOrDefault(static instruction => instruction.IsFallback);

            sawCall |= instructions.Any(static instruction => instruction.Mnemonic == "call");
            sawLocalGet |= instructions.Any(static instruction => instruction.Mnemonic == "local.get");

            if (length != body.Code.Length || fallback is not null)
            {
                failures.Add(
                    $"body {body.Index} @ 0x{body.CodeOffset:x}: "
                    + $"length={length}/{body.Code.Length}, fallback={fallback?.Mnemonic} {fallback?.OperandText}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.True(sawCall, "Expected at least one SDK-produced Wasm body to contain a call.");
        Assert.True(sawLocalGet, "Expected at least one SDK-produced Wasm body to contain local.get.");
    }

    private static List<WasmFunctionBody> ReadFunctionBodies(ReadOnlySpan<byte> module)
    {
        if (module.Length < 8
            || module[0] != 0x00 || module[1] != 0x61 || module[2] != 0x73 || module[3] != 0x6D
            || module[4] != 0x01 || module[5] != 0x00 || module[6] != 0x00 || module[7] != 0x00)
        {
            throw new InvalidDataException("The fixture is not a WebAssembly 1.0 module.");
        }

        var pos = 8;
        while (pos < module.Length)
        {
            var sectionId = ReadByte(module, ref pos);
            var sectionSize = checked((int)ReadUleb(module, ref pos));
            var sectionEnd = checked(pos + sectionSize);
            if (sectionEnd > module.Length)
                throw new InvalidDataException("The WebAssembly section extends past the module.");

            if (sectionId == 10)
                return ReadCodeSection(module[pos..sectionEnd], pos);

            pos = sectionEnd;
        }

        throw new InvalidDataException("The WebAssembly module does not contain a code section.");
    }

    private static List<WasmFunctionBody> ReadCodeSection(ReadOnlySpan<byte> section, int sectionOffset)
    {
        var bodies = new List<WasmFunctionBody>();
        var pos = 0;
        var count = checked((int)ReadUleb(section, ref pos));

        for (var index = 0; index < count; index++)
        {
            var bodySize = checked((int)ReadUleb(section, ref pos));
            var bodyEnd = checked(pos + bodySize);
            if (bodyEnd > section.Length)
                throw new InvalidDataException("The WebAssembly function body extends past the code section.");

            var localDeclCount = checked((int)ReadUleb(section, ref pos));
            for (var local = 0; local < localDeclCount; local++)
            {
                _ = ReadUleb(section, ref pos);
                _ = ReadByte(section, ref pos);
            }

            var codeOffset = sectionOffset + pos;
            var code = section[pos..bodyEnd].ToArray();
            bodies.Add(new WasmFunctionBody(index, codeOffset, code));
            pos = bodyEnd;
        }

        return bodies;
    }

    private static byte ReadByte(ReadOnlySpan<byte> bytes, ref int pos)
    {
        if ((uint)pos >= (uint)bytes.Length)
            throw new InvalidDataException("Unexpected end of WebAssembly data.");

        return bytes[pos++];
    }

    private static ulong ReadUleb(ReadOnlySpan<byte> bytes, ref int pos)
    {
        ulong value = 0;
        var shift = 0;
        while (true)
        {
            var b = ReadByte(bytes, ref pos);
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return value;

            shift += 7;
            if (shift >= 64)
                throw new InvalidDataException("The WebAssembly LEB128 value is too large.");
        }
    }

    private readonly record struct WasmFunctionBody(int Index, int CodeOffset, byte[] Code);
}
