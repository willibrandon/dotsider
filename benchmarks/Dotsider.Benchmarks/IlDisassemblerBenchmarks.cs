using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="IlDisassembler"/> disassembly of all methods
/// in large BCL assemblies, plus single-method <see cref="IlDisassembler.DisassembleWithText"/>
/// and <see cref="IlDisassembler.GetHeaderLineCount"/>. Some methods in CoreLib contain tokens
/// that reference forward-declared runtime types, causing BadImageFormatException
/// during operand resolution — these are caught and skipped, matching the
/// real app's behavior.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class IlDisassemblerBenchmarks
{
    private AssemblyAnalyzer _coreLibAnalyzer = null!;
    private AssemblyAnalyzer _xmlAnalyzer = null!;
    private IlDisassembler _coreLibDisasm = null!;
    private IlDisassembler _xmlDisasm = null!;
    private MethodDefInfo _representativeMethod = null!;

    /// <summary>
    /// Opens BCL analyzers, wires up disassemblers, and picks a representative method with a non-trivial body.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        _coreLibAnalyzer = new AssemblyAnalyzer(Path.Combine(runtimeDir, "System.Private.CoreLib.dll"));
        _xmlAnalyzer = new AssemblyAnalyzer(Path.Combine(runtimeDir, "System.Private.Xml.dll"));
        _coreLibDisasm = new IlDisassembler(_coreLibAnalyzer);
        _xmlDisasm = new IlDisassembler(_xmlAnalyzer);

        // Pick a representative method with a non-trivial body (>10 instructions)
        foreach (var method in _coreLibAnalyzer.MethodDefs)
        {
            try
            {
                var instructions = _coreLibDisasm.Disassemble(method);
                if (instructions.Count > 10)
                {
                    _representativeMethod = method;
                    break;
                }
            }
            catch (BadImageFormatException) { }
        }

        _representativeMethod ??= _coreLibAnalyzer.MethodDefs[0];
    }

    /// <summary>
    /// Disposes the shared analyzers.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _coreLibAnalyzer.Dispose();
        _xmlAnalyzer.Dispose();
    }

    /// <summary>
    /// Disassembles every CoreLib method body to instruction streams, summing total instruction count.
    /// </summary>
    [Benchmark(Description = "CoreLib DisassembleAll")]
    public int CoreLib_DisassembleAll()
    {
        var count = 0;
        foreach (var method in _coreLibAnalyzer.MethodDefs)
        {
            try
            {
                var instructions = _coreLibDisasm.Disassemble(method);
                count += instructions.Count;
            }
            catch (BadImageFormatException) { }
        }
        return count;
    }

    /// <summary>
    /// Disassembles every Xml method body to instruction streams.
    /// </summary>
    [Benchmark(Description = "Xml DisassembleAll")]
    public int Xml_DisassembleAll()
    {
        var count = 0;
        foreach (var method in _xmlAnalyzer.MethodDefs)
        {
            try
            {
                var instructions = _xmlDisasm.Disassemble(method);
                count += instructions.Count;
            }
            catch (BadImageFormatException) { }
        }
        return count;
    }

    /// <summary>
    /// Adds full textual formatting on top of disassembly for every CoreLib method.
    /// </summary>
    [Benchmark(Description = "CoreLib FormatAll")]
    public int CoreLib_FormatAll()
    {
        var totalLen = 0;
        foreach (var method in _coreLibAnalyzer.MethodDefs)
        {
            try
            {
                totalLen += _coreLibDisasm.FormatDisassembly(method).Length;
            }
            catch (BadImageFormatException) { }
        }
        return totalLen;
    }

    /// <summary>
    /// Full textual formatting across every Xml method.
    /// </summary>
    [Benchmark(Description = "Xml FormatAll")]
    public int Xml_FormatAll()
    {
        var totalLen = 0;
        foreach (var method in _xmlAnalyzer.MethodDefs)
        {
            try
            {
                totalLen += _xmlDisasm.FormatDisassembly(method).Length;
            }
            catch (BadImageFormatException) { }
        }
        return totalLen;
    }

    // --- Single-method benchmarks (used on every method selection in the UI) ---

    /// <summary>
    /// Measures the DisassembleWithText hot path invoked on every UI method selection.
    /// </summary>
    [Benchmark(Description = "CoreLib DisassembleWithText single method")]
    [BenchmarkCategory("SingleMethod")]
    public int CoreLib_DisassembleWithText_SingleMethod()
    {
        var result = _coreLibDisasm.DisassembleWithText(_representativeMethod);
        return result?.Text.Length ?? 0;
    }

    /// <summary>
    /// Measures the GetHeaderLineCount fast path used for cursor positioning in the IL view.
    /// </summary>
    [Benchmark(Description = "CoreLib GetHeaderLineCount single method")]
    [BenchmarkCategory("SingleMethod")]
    public int CoreLib_GetHeaderLineCount_SingleMethod()
        => _coreLibDisasm.GetHeaderLineCount(_representativeMethod);
}
