using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="IlDisassembler"/> disassembly of all methods
/// in large BCL assemblies. Some methods in CoreLib contain tokens that
/// reference forward-declared runtime types, causing BadImageFormatException
/// during operand resolution — these are caught and skipped, matching the
/// real app's behavior.
/// </summary>
[MemoryDiagnoser]
public class IlDisassemblerBenchmarks
{
    private AssemblyAnalyzer _coreLibAnalyzer = null!;
    private AssemblyAnalyzer _xmlAnalyzer = null!;
    private IlDisassembler _coreLibDisasm = null!;
    private IlDisassembler _xmlDisasm = null!;

    [GlobalSetup]
    public void Setup()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        _coreLibAnalyzer = new AssemblyAnalyzer(Path.Combine(runtimeDir, "System.Private.CoreLib.dll"));
        _xmlAnalyzer = new AssemblyAnalyzer(Path.Combine(runtimeDir, "System.Private.Xml.dll"));
        _coreLibDisasm = new IlDisassembler(_coreLibAnalyzer);
        _xmlDisasm = new IlDisassembler(_xmlAnalyzer);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _coreLibAnalyzer.Dispose();
        _xmlAnalyzer.Dispose();
    }

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
}
