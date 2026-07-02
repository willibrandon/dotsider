using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the native import/export/load-config readers, exercised through the public
/// <see cref="AssemblyAnalyzer"/> properties against real sample binaries. Each CI OS
/// publishes its own-format Native AOT binary, so the import/export cases cover PE on
/// Windows, ELF on Linux, and Mach-O on macOS. A synthetic PE covers export shapes
/// (forwarders, ordinal-only) that no sample produces.
/// </summary>
[Collection("SampleAssemblies")]
public class PeDirectoryReaderTests(SampleAssemblyFixture samples)
{
    /// <summary>The core system library name expected in the import table of the running OS.</summary>
    private static string CoreImportLibrary =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "kernel32"
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "libSystem"
        : "libc";

    /// <summary>
    /// Verifies the apphost executable's import table lists the platform's core system library.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Imports_ApphostExe_ContainsCoreLibrary()
    {
        using var analyzer = new AssemblyAnalyzer(samples.HelloWorldExe);

        var imports = analyzer.Imports;

        Assert.NotEmpty(imports);
        Assert.Contains(imports, m =>
            m.ModuleName.Contains(CoreImportLibrary, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies the Native AOT executable imports the platform's core system library
    /// with named functions attributed to it (PE thunks, ELF versioned symbols, or
    /// Mach-O two-level-namespace bindings).
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Imports_NativeAotExe_ContainsCoreLibraryWithFunctions()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);

        var imports = analyzer.Imports;

        Assert.NotEmpty(imports);
        Assert.Contains(imports, m =>
            m.ModuleName.Contains(CoreImportLibrary, StringComparison.OrdinalIgnoreCase));

        var named = imports.SelectMany(m => m.Functions).Where(f => f.Name is not null).ToList();
        Assert.NotEmpty(named);
        Assert.All(named, f => Assert.Null(f.Ordinal));
    }

    /// <summary>
    /// Verifies a managed DLL parses without errors — its import table is empty or tiny.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Imports_ManagedDll_WellFormed()
    {
        using var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);

        var imports = analyzer.Imports;

        Assert.NotNull(imports);
        Assert.All(imports, m => Assert.False(string.IsNullOrEmpty(m.ModuleName)));
    }

    /// <summary>
    /// Verifies export parsing on real samples never throws — the samples export nothing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Exports_RealSamples_WellFormed()
    {
        using var apphost = new AssemblyAnalyzer(samples.HelloWorldExe);
        using var managed = new AssemblyAnalyzer(samples.RichLibraryDll);

        Assert.NotNull(apphost.Exports);
        Assert.NotNull(managed.Exports);
    }

    /// <summary>
    /// Verifies named, forwarded, and ordinal-only exports parse from a synthetic PE.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Exports_SyntheticPe_ParsesNamedForwarderAndOrdinalOnly()
    {
        using var peReader = new PEReader(new MemoryStream(BuildExportTestPe()));

        var exports = PeDirectoryReader.ReadExports(peReader);

        Assert.Equal(3, exports.Count);

        var alpha = Assert.Single(exports, e => e.Name == "Alpha");
        Assert.Equal(5, alpha.Ordinal);
        Assert.Null(alpha.ForwardedTo);

        var beta = Assert.Single(exports, e => e.Name == "Beta");
        Assert.Equal(6, beta.Ordinal);
        Assert.Equal("NTDLL.RtlAllocateHeap", beta.ForwardedTo);

        var ordinalOnly = Assert.Single(exports, e => e.Name is null);
        Assert.Equal(7, ordinalOnly.Ordinal);
    }

    /// <summary>
    /// Verifies the Native AOT executable exports parse without error. An ELF image
    /// exports its runtime debug header; a Windows PE executable typically exports
    /// nothing, which is equally valid.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Exports_NativeAotExe_WellFormed()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);

        var exports = analyzer.Exports;

        Assert.NotNull(exports);
        Assert.All(exports, e => Assert.False(string.IsNullOrEmpty(e.Name)));
    }

    /// <summary>
    /// Verifies the Native AOT executable's load configuration parses with a security
    /// cookie on Windows, where the PE load configuration directory exists. The
    /// directory is a PE-only structure with no ELF or Mach-O equivalent.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void LoadConfig_NativeAotExe_HasSecurityCookie()
    {
        Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            "the load configuration directory is a PE-only structure");
        Assert.SkipWhen(samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);

        var loadConfig = analyzer.LoadConfig;

        Assert.NotNull(loadConfig);
        Assert.True(loadConfig.Size > 0);
        Assert.NotEqual(0UL, loadConfig.SecurityCookie);
        Assert.False(string.IsNullOrEmpty(loadConfig.GuardFlagsDescription));
    }

    /// <summary>
    /// Verifies the apphost executable's load configuration parses on Windows. The
    /// directory is a PE-only structure with no ELF or Mach-O equivalent.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void LoadConfig_ApphostExe_Parses()
    {
        Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            "the load configuration directory is a PE-only structure");

        using var analyzer = new AssemblyAnalyzer(samples.HelloWorldExe);

        var loadConfig = analyzer.LoadConfig;

        Assert.NotNull(loadConfig);
        Assert.True(loadConfig.Size > 0);
    }

    /// <summary>
    /// Verifies an import directory pointing outside every section yields an empty
    /// result rather than throwing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Imports_UnmappedDirectory_ReturnsEmpty()
    {
        using var peReader = new PEReader(
            new MemoryStream(BuildExportTestPe(importDirRva: 0x5000, importDirSize: 0x100)));

        Assert.Empty(PeDirectoryReader.ReadImports(peReader));
    }

    /// <summary>
    /// Verifies all directory properties degrade gracefully for a non-PE native binary.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NonPe_AllDirectories_Empty()
    {
        var elfBytes = new byte[128];
        elfBytes[0] = 0x7F;
        elfBytes[1] = (byte)'E';
        elfBytes[2] = (byte)'L';
        elfBytes[3] = (byte)'F';

        using var analyzer = new AssemblyAnalyzer(elfBytes, "fake.so");

        Assert.Empty(analyzer.Imports);
        Assert.Empty(analyzer.Exports);
        Assert.Null(analyzer.LoadConfig);
    }

    /// <summary>
    /// Builds a minimal valid PE32+ image with one .edata section holding an export
    /// directory: "Alpha" (ordinal 5), a forwarder "Beta" (ordinal 6), and an
    /// ordinal-only export (ordinal 7).
    /// </summary>
    private static byte[] BuildExportTestPe(int importDirRva = 0, int importDirSize = 0)
    {
        var image = new byte[0x400];
        var w = new SpanWriter(image);

        // DOS header
        w.U16(0x0000, 0x5A4D); // MZ
        w.U32(0x003C, 0x80);   // e_lfanew

        // PE signature + COFF header
        w.U32(0x0080, 0x00004550);        // "PE\0\0"
        w.U16(0x0084, 0x8664);            // Machine: x64
        w.U16(0x0086, 1);                 // NumberOfSections
        w.U16(0x0090, 240);               // SizeOfOptionalHeader
        w.U16(0x0092, 0x2022);            // Characteristics: EXE | LARGE_ADDRESS_AWARE | DLL

        // Optional header (PE32+) at 0x98
        w.U16(0x0098, 0x020B);            // Magic
        w.U32(0x0098 + 0x14, 0x1000);     // BaseOfCode
        w.U64(0x0098 + 0x18, 0x1_8000_0000); // ImageBase
        w.U32(0x0098 + 0x20, 0x1000);     // SectionAlignment
        w.U32(0x0098 + 0x24, 0x200);      // FileAlignment
        w.U16(0x0098 + 0x28, 6);          // MajorOSVersion
        w.U16(0x0098 + 0x30, 6);          // MajorSubsystemVersion
        w.U32(0x0098 + 0x38, 0x2000);     // SizeOfImage
        w.U32(0x0098 + 0x3C, 0x200);      // SizeOfHeaders
        w.U16(0x0098 + 0x44, 3);          // Subsystem: console
        w.U64(0x0098 + 0x48, 0x100000);   // SizeOfStackReserve
        w.U64(0x0098 + 0x50, 0x1000);     // SizeOfStackCommit
        w.U64(0x0098 + 0x58, 0x100000);   // SizeOfHeapReserve
        w.U64(0x0098 + 0x60, 0x1000);     // SizeOfHeapCommit
        w.U32(0x0098 + 0x6C, 16);         // NumberOfRvaAndSizes

        // Data directories at optional+0x70: [0] export, [1] import
        w.U32(0x0098 + 0x70, 0x1000);     // export RVA
        w.U32(0x0098 + 0x74, 0xC0);       // export size
        w.U32(0x0098 + 0x78, (uint)importDirRva);
        w.U32(0x0098 + 0x7C, (uint)importDirSize);

        // Section table at 0x188: ".edata", RVA 0x1000, file 0x200, 0x200 bytes
        "edata"u8.CopyTo(image.AsSpan(0x0189)); // ".edata" with leading dot
        image[0x0188] = (byte)'.';
        w.U32(0x0188 + 0x08, 0x200);      // VirtualSize
        w.U32(0x0188 + 0x0C, 0x1000);     // VirtualAddress
        w.U32(0x0188 + 0x10, 0x200);      // SizeOfRawData
        w.U32(0x0188 + 0x14, 0x200);      // PointerToRawData
        w.U32(0x0188 + 0x24, 0x40000040); // INITIALIZED_DATA | READ

        // Export directory at file 0x200 (RVA 0x1000)
        const int S = 0x200;              // file offset of section; RVA = offset - S + 0x1000
        w.U32(S + 0x0C, 0x1060);          // NameRVA -> "TestLib.dll"
        w.U32(S + 0x10, 5);               // OrdinalBase
        w.U32(S + 0x14, 3);               // NumberOfFunctions
        w.U32(S + 0x18, 2);               // NumberOfNames
        w.U32(S + 0x1C, 0x1028);          // AddressOfFunctions
        w.U32(S + 0x20, 0x1034);          // AddressOfNames
        w.U32(S + 0x24, 0x103C);          // AddressOfNameOrdinals

        // Function RVAs: [0]=Alpha code (outside dir), [1]=forwarder (inside dir), [2]=ordinal-only
        w.U32(S + 0x28, 0x1100);
        w.U32(S + 0x2C, 0x10A0);
        w.U32(S + 0x30, 0x1110);

        // Name pointer table + name ordinal table
        w.U32(S + 0x34, 0x1070);          // -> "Alpha"
        w.U32(S + 0x38, 0x1078);          // -> "Beta"
        w.U16(S + 0x3C, 0);               // Alpha -> function index 0
        w.U16(S + 0x3E, 1);               // Beta -> function index 1

        // Strings
        "TestLib.dll"u8.CopyTo(image.AsSpan(S + 0x60));
        "Alpha"u8.CopyTo(image.AsSpan(S + 0x70));
        "Beta"u8.CopyTo(image.AsSpan(S + 0x78));
        "NTDLL.RtlAllocateHeap"u8.CopyTo(image.AsSpan(S + 0xA0));

        return image;
    }

    /// <summary>Little-endian field writer over an in-memory PE image.</summary>
    private readonly ref struct SpanWriter(Span<byte> span)
    {
        private readonly Span<byte> _span = span;

        /// <summary>Writes a little-endian 16-bit value at the given offset.</summary>
        public void U16(int offset, ushort value) =>
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(_span[offset..], value);

        /// <summary>Writes a little-endian 32-bit value at the given offset.</summary>
        public void U32(int offset, uint value) =>
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(_span[offset..], value);

        /// <summary>Writes a little-endian 64-bit value at the given offset.</summary>
        public void U64(int offset, ulong value) =>
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(_span[offset..], value);
    }
}
