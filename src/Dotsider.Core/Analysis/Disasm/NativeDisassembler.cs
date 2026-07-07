using Dotsider.Core.Analysis.Models;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dotsider.Core.Analysis.Disasm;

/// <summary>
/// Disassembles a native code window into <see cref="NativeInstruction"/>s and a rendered listing,
/// dispatching to the table-driven x86-64 and A64 decoders. The listing mirrors the IL disassembly
/// shape (<c>IlDisassembler.DisassembleWithText</c>): an optional header, then one line per
/// instruction, <c>loc_…:</c> labels for intra-function targets, and each rendered line's column
/// spans recorded on <see cref="NativeInstruction.Layout"/> so the TUI decorates structurally.
/// Call/branch/data targets are resolved to names through a <see cref="NativeSymbolResolver"/>.
/// A byte the decoder cannot recognize renders as an exact-width <c>.byte</c>/<c>.word</c> safety
/// net that never desyncs the listing.
/// </summary>
public static class NativeDisassembler
{
    private const int MaxInstructions = 1 << 20;

    /// <summary>
    /// Decodes a code window into instructions, resolving call/branch/data targets to names and
    /// synthesizing labels for intra-window targets.
    /// </summary>
    /// <param name="code">The exact code bytes of the region to disassemble.</param>
    /// <param name="baseAddress">The virtual address the first byte maps to.</param>
    /// <param name="arch">The instruction-set architecture to decode as.</param>
    /// <param name="resolver">Resolves a target address to a symbol name, or null for no naming.</param>
    public static IReadOnlyList<NativeInstruction> Disassemble(
        ReadOnlySpan<byte> code, ulong baseAddress, NativeArchitecture arch,
        NativeSymbolResolver? resolver = null)
    {
        var result = new List<NativeInstruction>();
        var windowEnd = baseAddress + (ulong)code.Length;
        var offset = 0;

        // Only x64 and AArch64 have decoders; any other architecture (the report-only R2R arches —
        // x86, arm32, riscv64, loongarch64, wasm32) must not be silently decoded as x64. Skip the
        // decode loop so the tail renders every byte as .byte rather than fabricating instructions.
        var decodable = arch is NativeArchitecture.X64 or NativeArchitecture.Arm64;

        try
        {
            while (decodable && offset < code.Length && result.Count < MaxInstructions)
            {
                var insn = DecodeOne(arch, code, offset, baseAddress);
                var length = insn.Length < 1 ? 1 : insn.Length;
                result.Add(insn.Length < 1 ? insn with { Length = 1 } : insn);
                offset += length;
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // A decoder read past a truncated tail; fall through to render the remainder as .byte.
        }

        // Render any undecoded tail — a truncated or corrupt region a decoder could not size — as one
        // .byte per remaining byte, so summed instruction lengths always equal the window and nothing
        // is silently dropped (the promised fallback safety net).
        while (offset < code.Length && result.Count < MaxInstructions)
        {
            result.Add(ByteFallback(baseAddress + (ulong)offset, code[offset]));
            offset++;
        }

        if (arch == NativeArchitecture.Arm64 && resolver is not null)
            ResolveArm64IndirectImports(result, resolver);

        return ResolveTargets(result, baseAddress, windowEnd, resolver);
    }

    private static NativeInstruction ByteFallback(ulong address, byte value)
    {
        var text = $"0x{value:x2}";
        return new NativeInstruction(
            Address: address, Rva: null, FileOffset: null, Bytes: [value], Length: 1,
            Mnemonic: ".byte", Operands: [new NativeOperand(NativeOperandKind.Immediate, text)],
            OperandText: text, Category: NativeInstructionCategory.Unknown,
            Flow: NativeFlowKind.Sequential, TargetAddress: null, TargetKind: NativeTargetKind.None,
            TargetName: null, SourceFile: null, Line: null, IsFallback: true);
    }

    /// <summary>
    /// Disassembles a code window and renders it to text, returning the text, the instruction list
    /// (each carrying its 1-based <see cref="NativeInstruction.DisplayLine"/> and
    /// <see cref="NativeInstruction.Layout"/>), and the header line count.
    /// </summary>
    /// <param name="code">The exact code bytes of the region to disassemble.</param>
    /// <param name="baseAddress">The virtual address the first byte maps to.</param>
    /// <param name="arch">The instruction-set architecture to decode as.</param>
    /// <param name="header">Optional header lines (without a trailing blank), or null.</param>
    /// <param name="resolver">Resolves a target address to a symbol name, or null for no naming.</param>
    public static (string Text, IReadOnlyList<NativeInstruction> Instructions, int HeaderLineCount)
        DisassembleWithText(
            ReadOnlySpan<byte> code, ulong baseAddress, NativeArchitecture arch,
            string? header = null, NativeSymbolResolver? resolver = null)
    {
        var instructions = Disassemble(code, baseAddress, arch, resolver);
        return Render(instructions, header);
    }

    /// <summary>
    /// The convenience the view, CLI, MCP, and session share: disassembles one recovered native
    /// symbol from its owning <paramref name="analyzer"/>. It slices the symbol's bytes, takes the
    /// architecture from the recovered symbol info (the real selected slice), resolves call/branch/
    /// data targets through the other symbols, stamps each instruction's source location from the
    /// symbol info's source map, and renders a header with the symbol name and its file:line.
    /// Returns null when the symbol has no file-backed bytes or the architecture is unknown.
    /// </summary>
    /// <param name="analyzer">The analyzer that recovered the symbol.</param>
    /// <param name="symbol">The symbol to disassemble.</param>
    public static (string Text, IReadOnlyList<NativeInstruction> Instructions, int HeaderLineCount)?
        DisassembleSymbol(AssemblyAnalyzer analyzer, NativeSymbol symbol) =>
        DisassembleSymbol(analyzer, symbol, managedNameResolver: null);

    /// <summary>
    /// <see cref="DisassembleSymbol(AssemblyAnalyzer, NativeSymbol)"/> with correlation-aware
    /// target naming: <paramref name="managedNameResolver"/> is consulted before the symbol
    /// and import resolvers, so a call target resolves to its pre-ILC companion-backed
    /// managed name when one exists. The two-argument overload is preserved as shipped
    /// public API and delegates here with a null resolver.
    /// </summary>
    /// <param name="analyzer">The analyzer that recovered the symbol.</param>
    /// <param name="symbol">The symbol to disassemble.</param>
    /// <param name="managedNameResolver">Maps a target virtual address to a managed display name, or null for none.</param>
    public static (string Text, IReadOnlyList<NativeInstruction> Instructions, int HeaderLineCount)?
        DisassembleSymbol(AssemblyAnalyzer analyzer, NativeSymbol symbol, Func<ulong, string?>? managedNameResolver)
        => DisassembleSymbol(analyzer, symbol, managedNameResolver, readyToRunImportResolver: null);

    internal static (string Text, IReadOnlyList<NativeInstruction> Instructions, int HeaderLineCount)?
        DisassembleSymbol(
            AssemblyAnalyzer analyzer,
            NativeSymbol symbol,
            Func<ulong, string?>? managedNameResolver,
            NativeSymbolResolver? readyToRunImportResolver)
    {
        var info = analyzer.NativeSymbols;
        if (info is null || symbol.FileOffset is not { } fileOffset || symbol.Size <= 0) return null;

        var arch = info.Architecture != NativeArchitecture.Unknown
            ? info.Architecture
            : MapArchitecture(analyzer.Architecture);
        // Only x64/AArch64 decode; a report-only architecture reports unsupported rather than
        // decoding its bytes as x64 (which would fabricate plausible-but-wrong instructions).
        if (arch is not (NativeArchitecture.X64 or NativeArchitecture.Arm64)) return null;

        // A composite component's symbols carry offsets into the owner composite, not this file — the
        // native code lives in the code image. Slice (and resolve imports) from there for R2R.
        var codeImage = analyzer.IsReadyToRun ? analyzer.ReadyToRunCodeImage ?? analyzer : analyzer;
        var raw = codeImage.RawBytes;
        if (fileOffset < 0 || fileOffset + symbol.Size > raw.Length) return null;
        var code = raw.Span.Slice((int)fileOffset, (int)symbol.Size).ToArray();

        // Compose the symbol resolver with the import resolver so indirect targets that land on an
        // import slot render as MODULE!Function rather than an unresolved address. The import table
        // is parsed once per image and cached — rebuilding it per call would re-read the whole PE
        // (copying megabytes) on every function selection. A ReadyToRun image resolves its indirect
        // call slots through its own ImportSections rather than the PE import directory.
        var imports = ImportResolverFor(codeImage);
        var r2rImports = readyToRunImportResolver is null && codeImage.IsReadyToRun
            ? ReadyToRunImportResolverFor(codeImage)
            : null;

        bool Resolver(ulong va, out NativeSymbolRef sym)
        {
            if (info.TryFindByAddress(va, out var found))
            {
                // Companion-backed names beat the reduced recovered-metadata join; the
                // offset-into-symbol delta renders the same either way.
                var name = managedNameResolver?.Invoke(found.VirtualAddress)
                    ?? found.ManagedName ?? found.Name;
                sym = new NativeSymbolRef(
                    found.VirtualAddress, name, found.Kind,
                    (long)(va - found.VirtualAddress));
                return true;
            }

            if (readyToRunImportResolver is not null && readyToRunImportResolver(va, out sym))
                return true;

            if (r2rImports is not null && r2rImports.TryResolve(va, out sym))
                return true;

            if (imports is not null && imports.TryResolve(va, out sym))
                return true;

            sym = default;
            return false;
        }

        var instructions = Disassemble(code, symbol.VirtualAddress, arch, Resolver);
        if (info.SourceMap is { } sourceMap)
            instructions = StampSource(instructions, sourceMap);

        var header = BuildSymbolHeader(symbol, instructions, info.SourceMap);
        return Render(instructions, header);
    }

    /// <summary>
    /// Resolves a disassembly target — a hex/decimal virtual address or a symbol name — to the
    /// matching executable symbols, so callers report an exact hit, an ambiguity, or a miss the same
    /// way. A hex <c>0x…</c> or decimal address resolves through the containing symbol; a name prefers
    /// an exact managed-name match, then the raw symbol name, then a suffix match.
    /// </summary>
    /// <param name="info">The recovered native symbols.</param>
    /// <param name="target">The address or name to resolve.</param>
    public static IReadOnlyList<NativeSymbol> FindExecutableSymbols(NativeSymbolInfo info, string target)
    {
        if (target.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? ulong.TryParse(target.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var va)
                : ulong.TryParse(target, out va))
        {
            return info.TryFindByAddress(va, out var found)
                && found.Kind is NativeSymbolKind.Function or NativeSymbolKind.Stub or NativeSymbolKind.Boundary
                ? [found] : [];
        }

        var executables = info.Symbols
            .Where(s => s.Kind is NativeSymbolKind.Function or NativeSymbolKind.Stub or NativeSymbolKind.Boundary)
            .ToList();

        List<NativeSymbol> exact = [.. executables.Where(s => string.Equals(s.ManagedName, target, StringComparison.Ordinal))];
        if (exact.Count > 0) return exact;

        List<NativeSymbol> raw = [.. executables.Where(s => string.Equals(s.Name, target, StringComparison.Ordinal))];
        if (raw.Count > 0) return raw;

        return [.. executables.Where(s => (s.ManagedName ?? s.Name).EndsWith(target, StringComparison.Ordinal))];
    }

    private static readonly ConditionalWeakTable<AssemblyAnalyzer, StrongBox<NativeImportResolver?>> ImportResolverCache = [];

    private static NativeImportResolver? ImportResolverFor(AssemblyAnalyzer analyzer) =>
        ImportResolverCache.GetValue(analyzer, a => new StrongBox<NativeImportResolver?>(
            NativeImportResolver.Build(a.RawBytes, a.NativeSymbols?.Architecture ?? NativeArchitecture.Unknown))).Value;

    private static readonly ConditionalWeakTable<AssemblyAnalyzer, StrongBox<ReadyToRun.ReadyToRunImportMap?>> ReadyToRunImportCache = [];

    private static ReadyToRun.ReadyToRunImportMap? ReadyToRunImportResolverFor(AssemblyAnalyzer analyzer) =>
        ReadyToRunImportCache.GetValue(
            analyzer, a => new StrongBox<ReadyToRun.ReadyToRunImportMap?>(ReadyToRun.ReadyToRunImportMap.Build(a))).Value;

    private static NativeArchitecture MapArchitecture(string architecture) => architecture.ToUpperInvariant() switch
    {
        "X64" => NativeArchitecture.X64,
        "ARM64" => NativeArchitecture.Arm64,
        _ => NativeArchitecture.Unknown,
    };

    private static List<NativeInstruction> StampSource(
        IReadOnlyList<NativeInstruction> instructions, NativeSourceMap sourceMap)
    {
        var stamped = new List<NativeInstruction>(instructions.Count);
        foreach (var insn in instructions)
        {
            stamped.Add(sourceMap.TryGetLine(insn.Address, out var file, out var line)
                ? insn with { SourceFile = file, Line = line }
                : insn);
        }

        return stamped;
    }

    private static string BuildSymbolHeader(
        NativeSymbol symbol, IReadOnlyList<NativeInstruction> instructions, NativeSourceMap? sourceMap)
    {
        var name = symbol.ManagedName ?? symbol.Name;
        if (sourceMap is not null && instructions.Count > 0
            && sourceMap.TryGetLine(symbol.VirtualAddress, out var file, out var line))
        {
            return $"{name}\n// Source: {file}:{line}";
        }

        return name;
    }

    /// <summary>
    /// Decodes a single instruction at <paramref name="offset"/>, dispatching to the per-arch
    /// decoder. The A64 decoder lands later; until then A64 uses the exact-width word fallback.
    /// </summary>
    private static NativeInstruction DecodeOne(
        NativeArchitecture arch, ReadOnlySpan<byte> code, int offset, ulong baseAddress) =>
        arch == NativeArchitecture.Arm64
            ? arm64.Arm64Decoder.Decode(code, offset, baseAddress + (ulong)offset)
            : x64.XarchDecoder.Decode(code, offset, baseAddress + (ulong)offset);

    /// <summary>One-byte <c>.byte 0x..</c> safety net for x64.</summary>
    private static NativeInstruction FallbackByte(ReadOnlySpan<byte> code, int offset, ulong baseAddress)
    {
        var b = code[offset];
        return Fallback(baseAddress + (ulong)offset, [b], ".byte", $"0x{b:x2}");
    }

    /// <summary>One 32-bit <c>.word 0x........</c> safety net for A64 (or bytes at a truncated tail).</summary>
    private static NativeInstruction FallbackWord(ReadOnlySpan<byte> code, int offset, ulong baseAddress)
    {
        if (offset + 4 > code.Length)
        {
            var b = code[offset];
            return Fallback(baseAddress + (ulong)offset, [b], ".byte", $"0x{b:x2}");
        }

        var word = (uint)(code[offset] | (code[offset + 1] << 8) | (code[offset + 2] << 16) | (code[offset + 3] << 24));
        return Fallback(baseAddress + (ulong)offset, [.. code.Slice(offset, 4)], ".word", $"0x{word:x8}");
    }

    private static NativeInstruction Fallback(ulong address, byte[] bytes, string mnemonic, string operandText) =>
        new(
            Address: address,
            Rva: null,
            FileOffset: null,
            Bytes: bytes,
            Length: bytes.Length,
            Mnemonic: mnemonic,
            Operands: [new NativeOperand(NativeOperandKind.Immediate, operandText)],
            OperandText: operandText,
            Category: NativeInstructionCategory.Unknown,
            Flow: NativeFlowKind.Sequential,
            IsFallback: true);

    /// <summary>
    /// Names each instruction's target and refines its kind: a target inside the window becomes a
    /// synthesized <c>loc_…</c> label; a resolved symbol becomes <c>Name</c> or <c>Name+0x..</c>.
    /// </summary>
    private static List<NativeInstruction> ResolveTargets(
        List<NativeInstruction> instructions, ulong windowStart, ulong windowEnd, NativeSymbolResolver? resolver)
    {
        // Intra-window target addresses that land on an instruction boundary get a label.
        var boundaries = new HashSet<ulong>();
        foreach (var insn in instructions) boundaries.Add(insn.Address);

        for (var i = 0; i < instructions.Count; i++)
        {
            var insn = instructions[i];
            if (insn.TargetAddress is not { } target) continue;

            if (target >= windowStart && target < windowEnd && boundaries.Contains(target))
            {
                instructions[i] = insn with
                {
                    TargetKind = NativeTargetKind.LocalLabel,
                    TargetName = LocalLabel(target),
                };
                continue;
            }

            if (resolver is not null && resolver(target, out var sym))
            {
                var name = sym.Offset == 0 ? sym.Name : $"{sym.Name}+0x{sym.Offset:x}";
                var kind = insn.TargetKind == NativeTargetKind.None
                    ? (sym.Kind == NativeSymbolKind.Function ? NativeTargetKind.Function : NativeTargetKind.Data)
                    : insn.TargetKind;
                instructions[i] = insn with { TargetKind = kind, TargetName = name };
                continue;
            }

            // An unresolved RIP-relative data reference still shows its absolute address, since the
            // operand text only carries the [rip+disp] form.
            if (insn.TargetKind == NativeTargetKind.Data)
                instructions[i] = insn with { TargetName = $"0x{target:x}" };
        }

        return instructions;
    }

    private static void ResolveArm64IndirectImports(
        List<NativeInstruction> instructions, NativeSymbolResolver resolver)
    {
        var registerValues = new Dictionary<string, ulong>(StringComparer.Ordinal);
        var registerImports = new Dictionary<string, NativeSymbolRef>(StringComparer.Ordinal);

        for (var i = 0; i < instructions.Count; i++)
        {
            var insn = instructions[i];
            switch (insn.Mnemonic)
            {
                case "adrp":
                case "adr":
                    if (RegisterOperand(insn, 0) is { } adrReg && insn.TargetAddress is { } target)
                    {
                        SetRegister(registerValues, registerImports, adrReg, target);
                    }
                    break;

                case "add":
                    if (RegisterOperand(insn, 0) is { } addDest
                        && RegisterOperand(insn, 1) is { } addSource
                        && ImmediateOperand(insn, 2) is { } addend
                        && registerValues.TryGetValue(addSource, out var baseAddress))
                    {
                        SetRegister(registerValues, registerImports, addDest, unchecked(baseAddress + (ulong)addend));
                    }
                    else if (RegisterOperand(insn, 0) is { } unknownAddDest)
                    {
                        ClearRegister(registerValues, registerImports, unknownAddDest);
                    }
                    break;

                case "mov":
                    if (RegisterOperand(insn, 0) is { } movDest
                        && RegisterOperand(insn, 1) is { } movSource)
                    {
                        if (registerValues.TryGetValue(movSource, out var sourceValue))
                        {
                            SetRegister(registerValues, registerImports, movDest, sourceValue);
                            if (registerImports.TryGetValue(movSource, out var sourceImport))
                                registerImports[movDest] = sourceImport;
                        }
                        else
                        {
                            ClearRegister(registerValues, registerImports, movDest);
                        }
                    }
                    break;

                case "ldr":
                    if (RegisterOperand(insn, 0) is { } loadDest
                        && MemoryOperand(insn, 1) is { } memory
                        && registerValues.TryGetValue(memory.BaseRegister, out var pointerBase))
                    {
                        var slot = unchecked(pointerBase + (ulong)memory.Displacement);
                        ClearRegister(registerValues, registerImports, loadDest);
                        if (resolver(slot, out var import))
                            registerImports[loadDest] = import;
                    }
                    else if (RegisterOperand(insn, 0) is { } unknownLoadDest)
                    {
                        ClearRegister(registerValues, registerImports, unknownLoadDest);
                    }
                    break;

                case "blr":
                case "br":
                    if (RegisterOperand(insn, 0) is { } branchReg
                        && registerImports.TryGetValue(branchReg, out var importRef))
                    {
                        var name = importRef.Offset == 0
                            ? importRef.Name
                            : $"{importRef.Name}+0x{importRef.Offset:x}";
                        instructions[i] = insn with
                        {
                            TargetAddress = importRef.Start,
                            TargetKind = NativeTargetKind.Import,
                            TargetName = name,
                        };
                    }
                    break;

                default:
                    if (MayWriteFirstRegister(insn.Mnemonic) && RegisterOperand(insn, 0) is { } dest)
                        ClearRegister(registerValues, registerImports, dest);
                    break;
            }
        }
    }

    private static string? RegisterOperand(NativeInstruction insn, int index) =>
        index < insn.Operands.Count && NormalizeArm64Register(insn.Operands[index].Register) is { } reg ? reg : null;

    private static long? ImmediateOperand(NativeInstruction insn, int index) =>
        index < insn.Operands.Count ? insn.Operands[index].Immediate : null;

    private readonly record struct Arm64MemoryOperand(string BaseRegister, long Displacement);

    private static Arm64MemoryOperand? MemoryOperand(NativeInstruction insn, int index)
    {
        if (index >= insn.Operands.Count
            || insn.Operands[index] is not { Kind: NativeOperandKind.Memory, MemoryBase: { } memoryBase } operand
            || NormalizeArm64Register(memoryBase) is not { } baseRegister)
        {
            return null;
        }

        return new Arm64MemoryOperand(baseRegister, operand.MemoryDisplacement);
    }

    private static void SetRegister(
        Dictionary<string, ulong> registerValues,
        Dictionary<string, NativeSymbolRef> registerImports,
        string register,
        ulong value)
    {
        registerValues[register] = value;
        registerImports.Remove(register);
    }

    private static void ClearRegister(
        Dictionary<string, ulong> registerValues,
        Dictionary<string, NativeSymbolRef> registerImports,
        string register)
    {
        registerValues.Remove(register);
        registerImports.Remove(register);
    }

    private static string? NormalizeArm64Register(string? register)
    {
        if (string.IsNullOrEmpty(register))
            return null;
        return register[0] is 'w' or 'x' && register.Length > 1 ? "x" + register[1..] : register;
    }

    private static bool MayWriteFirstRegister(string mnemonic) =>
        mnemonic is not ("str" or "strb" or "strh" or "stp" or "stur" or "sturb" or "sturh");

    /// <summary>The synthesized label name for an intra-function target address.</summary>
    internal static string LocalLabel(ulong address) => $"loc_{address:x}";

    private static (string, IReadOnlyList<NativeInstruction>, int) Render(
        IReadOnlyList<NativeInstruction> instructions, string? header)
    {
        var lines = new List<string>();
        if (!string.IsNullOrEmpty(header))
        {
            lines.AddRange(header.Split('\n'));
            lines.Add("");
        }

        var headerLineCount = lines.Count;

        // Addresses that are the target of an intra-function label get a "loc_…:" line above them.
        var labels = new HashSet<ulong>();
        foreach (var insn in instructions)
        {
            if (insn.TargetKind == NativeTargetKind.LocalLabel && insn.TargetAddress is { } t)
                labels.Add(t);
        }

        var rendered = new List<NativeInstruction>(instructions.Count);
        var lastLine = 0;
        var lastFile = (string?)null;
        foreach (var insn in instructions)
        {
            if (labels.Contains(insn.Address))
                lines.Add($"{LocalLabel(insn.Address)}:");

            if (insn.Line is { } line && (line != lastLine || insn.SourceFile != lastFile))
            {
                lines.Add($"// {insn.SourceFile}:{line}");
                lastLine = line;
                lastFile = insn.SourceFile;
            }

            var (text, layout) = RenderLine(insn);
            var displayLine = lines.Count + 1;
            lines.Add(text);
            rendered.Add(insn with { DisplayLine = displayLine, Layout = layout });
        }

        return (string.Join('\n', lines), rendered, headerLineCount);
    }

    /// <summary>
    /// Renders one instruction as <c>0x{addr}: {bytes}  {mnemonic} {operands}  ; {target}</c> and
    /// records the mnemonic/operand/target column spans for the decoration providers.
    /// </summary>
    private static (string Text, NativeLineLayout Layout) RenderLine(NativeInstruction insn)
    {
        var sb = new StringBuilder();
        sb.Append("0x").Append(insn.Address.ToString("x")).Append(": ");

        var bytesHex = string.Join(' ', insn.Bytes.Select(b => b.ToString("x2")));
        sb.Append(bytesHex.PadRight(BytesColumnWidth)).Append("  ");

        var mnemonicStart = sb.Length;
        sb.Append(insn.Mnemonic);
        var mnemonicLength = insn.Mnemonic.Length;

        var operandsStart = -1;
        var operandsLength = 0;
        if (!string.IsNullOrEmpty(insn.OperandText))
        {
            sb.Append(' ', Math.Max(1, MnemonicColumnWidth - mnemonicLength));
            operandsStart = sb.Length;
            sb.Append(insn.OperandText);
            operandsLength = insn.OperandText.Length;
        }

        var targetStart = -1;
        var targetLength = 0;
        if (insn.TargetName is { Length: > 0 } targetName)
        {
            sb.Append("  ; ");
            targetStart = sb.Length;
            sb.Append(targetName);
            targetLength = targetName.Length;
        }

        return (sb.ToString(),
            new NativeLineLayout(mnemonicStart, mnemonicLength, operandsStart, operandsLength, targetStart, targetLength));
    }

    private const int BytesColumnWidth = 24;
    private const int MnemonicColumnWidth = 8;
}
