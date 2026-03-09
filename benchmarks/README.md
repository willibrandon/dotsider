# Dotsider Benchmarks

BenchmarkDotNet project measuring performance of core analyzers against large BCL assemblies.

## Running

Run all benchmarks:

```sh
dotnet run --project benchmarks/Dotsider.Benchmarks -c Release
```

Run a specific benchmark class:

```sh
dotnet run --project benchmarks/Dotsider.Benchmarks -c Release -- --filter '*HexSearchBenchmarks*'
```

Run the parameterized hex search threshold test:

```sh
dotnet run --project benchmarks/Dotsider.Benchmarks -c Release -- --filter '*HexSearchThresholdBenchmarks*'
```

List all available benchmarks:

```sh
dotnet run --project benchmarks/Dotsider.Benchmarks -c Release -- --list flat
```

## Benchmark Classes

| Class | What it measures |
|---|---|
| `AssemblyAnalyzerBenchmarks` | Constructor and metadata table enumeration (TypeDefs, MethodDefs) |
| `AssemblyDifferBenchmarks` | Dictionary-based O(n) diff of two assemblies by type, method, and reference |
| `DependencyGraphBuilderBenchmarks` | Positioned dependency graph construction from assembly refs and type ref counts |
| `HexSearchBenchmarks` | `FindBytePattern` with short, long, and no-match patterns against real assemblies |
| `HexSearchThresholdBenchmarks` | Parameterized sweep (4–16MB) to pinpoint the 8ms adaptive search crossover |
| `IlDisassemblerBenchmarks` | Disassemble and format all methods |
| `NuGetPackageAnalyzerBenchmarks` | NuGet package construction and DLL extraction from .nupkg |
| `SizeAnalyzerBenchmarks` | `BuildSizeTree` full traversal |
| `StringExtractorBenchmarks` | UserStrings, MetadataStrings, and RawStrings extraction |
| `TreemapLayoutBenchmarks` | Squarified treemap rectangle computation for assembly size trees |
| `McpToolBenchmarks` | MCP tool call round-trip and session discovery through in-process pipe transport |

## Test Assemblies

Benchmarks use BCL assemblies from the running .NET runtime directory:

- **System.Private.CoreLib.dll** (~16MB) — largest BCL assembly, stress tests all analyzers
- **System.Private.Xml.dll** (~8MB) — mid-size assembly near the hex search threshold

## Baseline Results

### macOS — Apple M4 Pro, .NET 10.0.2

#### AssemblyAnalyzer

| Benchmark | Mean | Allocated |
|---|---|---|
| CoreLib Construction | 1.35 ms | 16.29 MB |
| Xml Construction | 0.66 ms | 8.60 MB |
| CoreLib TypeDefs | 2.13 ms | 17.31 MB |
| Xml TypeDefs | 1.19 ms | 9.25 MB |
| CoreLib MethodDefs | 23.5 ms | 47.04 MB |
| Xml MethodDefs | 9.13 ms | 18.14 MB |

#### AssemblyDiffer

| Benchmark | Mean | Allocated |
|---|---|---|
| CoreLib vs Xml (max diff) | 1,513.5 ns | 7.2 KB |
| CoreLib vs CoreLib (identity) | 463.4 ns | 2.93 KB |

#### DependencyGraphBuilder

| Benchmark | Mean | Allocated |
|---|---|---|
| CoreLib graph | 1,528.9 ns | 7.2 KB |
| Xml graph | 460.2 ns | 2.93 KB |

#### Hex Search (FindBytePattern)

| Benchmark | Mean | Allocated |
|---|---|---|
| CoreLib short pattern (2B) | 11.7 ms | 1,156 B |
| CoreLib long pattern (14B) | 13.1 ms | 33 KB |
| CoreLib no-match (full scan) | 13.1 ms | — |
| Xml short pattern (2B) | 6.2 ms | 614 B |
| Xml long pattern (14B) | 7.0 ms | 182 B |
| Xml no-match (full scan) | 7.0 ms | — |

#### Hex Search Threshold (8ms crossover)

| SizeMB | Mean |
|---|---|
| 4 | 3.28 ms |
| 8 | 6.51 ms |
| 9 | 7.33 ms |
| **10** | **8.14 ms** |
| 11 | 8.97 ms |
| 12 | 9.88 ms |
| 16 | 13.03 ms |

The 8ms adaptive threshold (`HexDumpView` line 44) crosses at ~10MB on this machine. Scaling is linear at ~0.82 ms/MB.

#### StringExtractor

| Benchmark | Mean | Allocated |
|---|---|---|
| CoreLib UserStrings | 0.05 ms | 42 KB |
| CoreLib MetadataStrings | 1.73 ms | 2,875 KB |
| CoreLib RawStrings | 27.3 ms | 7,430 KB |
| Xml UserStrings | 0.07 ms | 375 KB |
| Xml MetadataStrings | 1.03 ms | 1,519 KB |
| Xml RawStrings | 15.0 ms | 3,962 KB |

#### SizeAnalyzer

| Benchmark | Mean | Allocated |
|---|---|---|
| CoreLib BuildSizeTree | 11.1 ms | 28.49 MB |
| Xml BuildSizeTree | 3.32 ms | 10.03 MB |

#### NuGetPackageAnalyzer

| Benchmark | Mean | Allocated |
|---|---|---|
| Construction (2 DLLs, ~24MB) | 45.32 us | 42.59 KB |
| OpenDll (CoreLib ~16MB) | 32.51 ms | 16,727 KB |

#### TreemapLayout

| Benchmark | Mean | Allocated |
|---|---|---|
| CoreLib layout (120x30) | 1,506.1 ns | 7.2 KB |
| Xml layout (120x30) | 453.6 ns | 2.93 KB |
| CoreLib layout (240x60) | 1,493.7 ns | 7.2 KB |

#### IlDisassembler

| Benchmark | Mean | Allocated |
|---|---|---|
| CoreLib DisassembleAll | 42.1 ms | 134.18 MB |
| Xml DisassembleAll | 38.7 ms | 129.75 MB |
| CoreLib FormatAll | 79.4 ms | 285.24 MB |
| Xml FormatAll | 69.3 ms | 254.36 MB |

### MCP Server

Full tool call round-trip through the in-process pipe transport — includes JSON-RPC framing, DI resolution, filter execution, tool dispatch, analysis, JSON serialization, and response framing.

#### McpToolBenchmarks

| Benchmark | Mean | Allocated |
|---|---|---|
| GetAssemblyInfo (CoreLib) | 26.04 ms | 49,237 KB |
| ListTypes (CoreLib) | 10.67 ms | 25,186 KB |
| GetSizeBreakdown (CoreLib) | 192.36 ms | 205,760 KB |
| DisassembleMethod (single) | 25.23 ms | 48,204 KB |
| ExtractStrings (CoreLib) | 135.46 ms | 117,025 KB |
| DiscoverSessions (5 sockets) | 0.20 ms | 143 KB |

Session discovery exercises the full path: directory scan, UDS connect to each socket, assembly-info round-trip, stale socket cleanup, and JSON serialization. Benchmarks use an isolated temp directory for reproducibility.
