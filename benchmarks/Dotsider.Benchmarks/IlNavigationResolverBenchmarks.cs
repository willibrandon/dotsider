using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="IlNavigationResolver"/> token resolution across all
/// supported metadata table kinds (MethodDef, TypeDef, FieldDef, MemberRef, TypeSpec,
/// MethodSpec) plus batch resolution of an entire method body.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class IlNavigationResolverBenchmarks
{
    private const string SamplePath = "samples/RichLibrary";
    private const string NavigationFixtureType = "RichLibrary.IlNavigationFixture";
    private const string StringHelpersType = "RichLibrary.Utilities.StringHelpers";

    private AssemblyAnalyzer _analyzer = null!;
    private IlDisassembler _disasm = null!;

    private int _methodDefToken;
    private int _typeDefToken;
    private int _fieldDefToken;
    private int _memberRefMethodToken;
    private int _memberRefFieldToken;
    private int _typeSpecToken;
    private int _methodSpecToken;
    private int[] _batchTokens = null!;

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelpers.BuildSample(SamplePath);
        var dllPath = BenchmarkHelpers.GetBuildPath(SamplePath, "RichLibrary.dll");
        _analyzer = new AssemblyAnalyzer(dllPath);
        _disasm = new IlDisassembler(_analyzer);

        _methodDefToken = FindToken(NavigationFixtureType, "CallLocalMethod", 0x06000000);
        _typeDefToken = FindToken(NavigationFixtureType, "CastToSelf", 0x02000000);
        _fieldDefToken = FindToken(NavigationFixtureType, "ReadInstanceField", 0x04000000);
        _memberRefMethodToken = FindToken(NavigationFixtureType, "CallExternal", 0x0A000000);
        _memberRefFieldToken = FindToken(NavigationFixtureType, "GetStringEmpty", 0x0A000000);
        _methodSpecToken = FindToken(NavigationFixtureType, "GenericUsage", 0x2B000000);
        _typeSpecToken = FindToken(StringHelpersType, "ToTitleCase", 0x1B000000);

        // Collect all tokens from CallLocalMethod for batch resolution.
        var callLocal = _analyzer.MethodDefs.First(m =>
            m.DeclaringType == NavigationFixtureType && m.Name == "CallLocalMethod");
        _batchTokens = _disasm.Disassemble(callLocal)
            .Where(i => i.MetadataToken.HasValue)
            .Select(i => i.MetadataToken!.Value)
            .ToArray();
    }

    [GlobalCleanup]
    public void Cleanup() => _analyzer.Dispose();

    [Benchmark(Description = "MethodDef")]
    [BenchmarkCategory("SingleToken")]
    public IlNavigationTarget Resolve_MethodDef()
        => IlNavigationResolver.Resolve(_analyzer, _methodDefToken);

    [Benchmark(Description = "TypeDef")]
    [BenchmarkCategory("SingleToken")]
    public IlNavigationTarget Resolve_TypeDef()
        => IlNavigationResolver.Resolve(_analyzer, _typeDefToken);

    [Benchmark(Description = "FieldDef")]
    [BenchmarkCategory("SingleToken")]
    public IlNavigationTarget Resolve_FieldDef()
        => IlNavigationResolver.Resolve(_analyzer, _fieldDefToken);

    [Benchmark(Description = "MemberRef (method)")]
    [BenchmarkCategory("SingleToken")]
    public IlNavigationTarget Resolve_MemberRef_Method()
        => IlNavigationResolver.Resolve(_analyzer, _memberRefMethodToken);

    [Benchmark(Description = "MemberRef (field)")]
    [BenchmarkCategory("SingleToken")]
    public IlNavigationTarget Resolve_MemberRef_Field()
        => IlNavigationResolver.Resolve(_analyzer, _memberRefFieldToken);

    [Benchmark(Description = "TypeSpec")]
    [BenchmarkCategory("SingleToken")]
    public IlNavigationTarget Resolve_TypeSpec()
        => IlNavigationResolver.Resolve(_analyzer, _typeSpecToken);

    [Benchmark(Description = "MethodSpec")]
    [BenchmarkCategory("SingleToken")]
    public IlNavigationTarget Resolve_MethodSpec()
        => IlNavigationResolver.Resolve(_analyzer, _methodSpecToken);

    [Benchmark(Description = "Batch method body")]
    [BenchmarkCategory("BatchResolve")]
    public int Resolve_BatchMethodBody()
    {
        var count = 0;
        foreach (var token in _batchTokens)
        {
            IlNavigationResolver.Resolve(_analyzer, token);
            count++;
        }

        return count;
    }

    private int FindToken(string typeName, string methodName, int tablePrefix)
    {
        var method = _analyzer.MethodDefs.First(m =>
            m.DeclaringType == typeName && m.Name == methodName);
        var instructions = _disasm.Disassemble(method);
        return instructions
            .First(i => i.MetadataToken.HasValue &&
                        (i.MetadataToken.Value & 0xFF000000) == tablePrefix)
            .MetadataToken!.Value;
    }
}
