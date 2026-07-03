using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="NativeImportResolver"/>: it maps a PE image's Import Address Table slots to
/// MODULE!Function names, so an indirect call through the IAT resolves to the imported symbol, and
/// composes into <see cref="NativeDisassembler.DisassembleSymbol"/>.
/// </summary>
[Collection("SampleAssemblies")]
public class NativeImportResolverTests(SampleAssemblyFixture samples)
{
    /// <summary>Verifies the resolver reads the AOT binary's imports and names an IAT slot.</summary>
    [Fact(Timeout = 60_000)]
    public void Build_NativeAot_MapsImportSlots()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null || !File.Exists(samples.NativeAotConsoleExe),
            "NativeAOT publish did not run on this leg.");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var resolver = NativeImportResolver.Build(analyzer.RawBytes);

        Assert.NotNull(resolver); // a NativeAOT exe imports a handful of OS APIs
        Assert.NotEmpty(resolver.Slots);

        // Each mapped slot round-trips through TryResolve to its MODULE!Function name.
        var (slotVa, name) = resolver.Slots.First();
        Assert.True(resolver.TryResolve(slotVa, out var import));
        Assert.Equal(name, import.Name);
        Assert.Contains('!', import.Name);
    }

    /// <summary>Verifies the import resolver composes into DisassembleSymbol's target naming.</summary>
    [Fact(Timeout = 60_000)]
    public void DisassembleSymbol_ComposesImportResolver()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null || !File.Exists(samples.NativeAotConsoleExe),
            "NativeAOT publish did not run on this leg.");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var resolver = NativeImportResolver.Build(analyzer.RawBytes);
        Assert.NotNull(resolver);

        // A synthetic call [rip+0] whose slot is the first IAT entry names the import.
        var (slotVa, expected) = resolver.Slots.First();
        var callAddress = slotVa - 6; // call [rip+0] is 6 bytes; next-IP == slot
        byte[] code = [0xFF, 0x15, 0x00, 0x00, 0x00, 0x00];
        bool Compose(ulong va, out NativeSymbolRef sym) => resolver.TryResolve(va, out sym);

        var insn = NativeDisassembler.Disassemble(code, callAddress, NativeArchitecture.X64, Compose)[0];
        Assert.Equal(expected, insn.TargetName);
    }

    /// <summary>Verifies a binary with no import table yields no resolver.</summary>
    [Fact(Timeout = 30_000)]
    public void Build_NoImports_ReturnsNull()
    {
        Assert.Null(NativeImportResolver.Build(new byte[] { 0x00, 0x01, 0x02, 0x03 }));
    }
}
